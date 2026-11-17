#!/usr/bin/env python3
"""Draws the app icon: a green gas cloud with speed lines on the goal's orange.

    python3 make_icon.py ../../Assets/Icon.png

Original artwork, generated; needs Pillow. The picture stays inside the middle
two thirds of the square so Android's round and squircle masks never cut it.
"""
import sys
from PIL import Image, ImageDraw, ImageFilter

SIZE = 512
S = 4                      # supersampling
N = SIZE * S


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def disc(draw, cx, cy, r, fill):
    draw.ellipse([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], fill=fill)


def main(path):
    # background: the goal's orange, lighter at the top
    top, bottom = (255, 158, 40), (226, 86, 0)
    img = Image.new("RGB", (N, N))
    px = ImageDraw.Draw(img)
    for y in range(N):
        px.line([(0, y), (N, y)], fill=lerp(top, bottom, y / (N - 1)))

    # the cloud: overlapping puffs (x, y, radius), drifting to the right
    puffs = [(214, 286, 62), (268, 232, 70), (330, 262, 60), (300, 310, 58), (244, 322, 46), (352, 312, 40)]

    # soft shadow under it
    shadow = Image.new("L", (N, N), 0)
    sd = ImageDraw.Draw(shadow)
    for x, y, r in puffs:
        disc(sd, x + 6, y + 16, r, 120)
    shadow = shadow.filter(ImageFilter.GaussianBlur(10 * S))
    img.paste((120, 40, 0), (0, 0), shadow)

    d = ImageDraw.Draw(img)
    for x, y, r in puffs:                     # dark rim
        disc(d, x, y, r + 7, (52, 110, 14))
    for x, y, r in puffs:                     # body
        disc(d, x, y, r, (126, 222, 48))
    for x, y, r in puffs:                     # lit from the top left
        disc(d, x - r * 0.18, y - r * 0.2, r * 0.68, (170, 240, 92))
    for x, y, r in [(250, 208, 22), (198, 262, 15), (318, 240, 13)]:
        disc(d, x, y, r, (222, 252, 170))

    # speed lines trailing to the left: it is a race
    for y, x0, x1, w in [(238, 96, 150, 12), (286, 84, 132, 12), (334, 104, 168, 12)]:
        d.rounded_rectangle([x0 * S, (y - w / 2) * S, x1 * S, (y + w / 2) * S], radius=w / 2 * S, fill=(255, 240, 214))

    img.resize((SIZE, SIZE), Image.LANCZOS).save(path)
    print("wrote", path)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Icon.png")
