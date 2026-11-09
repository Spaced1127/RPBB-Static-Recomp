from PIL import Image, ImageDraw
import math, os

ROOT = r"H:\Projects\RPBB-GCN-Recomp"
COVER = os.path.join(ROOT, "work", "cover-front.png")
OUT = os.path.join(ROOT, "assets")
os.makedirs(OUT, exist_ok=True)


def rounded_mask(size, radius):
    mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (0, 0, size - 1, size - 1), radius=radius, fill=255)
    return mask


def cover_square():
    cover = Image.open(COVER).convert("RGBA")
    # Drop the GameCube banner and THQ/rating strip; keep the logo and characters.
    return cover.crop((0, 118, 800, 918))


def gear_badge(img):
    draw = ImageDraw.Draw(img, "RGBA")
    size = img.size[0]
    gx, gy = int(size * 0.81), int(size * 0.81)
    gr = int(size * 0.105)

    draw.ellipse((gx - int(gr * 1.22), gy - int(gr * 1.22),
                  gx + int(gr * 1.22), gy + int(gr * 1.22)),
                 fill=(4, 20, 28, 250), outline=(245, 195, 30, 235),
                 width=max(2, size // 220))

    for i in range(8):
        angle = i * math.pi / 4.0
        tx = gx + math.cos(angle) * gr * 1.06
        ty = gy + math.sin(angle) * gr * 1.06
        tw, th = int(gr * 0.44), int(gr * 0.88)
        tooth = Image.new("RGBA", (tw * 2, th * 2), (0, 0, 0, 0))
        td = ImageDraw.Draw(tooth)
        td.rounded_rectangle((tw // 2, 0, tw + tw // 2, th * 2),
                             radius=tw // 3, fill=(245, 195, 30, 255))
        tooth = tooth.rotate(-math.degrees(angle), resample=Image.BICUBIC)
        img.alpha_composite(tooth, (int(tx - tw), int(ty - th)))

    draw = ImageDraw.Draw(img, "RGBA")
    draw.ellipse((gx - gr, gy - gr, gx + gr, gy + gr),
                 fill=(245, 195, 30, 255))
    draw.ellipse((gx - int(gr * 0.42), gy - int(gr * 0.42),
                  gx + int(gr * 0.42), gy + int(gr * 0.42)),
                 fill=(6, 26, 34, 255))
    return img


def make(path, launcher=False):
    size = 800
    square = cover_square().resize((size, size), Image.LANCZOS)

    # A thin dark rim and rounded corners keep the cover readable on light docks.
    mask = rounded_mask(size, 92)
    framed = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    framed.paste(square, (0, 0), mask)
    draw = ImageDraw.Draw(framed, "RGBA")
    draw.rounded_rectangle((2, 2, size - 3, size - 3), radius=90,
                           outline=(4, 20, 28, 220), width=8)

    if launcher:
        framed = gear_badge(framed)

    big = framed.resize((1024, 1024), Image.LANCZOS)
    png = path.replace(".ico", ".png")
    big.save(png, "PNG")

    sizes = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64),
             (128, 128), (256, 256)]
    framed.save(path, format="ICO", sizes=sizes)
    print(path, os.path.getsize(path))


make(os.path.join(OUT, "rpbb.ico"), launcher=False)
make(os.path.join(OUT, "rpbb-launcher.ico"), launcher=True)
