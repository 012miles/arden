# Packs downloaded ambientCG ground materials into two vertical strips Unity imports as Texture2DArrays:
#   TerrainDetailA.png  RGB = the photo's colour divided by its own average (stored around 0.5, so the planet's map
#                        colour stays in charge and the photo only adds texture), A = height (for blending)
#   TerrainDetailN.png  RGB = tangent-space normal (OpenGL convention)
# Layer order must match RkTerrain layers in RocketPlanet.shader / RenderEnv.TerrainLayers.
import sys, os
from PIL import Image, ImageStat, ImageChops
src, out = sys.argv[1], sys.argv[2]
LAYERS = ["Grass001", "Rock051", "Ground054", "Snow010A", "Gravel043", "Snow006", "Ground096B", "Ground110"]
S = 1024
A = Image.new("RGBA", (S, S * len(LAYERS)))
N = Image.new("RGB", (S, S * len(LAYERS)))
for i, name in enumerate(LAYERS):
    d = os.path.join(src, name + "_2K-JPG")
    col = Image.open(os.path.join(d, name + "_2K-JPG_Color.jpg")).convert("RGB").resize((S, S), Image.LANCZOS)
    mean = ImageStat.Stat(col).mean
    bands = []
    for c, m in zip(col.split(), mean):
        k = 0.5 / max(m / 255.0, 0.02)
        bands.append(c.point(lambda v, k=k: max(0, min(255, int(v * k + 0.5)))))
    hp = os.path.join(d, name + "_2K-JPG_Displacement.jpg")
    h = Image.open(hp).convert("L").resize((S, S), Image.LANCZOS) if os.path.exists(hp) else Image.new("L", (S, S), 128)
    nrm = Image.open(os.path.join(d, name + "_2K-JPG_NormalGL.jpg")).convert("RGB").resize((S, S), Image.LANCZOS)
    A.paste(Image.merge("RGBA", (*bands, h)), (0, i * S))
    N.paste(nrm, (0, i * S))
    print(name, [round(m) for m in mean])
A.save(os.path.join(out, "TerrainDetailA.png"))
N.save(os.path.join(out, "TerrainDetailN.png"))
