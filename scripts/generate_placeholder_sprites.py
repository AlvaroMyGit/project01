#!/usr/bin/env python3
"""Generate procedural 32x32 placeholder sprites for the visualizer (no rembg required)."""
import os
from PIL import Image, ImageDraw

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "visualizer", "assets")

FACTIONS = {
    "stalker_loner":     (161, 161, 170),
    "stalker_duty":      (239, 68, 68),
    "stalker_freedom":   (34, 197, 94),
    "stalker_bandit":    (120, 53, 15),
    "stalker_mercenary": (59, 130, 246),
    "stalker_clearsky":  (6, 182, 212),
    "stalker_monolith":  (243, 244, 246),
    "stalker_ecologist": (251, 191, 36),
    "stalker_zombified": (124, 58, 237),
    "stalker_military":  (132, 204, 22),
    "mutant_default":    (217, 70, 239),
    "corpse":            (80, 20, 20),
    "paperdoll_weapon":  (200, 160, 60),
    "paperdoll_armor":   (100, 120, 140),
}


def draw_stalker(name, body_rgb):
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r, g, b = body_rgb
    # Head
    d.ellipse((12, 4, 20, 12), fill=(220, 190, 160, 255))
    # Body
    d.rectangle((11, 12, 21, 24), fill=(r, g, b, 255))
    # Legs
    d.rectangle((12, 24, 15, 30), fill=(60, 60, 60, 255))
    d.rectangle((17, 24, 20, 30), fill=(60, 60, 60, 255))
    # Outline
    d.rectangle((10, 3, 22, 30), outline=(0, 0, 0, 180))
    img.save(os.path.join(OUT_DIR, f"{name}.png"))


def draw_mutant(name, body_rgb):
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r, g, b = body_rgb
    d.polygon([(16, 2), (28, 16), (16, 30), (4, 16)], fill=(r, g, b, 255), outline=(0, 0, 0, 200))
    d.ellipse((12, 10, 16, 14), fill=(255, 60, 60, 255))
    d.ellipse((18, 10, 22, 14), fill=(255, 60, 60, 255))
    img.save(os.path.join(OUT_DIR, f"{name}.png"))


def draw_corpse(name, body_rgb):
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r, g, b = body_rgb
    d.ellipse((6, 14, 26, 28), fill=(r, g, b, 220))
    d.line((8, 18, 24, 22), fill=(120, 30, 30, 255), width=2)
    img.save(os.path.join(OUT_DIR, f"{name}.png"))


def draw_icon(name, body_rgb):
    img = Image.new("RGBA", (8, 8), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r, g, b = body_rgb
    d.rectangle((0, 0, 7, 7), fill=(r, g, b, 255), outline=(0, 0, 0, 255))
    img.save(os.path.join(OUT_DIR, f"{name}.png"))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    for name, rgb in FACTIONS.items():
        if name.startswith("stalker_"):
            draw_stalker(name, rgb)
        elif name == "mutant_default":
            draw_mutant(name, rgb)
        elif name == "corpse":
            draw_corpse(name, rgb)
        else:
            draw_icon(name, rgb)
    print(f"Generated {len(FACTIONS)} sprites in {OUT_DIR}")


if __name__ == "__main__":
    main()
