"""Deterministic technical textures. Run from the Unity project root with Python 3."""
import math
import pathlib
import random
import struct
import uuid
import zlib

DEST = pathlib.Path("Assets/CandyRush/Resources/Candy/Visuals")
DEST.mkdir(parents=True, exist_ok=True)
SIZE = 128


def png(name, pixels, normal=False, linear=False):
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data))
    raw = b"".join(b"\0" + bytes(pixels[y * SIZE * 3:(y + 1) * SIZE * 3]) for y in range(SIZE))
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    path = DEST / (name + ".png")
    path.write_bytes(data)
    meta = pathlib.Path(str(path) + ".meta")
    if not meta.exists():
        meta.write_text(f"""fileFormatVersion: 2
guid: {uuid.uuid4().hex}
TextureImporter:
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {0 if linear or normal else 1}
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: {1 if normal else 0}
  isReadable: 0
  textureType: {1 if normal else 0}
  textureShape: 1
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 2
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  maxTextureSize: 256
  textureCompression: 0
  alphaSource: 0
  alphaIsTransparency: 0
""", encoding="utf-8")


rng = random.Random(3526)
noise, color, fur_normal, tire_normal = [], [], [], []
for y in range(SIZE):
    for x in range(SIZE):
        noise.extend(rng.randrange(256) for _ in range(3))
        fiber = math.sin(2 * math.pi * (x * 11 / SIZE + .08 * math.sin(y * 2 * math.pi / SIZE)))
        c = round(246 + fiber * 5 + rng.uniform(-3, 3))
        color.extend([c, c, c])
        for dest, nx, ny in (
            (fur_normal, .22 * fiber, .04 * math.sin(2 * math.pi * y * 5 / SIZE)),
            (tire_normal, .6 * math.sin(2 * math.pi * (x * 8 + y * 4) / SIZE),
             .32 * math.sin(2 * math.pi * y * 8 / SIZE)),
        ):
            length = math.sqrt(nx * nx + ny * ny + 1)
            dest.extend(round((v / length * .5 + .5) * 255) for v in (nx, ny, 1))
png("FurNoise", noise, linear=True)
png("FurColor", color)
png("FurNormal", fur_normal, normal=True)
png("TireNormal", tire_normal, normal=True)
print("Created four seamless 128x128 textures in", DEST)
