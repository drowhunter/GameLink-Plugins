"""Generate original GameLink plugin artwork: logo.png (220x175), recent.png (100x100), wide.png (1087x287).

Usage:
  python generate_images.py --out MyPlugin/Resources --title "RUSH OF BLOOD" --subtitle "UNTIL DAWN" \
      --short ROB --theme coaster --accent 200,10,10 --glow 110,10,14 --drips

Requires Pillow (pip install pillow). Produces procedural art only; never embeds official game assets.
"""

from __future__ import annotations

import argparse
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

SIZES = {"wide": (1087, 287), "logo": (220, 175), "recent": (100, 100)}


def rgb(s: str) -> tuple[int, int, int]:
    r, g, b = (int(v) for v in s.split(","))
    return r, g, b


def font(size: int) -> ImageFont.ImageFont:
    for name in ("impact.ttf", "Impact.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    return ImageFont.load_default()


def background(w: int, h: int, glow: tuple[int, int, int]) -> Image.Image:
    im = Image.new("RGB", (w, h))
    px = im.load()
    cx, cy = w * 0.5, h * 0.55
    md = math.hypot(cx, cy)
    for y in range(h):
        for x in range(w):
            v = max(0.0, 1 - math.hypot(x - cx, y - cy) / md) ** 2
            px[x, y] = tuple(int(12 + c * v) for c in glow)
    return im


def moon(d: ImageDraw.ImageDraw, x: float, y: float, r: float, glow) -> None:
    for i in range(6, 0, -1):
        k = 0.4 + i * 0.08
        d.ellipse((x - r - i * 4, y - r - i * 4, x + r + i * 4, y + r + i * 4),
                  fill=tuple(int(c * k) for c in glow))
    d.ellipse((x - r, y - r, x + r, y + r), fill=(230, 210, 200))


def wave(w: int, amp: float, base: float, n: int, phase: float) -> list[tuple[float, float]]:
    return [(w * i / n, base + amp * math.sin(i / n * math.pi * 2.2 + phase)
             + amp * 0.4 * math.sin(i / n * math.pi * 5)) for i in range(n + 1)]


def draw_coaster(d, w, h, s, accent):
    pts = wave(w, 45 * s, h * 0.70, max(40, int(120 * s)), 0.6)
    width = max(5, int(14 * s))
    rail = (accent[0] // 3 + 40, accent[1] // 3 + 30, accent[2] // 3 + 30)
    for off in (-width // 2, width // 2):
        d.line([(x, y + off) for x, y in pts], fill=rail, width=max(2, width // 5))
    for i in range(0, len(pts), 3):
        x, y = pts[i]
        d.line((x, y - width // 2, x, y + width // 2), fill=rail, width=max(1, width // 7))
    for i in range(0, len(pts), max(1, len(pts) // 10)):
        x, y = pts[i]
        d.line((x, y + width // 2, x, h), fill=rail, width=max(2, width // 5))
    i = int(len(pts) * 0.65)
    (x, y), (x2, y2) = pts[i], pts[i + 1]
    a, sz = math.atan2(y2 - y, x2 - x), 26 * s
    c, sn = math.cos(a), math.sin(a)
    y -= width
    rot = lambda px, py: (x + px * c - py * sn, y + px * sn + py * c)
    d.polygon([rot(-sz, -sz * 0.55), rot(sz * 0.9, -sz * 0.55), rot(sz, 0), rot(-sz, 0)], fill=accent)


def draw_road(d, w, h, s, accent):
    hy = h * 0.55
    d.polygon([(w * 0.45, hy), (w * 0.55, hy), (w * 0.95, h), (w * 0.05, h)], fill=(25, 25, 30))
    for i in range(8):
        t0, t1 = (i / 8) ** 2, ((i + 0.5) / 8) ** 2
        y0, y1 = hy + (h - hy) * t0, hy + (h - hy) * t1
        hw0, hw1 = 1 + 6 * s * t0, 1 + 6 * s * t1
        d.polygon([(w / 2 - hw0, y0), (w / 2 + hw0, y0), (w / 2 + hw1, y1), (w / 2 - hw1, y1)], fill=accent)


def draw_sky(d, w, h, s, accent):
    pts = wave(w, 20 * s, h * 0.85, 60, 1.0)
    d.polygon(pts + [(w, h), (0, h)], fill=(15, 15, 20))
    cx, cy, sz = w * 0.7, h * 0.45, 30 * s
    d.polygon([(cx + sz, cy), (cx - sz, cy - sz * 0.25), (cx - sz * 0.6, cy), (cx - sz, cy + sz * 0.25)], fill=accent)
    d.polygon([(cx, cy), (cx - sz * 0.4, cy - sz * 0.9), (cx - sz * 0.6, cy)], fill=accent)
    d.polygon([(cx, cy), (cx - sz * 0.4, cy + sz * 0.9), (cx - sz * 0.6, cy)], fill=accent)


THEMES = {"coaster": draw_coaster, "road": draw_road, "sky": draw_sky}


def drips(d, w, n, maxlen, col, band):
    d.rectangle((0, 0, w, band), fill=col)
    for _ in range(n):
        x, L = random.randint(0, w), random.randint(maxlen // 4, maxlen)
        r = random.randint(2, max(3, maxlen // 8))
        d.rectangle((x - r // 2, band, x + r // 2, band + L), fill=col)
        d.ellipse((x - r, band + L - r, x + r, band + L + r), fill=col)


def text(d, txt, cx, y, f, fill):
    tw = d.textlength(txt, font=f)
    d.text((cx - tw / 2 + 3, y + 3), txt, font=f, fill=(0, 0, 0))
    d.text((cx - tw / 2, y), txt, font=f, fill=fill)


def fit(d, txt, max_w, start):
    size = start
    while size > 8 and d.textlength(txt, font=font(size)) > max_w:
        size -= 2
    return font(size)


def render(kind, args):
    w, h = SIZES[kind]
    s = {"wide": 1.0, "logo": 0.35, "recent": 0.2}[kind]
    accent, glow, light = rgb(args.accent), rgb(args.glow), (220, 210, 200)
    im = background(w, h, glow)
    d = ImageDraw.Draw(im)
    if not args.no_moon:
        moon(d, w * 0.81, h * 0.31, max(8, 48 * s * 1.4), glow)
    THEMES[args.theme](d, w, h, s, accent)
    im = im.filter(ImageFilter.GaussianBlur(0.5))
    d = ImageDraw.Draw(im)
    if args.drips:
        drips(d, w, int(70 * max(s, 0.2) * 1.4), max(10, int(40 * s * 1.3)), accent, max(3, int(8 * s * 1.5)))

    if kind == "wide":
        cx = w * 0.36
        if args.subtitle:
            text(d, args.subtitle, cx, 60, fit(d, args.subtitle, w * 0.6, 40), light)
        text(d, args.title, cx, 100, fit(d, args.title, w * 0.66, 78), accent)
    elif kind == "logo":
        y = 30
        if args.subtitle:
            text(d, args.subtitle, w / 2, y, fit(d, args.subtitle, w * 0.85, 18), light)
            y += 24
        words = args.title.split()
        lines = [" ".join(words[: len(words) // 2 + len(words) % 2]), " ".join(words[len(words) // 2 + len(words) % 2:])] if len(words) > 1 else words
        for line in filter(None, lines):
            f = fit(d, line, w * 0.9, 30)
            text(d, line, w / 2, y, f, accent)
            y += f.size + 2
    else:
        short = args.short or "".join(word[0] for word in args.title.split())[:4]
        text(d, short, w / 2, 34, fit(d, short, w * 0.85, 30), accent)

    path = os.path.join(args.out, f"{kind}.png")
    im.save(path)
    print("wrote", path, im.size)


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--out", required=True, help="Resources folder")
    p.add_argument("--title", required=True, help="Main title (big text)")
    p.add_argument("--subtitle", default="", help="Small text above the title")
    p.add_argument("--short", default="", help="Text for the 100x100 icon (default: initials)")
    p.add_argument("--theme", choices=THEMES, default="coaster")
    p.add_argument("--accent", default="200,10,10", help="Title / vehicle color R,G,B")
    p.add_argument("--glow", default="110,10,14", help="Background glow color R,G,B")
    p.add_argument("--drips", action="store_true", help="Horror-style drips along the top")
    p.add_argument("--no-moon", action="store_true")
    p.add_argument("--seed", type=int, default=7)
    args = p.parse_args()
    random.seed(args.seed)
    os.makedirs(args.out, exist_ok=True)
    for kind in SIZES:
        render(kind, args)


if __name__ == "__main__":
    main()
