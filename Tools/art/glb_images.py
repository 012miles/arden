# Lists, extracts or replaces the images inside a .glb (binary glTF): the JSON chunk and BIN chunk are rebuilt
#   python glb_images.py extract model.glb outdir
#   python glb_images.py replace model.glb index new.png out.glb
import sys, json, struct, os

def load(path):
    b = open(path, 'rb').read()
    jl = struct.unpack('<I', b[12:16])[0]
    j = json.loads(b[20:20 + jl])
    o = 20 + jl
    bl = struct.unpack('<I', b[o:o + 4])[0]
    bin_ = b[o + 8:o + 8 + bl]
    return j, bin_

def view(j, bin_, i):
    v = j['bufferViews'][i]
    s = v.get('byteOffset', 0)
    return bin_[s:s + v['byteLength']]

def save(path, j, views):
    out = bytearray()
    for i, data in enumerate(views):
        while len(out) % 4: out += b'\0'
        j['bufferViews'][i]['byteOffset'] = len(out)
        j['bufferViews'][i]['byteLength'] = len(data)
        out += data
    while len(out) % 4: out += b'\0'
    j['buffers'][0]['byteLength'] = len(out)
    js = json.dumps(j, separators=(',', ':')).encode()
    while len(js) % 4: js += b' '
    total = 12 + 8 + len(js) + 8 + len(out)
    with open(path, 'wb') as f:
        f.write(struct.pack('<III', 0x46546C67, 2, total))
        f.write(struct.pack('<II', len(js), 0x4E4F534A)); f.write(js)
        f.write(struct.pack('<II', len(out), 0x004E4942)); f.write(out)

if __name__ == '__main__':
    cmd, path = sys.argv[1], sys.argv[2]
    j, bin_ = load(path)
    if cmd == 'extract':
        os.makedirs(sys.argv[3], exist_ok=True)
        for k, im in enumerate(j.get('images', [])):
            ext = '.png' if im.get('mimeType') == 'image/png' else '.jpg'
            open(os.path.join(sys.argv[3], f'image{k}{ext}'), 'wb').write(view(j, bin_, im['bufferView']))
            print('image', k, im.get('mimeType'))
    elif cmd == 'replace':
        k, newimg, outp = int(sys.argv[3]), sys.argv[4], sys.argv[5]
        views = [view(j, bin_, i) for i in range(len(j['bufferViews']))]
        views[j['images'][k]['bufferView']] = open(newimg, 'rb').read()
        j['images'][k]['mimeType'] = 'image/png' if newimg.endswith('.png') else 'image/jpeg'
        save(outp, j, views)
        print('wrote', outp)

def shrink(path, outp, maxsize):
    """Every image in the glb scaled down to at most maxsize pixels; ones without transparency become JPEG."""
    from PIL import Image
    import io
    j, bin_ = load(path)
    views = [view(j, bin_, i) for i in range(len(j['bufferViews']))]
    for im in j.get('images', []):
        data = views[im['bufferView']]
        img = Image.open(io.BytesIO(data))
        alpha = img.mode in ('RGBA', 'LA') and img.getchannel('A').getextrema()[0] < 250
        if max(img.size) > maxsize:
            k = maxsize / max(img.size)
            img = img.resize((max(1, int(img.size[0] * k)), max(1, int(img.size[1] * k))), Image.LANCZOS)
        buf = io.BytesIO()
        if alpha:
            img.save(buf, 'PNG', optimize=True); im['mimeType'] = 'image/png'
        else:
            img.convert('RGB').save(buf, 'JPEG', quality=86); im['mimeType'] = 'image/jpeg'
        views[im['bufferView']] = buf.getvalue()
    save(outp, j, views)

if __name__ == '__main__' and sys.argv[1] == 'shrink':
    shrink(sys.argv[2], sys.argv[3], int(sys.argv[4]))
    print('shrunk', sys.argv[3])
