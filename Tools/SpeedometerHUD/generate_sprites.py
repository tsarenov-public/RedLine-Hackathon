"""Generate the Unity HUD sprites with Pillow. Run from any working directory."""

import argparse
import math
import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


SIZE = 1024
AA = 3
OUTPUT = Path(__file__).resolve().parents[2] / "Assets" / "Cars" / "SpeedometerHUD"


class Layer:
    def __init__(self, width=SIZE, height=SIZE):
        self.image = Image.new("RGBA", (width * AA, height * AA))
        self.draw = ImageDraw.Draw(self.image)

    def line(self, points, color, width=1):
        self.draw.line([(round(x * AA), round(y * AA)) for x, y in points],
                       fill=color, width=round(width * AA))

    def shape(self, points, color):
        self.draw.polygon([(round(x * AA), round(y * AA)) for x, y in points], fill=color)

    def ring(self, radius, color, width):
        self.draw.ellipse(circle_box(radius), outline=color, width=round(width * AA))

    def arc(self, start, stop, radius, color, width):
        steps = math.ceil(abs(stop - start) * 2)
        self.line([polar(start + (stop - start) * i / steps, radius)
                   for i in range(steps + 1)], color, width)

    def label(self, x, y, text, font, color, stroke=0):
        self.draw.text((x * AA, y * AA), text, font=font, fill=color, anchor="mm",
                       stroke_width=stroke * AA, stroke_fill=(7, 12, 18))

    def save(self, name):
        self.image.resize((SIZE, SIZE), Image.Resampling.LANCZOS).save(
            OUTPUT / (name + ".png"), optimize=True)


def circle_box(radius):
    return tuple(round(v * AA) for v in (512 - radius, 512 - radius, 512 + radius, 512 + radius))


def polar(angle, radius):
    radians = math.radians(angle)
    return 512 + math.sin(radians) * radius, 512 - math.cos(radians) * radius


def dial(font_path):
    face = Layer()
    face.draw.ellipse(circle_box(502), fill=(3, 7, 13, 112))
    face.draw.ellipse(circle_box(494), fill=(7, 13, 21, 251))

    carbon = Layer()
    for y in range(0, SIZE, 2):
        light = max(0, 1 - y / 870)
        color = (round(11 + 11 * light), round(17 + 17 * light), round(25 + 22 * light))
        carbon.line([(10, y), (1014, y)], color, 2)
    for y in range(48, 984, 12):
        for x in range(48, 984, 24):
            offset = 12 if (y // 12) % 2 else 0
            light = 2 if (x + y) % 48 == 0 else 0
            carbon.line([(x + offset, y), (x + offset + 10, y - 1)],
                        (21 + light, 30 + light, 42 + light), 2)
            carbon.line([(x + offset + 10, y + 4), (x + offset + 22, y + 4)], (12, 20, 30), 2)
    mask = Image.new("L", face.image.size)
    ImageDraw.Draw(mask).ellipse(circle_box(465), fill=255)
    face.image.paste(carbon.image, (0, 0), mask)

    face.ring(489, (81, 106, 125), 4)
    face.ring(481, (29, 48, 66), 6)
    face.ring(468, (119, 153, 168), 2)
    face.ring(459, (6, 11, 18), 3)
    face.arc(-135, 135, 429, (8, 13, 21), 19)
    face.arc(-135, 135, 430, (77, 117, 136), 3)
    face.arc(-135, -97, 430, (116, 229, 240), 5)
    redline = -135 + 270 * 160 / 200
    face.arc(redline, 135, 430, (93, 31, 43), 18)
    face.arc(redline, 135, 430, (255, 100, 105), 6)
    face.arc(-112, -55, 484, (133, 202, 213), 3)
    face.arc(55, 112, 484, (133, 202, 213), 3)
    for angle in (-143, 143, 180):
        face.line([polar(angle, 470), polar(angle, 484)], (126, 164, 184), 2)

    digits = ImageFont.truetype(str(font_path), 51 * AA)
    units = ImageFont.truetype(str(font_path), 33 * AA)
    for mph in range(0, 201, 10):
        angle = -135 + 270 * mph / 200
        major = mph % 20 == 0
        color = (255, 160, 157) if mph >= 160 else (229, 246, 255)
        face.line([polar(angle, 402), polar(angle, 358 if major else 382)],
                  color if major else (145, 174, 191), 6 if major else 3)
        if major:
            face.label(*polar(angle, 314), str(mph), digits, color, stroke=2)
    face.label(512, 581, "MPH", units, (184, 214, 229), stroke=1)
    face.label(512, 768, "NOS", units, (154, 218, 229), stroke=1)
    face.line([(420, 768), (464, 768)], (67, 114, 137), 2)
    face.line([(560, 768), (604, 768)], (67, 114, 137), 2)
    inset = nitro_outline(432, 62, 3, 7, 7)
    face.shape([(x + 296, y + 796) for x, y in inset], (5, 11, 18))
    face.save("Speedometer_Dial")


def needle():
    layer = Layer()
    layer.shape([(512, 95), (530, 465), (548, 513), (512, 575), (476, 513), (494, 465)],
                (78, 220, 254, 45))
    layer.shape([(512, 103), (522, 411), (532, 495), (512, 545), (492, 495), (502, 411)],
                (9, 36, 47))
    layer.shape([(512, 109), (518, 411), (524, 486), (510, 519), (501, 482), (506, 408)],
                (68, 228, 247))
    layer.line([(512, 128), (512, 431), (513, 477)], (220, 255, 255), 3)
    for radius, color in [(40, (6, 13, 21)), (36, (114, 159, 178)), (32, (19, 35, 49)),
                          (24, (60, 88, 106)), (21, (9, 19, 30)), (13, (83, 124, 144)),
                          (10, (24, 47, 62))]:
        layer.draw.ellipse(circle_box(radius), fill=color)
    layer.arc(-65, 65, 29, (211, 239, 243), 2)
    layer.save("Speedometer_Needle")


def nitro_outline(width, height, inset, pad, chamfer):
    left, right = 4 + inset, width - 4 - inset
    xs = [left + (right - left) * i / 150 for i in range(151)]

    def bow(x):
        return 31 * (1 - (2 * x / width - 1) ** 2)

    top = [(x, pad + bow(x)) for x in xs[2:-2]]
    bottom = [(x, height - 37 + pad + bow(x)) for x in reversed(xs[2:-2])]
    return [(left, pad + bow(left) + chamfer), *top,
            (right, pad + bow(right) + chamfer),
            (right, height - 37 + pad + bow(right) - chamfer), *bottom,
            (left, height - 37 + pad + bow(left) - chamfer)]


def nitro():
    width, height = 432, 62
    bg = Layer(width, height)
    bg.shape(nitro_outline(width, height, 0, 2, 6), (133, 175, 194))
    bg.shape(nitro_outline(width, height - 4, 2, 4, 5), (10, 21, 32))
    bg.shape(nitro_outline(width, height - 11, 6, 7, 4), (28, 48, 64))
    bg.save("Nitro_Bar_Bg")

    fill = Layer(width, height)
    fill.shape(nitro_outline(width, height - 12, 6, 8, 4), (172, 237, 245))
    for x in range(17, 420):
        bend = 31 * (1 - (2 * x / width - 1) ** 2)
        fill.line([(x, 10 + bend), (x, 12 + bend)], (252, 255, 255), 2)
        if x % 24 in (0, 1):
            fill.line([(x, 16 + bend), (x, 20 + bend)], (93, 157, 181), 1)
    fill.save("Nitro_Bar_Fill")


def panel(name, width, height, corner):
    layer = Layer(width, height)
    for inset, color in ((2, (8, 14, 21, 180)), (5, (105, 149, 173, 244)),
                         (7, (11, 22, 33, 247)), (11, (7, 15, 24, 223))):
        x, y = width - inset, height - inset
        c = corner - inset / 2
        layer.shape([(inset + c, inset), (x - c, inset), (x, inset + c), (x, y - c),
                     (x - c, y), (inset + c, y), (inset, y - c), (inset, inset + c)], color)
    layer.line([(corner + 8, 6), (width - corner - 8, 6)], (129, 205, 219), 2)
    layer.save(name)


def main():
    windows_fonts = Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--font", type=Path, default=windows_fonts / "bahnschrift.ttf",
                        help="bold sans-serif TTF for the baked dial labels")
    args = parser.parse_args()
    OUTPUT.mkdir(parents=True, exist_ok=True)
    dial(args.font)
    needle()
    nitro()
    panel("Gear_Panel", 88, 106, 19)
    panel("Digital_Speed_Panel", 230, 106, 20)


if __name__ == "__main__":
    main()
