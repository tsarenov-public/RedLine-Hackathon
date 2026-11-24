"""Bake the original Diablo #97 race vinyls with Pillow."""
import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

AA = 3
OUTPUT = Path(__file__).resolve().parents[2] / "Assets/Cars/race cars/lamborgini/Decals"
IVORY = "#f4ead5"
GRAPHITE = "#1f2329"
ORANGE = "#fa6433"
COPPER = "#b5311f"
SLATE = "#433c3b"


def canvas(width, height):
    image = Image.new("RGBA", (width * AA, height * AA))
    return image, ImageDraw.Draw(image)


def poly(draw, color, *points):
    draw.polygon([(int(x * AA), int(y * AA)) for x, y in points], fill=color)


def save(image, width, height, filename):
    image.resize((width, height), Image.Resampling.LANCZOS).save(OUTPUT / filename, optimize=True)


def hood():
    width = height = 2048
    image, draw = canvas(width, height)
    poly(draw, GRAPHITE, (547, 93), (958, 93), (945, 1889), (855, 1983), (434, 1983))
    poly(draw, GRAPHITE, (1090, 93), (1501, 93), (1614, 1983), (1194, 1983), (1102, 1889))
    poly(draw, COPPER, (569, 100), (700, 100), (596, 1982), (480, 1982))
    poly(draw, ORANGE, (577, 100), (631, 100), (539, 1982), (490, 1982))
    poly(draw, IVORY, (710, 100), (744, 100), (665, 1982), (634, 1982))
    poly(draw, COPPER, (1348, 100), (1479, 100), (1568, 1982), (1452, 1982))
    poly(draw, ORANGE, (1417, 100), (1471, 100), (1558, 1982), (1509, 1982))
    poly(draw, IVORY, (1304, 100), (1338, 100), (1414, 1982), (1383, 1982))
    poly(draw, IVORY, (815, 1221), (939, 1305), (938, 1381), (807, 1293))
    poly(draw, IVORY, (1233, 1221), (1109, 1305), (1110, 1381), (1241, 1293))
    save(image, width, height, "Diablo_Hood.png")


def fender(font):
    width, height = 2048, 1024
    image, draw = canvas(width, height)
    poly(draw, GRAPHITE, (63, 691), (237, 433), (1239, 328), (1551, 103), (1929, 100),
         (1960, 216), (1425, 647), (382, 786))
    poly(draw, SLATE, (191, 709), (382, 648), (1432, 518), (1870, 160), (1950, 131),
         (1950, 204), (1413, 628), (382, 756))
    poly(draw, COPPER, (108, 677), (245, 538), (399, 505), (337, 630), (128, 709))
    poly(draw, ORANGE, (138, 668), (241, 555), (363, 523), (313, 572), (184, 665))
    poly(draw, ORANGE, (1445, 397), (1607, 247), (1904, 123), (1924, 151), (1656, 324), (1468, 487))
    poly(draw, IVORY, (279, 734), (1419, 592), (1797, 288), (1802, 314), (1436, 625), (325, 773))
    poly(draw, ORANGE, (392, 811), (1047, 730), (1092, 750), (449, 839))
    poly(draw, COPPER, (483, 855), (1006, 787), (1029, 799), (529, 871))
    poly(draw, IVORY, (698, 364), (1129, 331), (1161, 384), (1088, 639), (658, 693), (629, 635))
    poly(draw, ORANGE, (670, 646), (1093, 594), (1081, 628), (660, 680))
    digits = ImageFont.truetype(str(font), 220 * AA)
    draw.text((893 * AA, 498 * AA), "97", fill=GRAPHITE, font=digits, anchor="mm",
              stroke_width=2 * AA, stroke_fill=GRAPHITE)
    for x in (1190, 1235, 1280):
        poly(draw, IVORY, (x, 491), (x + 28, 485), (x + 65, 434), (x + 36, 439))
    save(image, width, height, "Diablo_Quarter_97.png")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font", type=Path, default=Path("C:/Windows/Fonts/bahnschrift.ttf"))
    OUTPUT.mkdir(parents=True, exist_ok=True)
    hood()
    fender(parser.parse_args().font)
