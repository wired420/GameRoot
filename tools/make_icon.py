"""Renders the GameRoot shield icon at each Windows icon size and writes a multi-size .ico."""
import sys
from PIL import Image, ImageDraw, ImageFilter

AMBER = (232, 178, 30, 255)
RED = (255, 48, 40, 255)
SS = 8  # supersampling factor


def bez(p0, p1, p2, p3, n=40):
    pts = []
    for i in range(n + 1):
        t = i / n
        mt = 1 - t
        x = mt**3 * p0[0] + 3 * mt * mt * t * p1[0] + 3 * mt * t * t * p2[0] + t**3 * p3[0]
        y = mt**3 * p0[1] + 3 * mt * mt * t * p1[1] + 3 * mt * t * t * p2[1] + t**3 * p3[1]
        pts.append((x, y))
    return pts


def shield_points():
    # Same outline as Logo.cs, in its 100 x 100 design space.
    pts = [(50, 6), (86, 16), (86, 48)]
    pts += bez((86, 48), (86, 70), (70, 86), (50, 96))[1:]
    pts += bez((50, 96), (30, 86), (14, 70), (14, 48))[1:]
    pts += [(14, 16)]
    return pts


def render(size):
    big = size * SS
    # Fit the shield's bounding box (x 14..86, y 6..96) into the square with a small margin.
    pad = 0.04 if size >= 32 else 0.02
    scale = big * (1 - 2 * pad) / 90.0
    ox = big / 2 - 50 * scale
    oy = big * pad - 6 * scale

    def P(x, y):
        return (ox + x * scale, oy + y * scale)

    poly = [P(x, y) for x, y in shield_points()]

    img = Image.new("RGBA", (big, big), (0, 0, 0, 0))

    # Steel gradient fill, masked to the shield.
    grad = Image.new("RGBA", (big, big))
    gd = ImageDraw.Draw(grad)
    top, bottom = (84, 89, 96), (30, 32, 36)
    for y in range(big):
        t = y / max(1, big - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        gd.line([(0, y), (big, y)], fill=c)
    mask = Image.new("L", (big, big), 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    img.paste(grad, (0, 0), mask)

    d = ImageDraw.Draw(img)

    # Circuit traces with solder pads, only where there is room to see them.
    if size >= 48:
        traces = [
            [(14, 30), (24, 30), (30, 36)],
            [(86, 30), (76, 30), (70, 36)],
            [(22, 70), (30, 70), (36, 64)],
            [(78, 70), (70, 70), (64, 64)],
            [(50, 92), (50, 72)],
            [(36, 46), (50, 46), (50, 58)],
            [(64, 46), (58, 52)],
        ]
        tw = max(1, int(2.0 * scale))
        r = 2.6 * scale
        trace_layer = Image.new("RGBA", (big, big), (0, 0, 0, 0))
        td = ImageDraw.Draw(trace_layer)
        for t in traces:
            pts = [P(x, y) for x, y in t]
            td.line(pts, fill=(232, 178, 30, 210), width=tw, joint="curve")
            ex, ey = pts[-1]
            td.ellipse([ex - r, ey - r, ex + r, ey + r], fill=AMBER)
        img.paste(trace_layer, (0, 0), Image.composite(trace_layer, Image.new("RGBA", (big, big)), mask))

    # Amber rim. Thicker at small sizes so it stays visible.
    rim = (3.2 if size >= 48 else 6.0 if size >= 24 else 8.0) * scale
    d.line(poly + [poly[0]], fill=AMBER, width=max(1, int(rim)), joint="curve")

    # Indicator light with a soft glow; larger relative size at small icon sizes.
    lr = (5.5 if size >= 48 else 8.5 if size >= 24 else 11) * scale
    cx, cy = P(50, 34 if size >= 48 else 38)
    glow = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse([cx - lr * 2, cy - lr * 2, cx + lr * 2, cy + lr * 2], fill=(255, 48, 40, 150))
    glow = glow.filter(ImageFilter.GaussianBlur(lr * 0.8))
    img = Image.alpha_composite(img, Image.composite(glow, Image.new("RGBA", (big, big)), mask))
    d = ImageDraw.Draw(img)
    d.ellipse([cx - lr, cy - lr, cx + lr, cy + lr], fill=RED)
    if size >= 24:
        d.ellipse([cx - lr * 0.55, cy - lr * 0.7, cx + lr * 0.15, cy - lr * 0.1], fill=(255, 255, 255, 150))

    return img.resize((size, size), Image.LANCZOS)


def main(out_ico, out_png):
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    images = {s: render(s) for s in sizes}
    images[256].save(out_ico, format="ICO", sizes=[(s, s) for s in sizes],
                     append_images=[images[s] for s in sizes if s != 256])
    # Preview strip for checking by eye.
    strip = Image.new("RGBA", (sum(sizes) + 10 * len(sizes), 256), (24, 26, 29, 255))
    x = 0
    for s in sizes:
        strip.paste(images[s], (x, 256 - s), images[s])
        x += s + 10
    strip.save(out_png)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
