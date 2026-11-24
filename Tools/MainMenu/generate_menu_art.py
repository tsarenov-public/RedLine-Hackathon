"""Generate the NFS Most Wanted style city map and the flat UI sprites for the main menu with Pillow."""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2] / "Assets"
ROADS = ROOT / "Cars/MinimapHUD/Stage_1_4_Roads.png"
OUT = ROOT / "UI/Menu"
SPRITES = OUT / "Sprites"
MAP = 2048
AA = 4


def smooth_noise(rng, cells, size):
    grid = rng.random((cells, cells)).astype(np.float32)
    image = Image.fromarray((grid * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return np.asarray(image, dtype=np.float32) / 255 - .5


def morph(mask, size, grow):
    image = Image.fromarray(mask)
    for _ in range(size // 2):
        image = image.filter(ImageFilter.MaxFilter(3) if grow else ImageFilter.MinFilter(3))
    return np.asarray(image, dtype=np.float32) / 255


def city_map():
    rng = np.random.default_rng(2005)
    alpha = np.asarray(Image.open(ROADS).convert("RGBA"))[:, :, 3]

    terrain = (smooth_noise(rng, 6, MAP) * 18 + smooth_noise(rng, 24, MAP) * 9 +
               smooth_noise(rng, 160, MAP) * 5 + (rng.random((MAP, MAP)) - .5) * 4)
    base = np.stack([62 + terrain, 66 + terrain, 70 + terrain * 1.1], axis=-1)

    y, x = np.mgrid[0:MAP, 0:MAP]
    major = ((x % 128) < 2) | ((y % 128) < 2)
    minor = ((x % 32) < 1) | ((y % 32) < 1)
    base -= major[..., None] * 9 + minor[..., None] * 3

    casing = morph(alpha, 4, True)
    shadow = np.asarray(Image.fromarray((casing * 255).astype(np.uint8))
                        .filter(ImageFilter.GaussianBlur(14)), dtype=np.float32) / 255
    core = morph(alpha, 8, False)
    core = np.asarray(Image.fromarray((core * 255).astype(np.uint8))
                      .filter(ImageFilter.GaussianBlur(.8)), dtype=np.float32) / 255

    base *= (1 - shadow[..., None] * .45)
    base = base * (1 - casing[..., None]) + np.array([24, 26, 29]) * casing[..., None]
    base = base * (1 - core[..., None]) + np.array([214, 218, 221]) * core[..., None]

    image = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB")
    image.save(OUT / "MenuMap.png", optimize=True)


def canvas(size):
    return Image.new("L", (size * AA, size * AA), 0)


def save(mask, name, size):
    mask = mask.resize((size, size), Image.LANCZOS)
    white = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    white.putalpha(mask)
    white.save(SPRITES / name)


def circle(size=256):
    mask = canvas(size)
    ImageDraw.Draw(mask).ellipse((2 * AA, 2 * AA, (size - 2) * AA, (size - 2) * AA), fill=255)
    save(mask, "Circle.png", size)


def ring(size=256, width=18):
    mask = canvas(size)
    draw = ImageDraw.Draw(mask)
    draw.ellipse((2 * AA, 2 * AA, (size - 2) * AA, (size - 2) * AA), fill=255)
    draw.ellipse(((2 + width) * AA, (2 + width) * AA, (size - 2 - width) * AA, (size - 2 - width) * AA), fill=0)
    save(mask, "Ring.png", size)


def glow(size=256):
    y, x = np.mgrid[0:size, 0:size]
    r = np.hypot(x - size / 2 + .5, y - size / 2 + .5) / (size / 2)
    a = np.clip(1 - r, 0, 1) ** 1.5
    image = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    image.putalpha(Image.fromarray((a * 255).astype(np.uint8)))
    image.save(SPRITES / "Glow.png")


def vignette(size=512):
    y, x = np.mgrid[0:size, 0:size]
    r = np.hypot((x - size / 2) / (size / 2), (y - size / 2) / (size / 2))
    a = np.clip((r - .55) / .9, 0, 1) ** 1.6
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    image.putalpha(Image.fromarray((a * 235).astype(np.uint8)))
    image.save(SPRITES / "Vignette.png")


def scanlines():
    image = Image.new("RGBA", (4, 4), (0, 0, 0, 0))
    for x in range(4):
        image.putpixel((x, 0), (0, 0, 0, 255))
        image.putpixel((x, 1), (0, 0, 0, 120))
    image.save(SPRITES / "Scanlines.png")


def rounded(name, size=96, radius=16, outline=0):
    mask = canvas(size)
    draw = ImageDraw.Draw(mask)
    draw.rounded_rectangle((0, 0, size * AA - 1, size * AA - 1), radius * AA, fill=255)
    if outline:
        draw.rounded_rectangle((outline * AA, outline * AA, (size - outline) * AA - 1, (size - outline) * AA - 1),
                               (radius - outline) * AA, fill=0)
    save(mask, name, size)


def lock(size=128):
    mask = canvas(size)
    draw = ImageDraw.Draw(mask)
    s = AA
    draw.rounded_rectangle((22 * s, 56 * s, 106 * s, 118 * s), 12 * s, fill=255)
    draw.arc((36 * s, 12 * s, 92 * s, 80 * s), 180, 360, fill=255, width=13 * s)
    draw.rectangle((36 * s, 44 * s, 49 * s, 60 * s), fill=255)
    draw.rectangle((79 * s, 44 * s, 92 * s, 60 * s), fill=255)
    draw.ellipse((55 * s, 72 * s, 73 * s, 90 * s), fill=0)
    draw.rectangle((60 * s, 84 * s, 68 * s, 104 * s), fill=0)
    save(mask, "Lock.png", size)


def check(size=128):
    mask = canvas(size)
    ImageDraw.Draw(mask).line([(22 * AA, 66 * AA), (52 * AA, 96 * AA), (108 * AA, 34 * AA)],
                              fill=255, width=18 * AA, joint="curve")
    save(mask, "Check.png", size)


def close(size=128):
    mask = canvas(size)
    draw = ImageDraw.Draw(mask)
    draw.line([(30 * AA, 30 * AA), (98 * AA, 98 * AA)], fill=255, width=14 * AA)
    draw.line([(98 * AA, 30 * AA), (30 * AA, 98 * AA)], fill=255, width=14 * AA)
    save(mask, "Close.png", size)


def gear(size=128, teeth=8):
    mask = canvas(size)
    draw = ImageDraw.Draw(mask)
    c = size * AA / 2
    outer, root, hole = 58 * AA, 44 * AA, 18 * AA
    points = []
    for i in range(teeth * 4):
        angle = (i / (teeth * 4)) * math.tau + math.pi / (teeth * 4)
        radius = outer if i % 4 in (1, 2) else root
        points.append((c + math.cos(angle) * radius, c + math.sin(angle) * radius))
    draw.polygon(points, fill=255)
    draw.ellipse((c - root, c - root, c + root, c + root), fill=255)
    draw.ellipse((c - hole, c - hole, c + hole, c + hole), fill=0)
    save(mask, "Gear.png", size)


def main():
    SPRITES.mkdir(parents=True, exist_ok=True)
    city_map()
    circle()
    ring()
    glow()
    vignette()
    scanlines()
    rounded("Panel.png")
    rounded("PanelOutline.png", outline=4)
    lock()
    check()
    close()
    gear()


if __name__ == "__main__":
    main()
