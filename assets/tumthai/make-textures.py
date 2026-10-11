#!/usr/bin/env python3
"""Make TumThai's logo textures and icons from assets/tumthai/logo.png (needs Pillow).

Run from the repository root: python3 assets/tumthai/make-textures.py
"""
import math
import os

from PIL import Image

SRC = 'assets/tumthai/logo.png'
TEXTURES = 'osu.Game/Resources/Textures'
GREEN = (120, 155, 97)  # the logo's flat circle colour


def square(im):
    """Crop to the logo's circle and centre it on a square canvas."""
    box = im.getchannel('A').getbbox()
    size = max(box[2] - box[0], box[3] - box[1])
    out = Image.new('RGBA', (size, size))
    out.paste(im.crop(box), ((size - (box[2] - box[0])) // 2, (size - (box[3] - box[1])) // 2))
    return out


def unblend(im):
    """Make the flat green circle transparent, keeping the rim, the sticker and its soft shadow."""
    out = im.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if a < 255:
                continue
            alpha = 0.0
            for c, k in zip((r, g, b), GREEN):
                alpha = max(alpha, (c - k) / (255 - k) if c > k else (k - c) / k)
            if alpha <= 0.02:
                px[x, y] = (0, 0, 0, 0)
                continue
            alpha = min(alpha, 1.0)
            px[x, y] = tuple(round(min(255, max(0, k + (c - k) / alpha))) for c, k in zip((r, g, b), GREEN)) + (round(alpha * 255),)
    return out


def fit(im, canvas, diameter):
    out = Image.new('RGBA', (canvas, canvas))
    scaled = im.resize((diameter, diameter), Image.LANCZOS)
    out.alpha_composite(scaled, ((canvas - diameter) // 2, (canvas - diameter) // 2))
    return out


def intro(keyed, canvas):
    """The triangles intro's outline: green = how opaque, red = when it's drawn (a clockwise sweep)."""
    logo = keyed.resize((canvas, canvas), Image.LANCZOS)
    out = Image.new('RGB', (canvas, canvas), (255, 0, 0))
    src, px = logo.load(), out.load()
    c = (canvas - 1) / 2
    for y in range(canvas):
        for x in range(canvas):
            r, g, b, a = src[x, y]
            white = max(0, min(r, g, b) - 200) / 55 * a / 255
            if white <= 0:
                continue
            sweep = (math.atan2(x - c, c - y) / (2 * math.pi)) % 1
            px[x, y] = (5 + round(sweep * 230), round(white * 255), 0)
    return out


def save(im, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)
    print(path)


full = square(Image.open(SRC).convert('RGBA'))
keyed = unblend(full)

# Main menu logo: the triangles and colour are drawn behind it, so the circle is transparent.
# The rim sits where the old logo's ring did (970 of 1024).
save(fit(keyed, 1024, 970), f'{TEXTURES}/Menu/logo.png')
save(fit(full, 90, 86), f'{TEXTURES}/Icons/Logo.png')
save(intro(keyed, 1295), f'{TEXTURES}/Intro/Triangles/logo-highlight.png')
save(intro(keyed, 1295), f'{TEXTURES}/Intro/Triangles/logo-background.png')

# App icons (Windows exe and window, AppImage, installer).
save(fit(full, 1024, 1024), 'assets/lazer.png')
fit(full, 256, 256).save('osu.Desktop/lazer.ico', sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print('osu.Desktop/lazer.ico')
