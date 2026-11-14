# Packs the downloaded ambientCG materials into two images that Unity imports as Texture2DArrays (a 2-wide grid,
# read left to right, top to bottom, so the image stays under Unity's 8192-pixel limit):
#   PartDetailA.png  RGB albedo + A roughness (one 1024x1024 layer per material)
#   PartDetailN.png  RGB tangent-space normal (OpenGL convention)
# Layer order must match Rocket/Lit (RocketLit.shader) and MeshBuilder.Mat*:
#   0 foil, 1 metal, 2 dark metal, 3 concrete, 4 asphalt,
#   5 office facade, 6 concrete-frame facade, 7 glass curtain wall, 8 metal cladding, 9 paving slabs, 10 lawn, 11 roof gravel
import sys, os
from PIL import Image
src = sys.argv[1]   # Downloads/ambientcg
out = sys.argv[2]   # Assets/Game/Resources/Textures
LAYERS = ["Foil002", "Metal032", "Metal027", "Concrete034", "Asphalt031",
          "Facade006", "Facade019A", "Facade001", "CorrugatedSteel005", "PavingStones136", "Grass004", "Gravel043"]
S, COLS = 1024, 2
ROWS = (len(LAYERS) + COLS - 1) // COLS
A = Image.new("RGBA", (S * COLS, S * ROWS))
N = Image.new("RGB", (S * COLS, S * ROWS), (128, 128, 255))
for i, name in enumerate(LAYERS):
    d = os.path.join(src, name + "_2K-JPG")
    col = Image.open(os.path.join(d, name + "_2K-JPG_Color.jpg")).convert("RGB").resize((S, S), Image.LANCZOS)
    rough = Image.open(os.path.join(d, name + "_2K-JPG_Roughness.jpg")).convert("L").resize((S, S), Image.LANCZOS)
    nrm = Image.open(os.path.join(d, name + "_2K-JPG_NormalGL.jpg")).convert("RGB").resize((S, S), Image.LANCZOS)
    r, g, b = col.split()
    at = ((i % COLS) * S, (i // COLS) * S)
    A.paste(Image.merge("RGBA", (r, g, b, rough)), at)
    N.paste(nrm, at)
A.save(os.path.join(out, "PartDetailA.png"))
N.save(os.path.join(out, "PartDetailN.png"))
print("layers", len(LAYERS), "grid", COLS, "x", ROWS)
