"""Generate the original paper atlas and peripheral wind-streak sprite with Pillow."""
import math
import random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2] / "Assets/Cars"
AA = 3
TILE = 512
SHAPES = (
    [(99, 61), (418, 79), (399, 408), (352, 440), (92, 419), (79, 369)],
    [(105, 83), (409, 67), (431, 308), (385, 419), (140, 445), (96, 375)],
    [(116, 94), (382, 54), (412, 171), (403, 431), (142, 411), (110, 354)],
    [(69, 109), (426, 74), (420, 372), (316, 427), (107, 407), (80, 276)],
)


def scaled(points):
    return [(x * AA, y * AA) for x, y in points]


def paper_atlas():
    page = Image.new("RGBA", (TILE * 2 * AA, TILE * 2 * AA))
    rng = random.Random(2708)
    colors = [(225, 215, 187), (242, 224, 172), (226, 231, 223), (228, 203, 175)]
    font = ImageFont.load_default(size=39 * AA)
    for i, outline in enumerate(SHAPES):
        mask = Image.new("L", (TILE * AA, TILE * AA))
        ImageDraw.Draw(mask).polygon(scaled(outline), fill=255)
        tile = Image.new("RGBA", mask.size, colors[i] + (255,))
        draw = ImageDraw.Draw(tile)
        draw.polygon(scaled([(0, 223), (273, 321), (490, 192), (512, 530), (0, 530)]),
                     fill=tuple(v - 19 for v in colors[i]) + (255,))
        draw.polygon(scaled([(383, 438), (340, 374), (432, 350)]), fill=(251, 246, 227, 255))
        draw.line(scaled([(75, 252), (276, 311), (423, 265)]), fill=(255, 250, 224, 255), width=4 * AA)
        draw.line(scaled([(168, 80), (212, 250), (192, 432)]), fill=(187, 178, 153, 255), width=4 * AA)
        for _ in range(330):
            x, y = rng.randrange(TILE), rng.randrange(TILE)
            shade = rng.randrange(149, 193)
            draw.rectangle((x * AA, y * AA, (x + 2) * AA, (y + 1) * AA),
                           fill=(shade, shade - 4, shade - 11, 255))
        ink = ((77, 78, 69), (141, 72, 45), (57, 75, 79), (105, 58, 47))[i]
        draw.text((125 * AA, 120 * AA), ("CITY", "MARKET", "METRO", "NIGHT")[i],
                  font=font, fill=ink + (255,))
        draw.line(scaled([(118, 169), (380, 166)]), fill=ink + (255,), width=5 * AA)
        if i % 2:
            draw.polygon(scaled([(126, 204), (378, 184), (355, 302), (147, 321)]), fill=ink + (255,))
            draw.line(scaled([(153, 286), (349, 223)]), fill=(241, 185, 110, 255), width=11 * AA)
        else:
            for x in (127, 264):
                for y in range(194, 370, 14):
                    draw.line(scaled([(x, y), (x + rng.randrange(74, 123), y - 2)]),
                              width=4 * AA, fill=ink + (255,))
        tile.putalpha(mask)
        page.paste(tile, ((i % 2) * TILE * AA, (i // 2) * TILE * AA))
    page.resize((2 * TILE, 2 * TILE), Image.Resampling.LANCZOS).save(
        ROOT / "Effects/SpeedDebris/Speed_Debris_Paper.png", optimize=True)


def speed_line():
    side = 128
    line = Image.new("RGBA", (side, side))
    for y in range(side):
        for x in range(side):
            u = (x + .5) / side * 2 - 1
            v = (y + .5) / side * 2 - 1
            width = .64 * (1 - abs(v) ** 1.8)
            opacity = max(0, 1 - (abs(u) / max(.001, width)) ** 2) * math.cos(v * math.pi * .5)
            line.putpixel((x, y), (227, 237, 248, round(255 * opacity)))
    line.save(ROOT / "Camera/Speed_Line.png", optimize=True)


if __name__ == "__main__":
    paper_atlas()
    speed_line()
