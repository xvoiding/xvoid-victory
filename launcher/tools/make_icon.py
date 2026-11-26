"""Draws the launcher's icon: a two-tone x on a dark tile, as a multi-size .ico.

    python launcher/tools/make_icon.py

Writes launcher/src/Xvoid.Launcher/Assets/xvoid.ico. Needs Pillow and numpy. Each size is drawn
on its own (heavier strokes when small) instead of shrinking one picture, so 16 px stays legible.
The rail mark in MainWindow.xaml is the same drawing; change both together.
"""
import io
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

A = (0xFF, 0x3D, 0x81)  # AccentA in Theme.xaml
B = (0xFF, 0x9A, 0x3C)  # AccentB
TILE = (0x0E, 0x0E, 0x15, 255)
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]
OUT = Path(__file__).resolve().parents[1] / "src" / "Xvoid.Launcher" / "Assets" / "xvoid.ico"


def stroke_mask(n: int, p0, p1, width: float) -> Image.Image:
    """A line with round ends, as an L mask."""
    m = Image.new("L", (n, n), 0)
    d = ImageDraw.Draw(m)
    d.line([p0, p1], fill=255, width=round(width))
    r = width / 2
    for cx, cy in (p0, p1):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    return m


def render(size: int, supersample: int = 8) -> Image.Image:
    small = size <= 32
    n = size * supersample
    pad = 0.035 * n
    radius = 0.22 * n

    # Dark tile with a faint rim so it holds its edge on a dark taskbar.
    tile = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    outer = Image.new("L", (n, n), 0)
    ImageDraw.Draw(outer).rounded_rectangle([pad, pad, n - 1 - pad, n - 1 - pad], radius=radius, fill=255)
    rim = 0.012 * n
    inner = Image.new("L", (n, n), 0)
    ImageDraw.Draw(inner).rounded_rectangle([pad + rim, pad + rim, n - 1 - pad - rim, n - 1 - pad - rim],
                                            radius=radius - rim, fill=255)
    tile.paste((255, 255, 255, 56), (0, 0), outer)
    tile.paste(TILE, (0, 0), inner)

    # Sunset gradient across the whole tile, used for the "\" stroke.
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    t = ((xx + yy) / (2 * (n - 1)))[..., None]
    rgb = np.array(A, np.float32) * (1 - t) + np.array(B, np.float32) * t
    gradient = Image.fromarray(rgb.astype(np.uint8), "RGB").convert("RGBA")

    lo, hi = (0.29, 0.71) if not small else (0.27, 0.73)
    width = (0.115 if not small else 0.15) * n
    gap = (0.055 if not small else 0.04) * n

    back = stroke_mask(n, (lo * n, lo * n), (hi * n, hi * n), width)            # \  gradient, underneath
    front = stroke_mask(n, (hi * n, lo * n), (lo * n, hi * n), width)           # /  white, on top
    halo = stroke_mask(n, (hi * n, lo * n), (lo * n, hi * n), width + 2 * gap)  # cut where / crosses \

    tile.paste(gradient, (0, 0), back)
    tile.paste(TILE, (0, 0), Image.composite(halo, Image.new("L", (n, n), 0), back))
    tile.paste((255, 255, 255, 255), (0, 0), front)

    return tile.resize((size, size), Image.LANCZOS)


def dib(f: Image.Image) -> bytes:
    """A 32-bit BGRA bitmap in the form an .ico entry wants: bottom-up rows, then an (unused) AND mask."""
    w, h = f.size
    raw = f.convert("RGBA").tobytes("raw", "BGRA")
    rows = [raw[i * w * 4:(i + 1) * w * 4] for i in range(h)]
    pixels = b"".join(reversed(rows))
    mask = bytes(((w + 31) // 32) * 4 * h)
    header = struct.pack("<IiiHHIIiiII", 40, w, h * 2, 1, 32, 0, len(pixels) + len(mask), 0, 0, 0, 0)
    return header + pixels + mask


def write_ico(frames: list[Image.Image], path: Path) -> None:
    pngs = []
    for f in frames:
        if f.width < 256:
            pngs.append(dib(f))
            continue
        buf = io.BytesIO()
        f.save(buf, format="PNG", optimize=True)
        pngs.append(buf.getvalue())
    head = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    entries, body = b"", b""
    for f, data in zip(frames, pngs):
        w = f.width if f.width < 256 else 0
        entries += struct.pack("<BBBBHHII", w, w, 0, 0, 1, 32, len(data), offset + len(body))
        body += data
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(head + entries + body)


if __name__ == "__main__":
    frames = [render(s) for s in SIZES]
    write_ico(frames, OUT)
    print(f"wrote {OUT} ({OUT.stat().st_size} bytes, sizes {SIZES})")
