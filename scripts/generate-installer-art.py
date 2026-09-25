#!/usr/bin/env python3
"""Paint the Velopack splash and MSI bitmaps. Stdlib only.

Setup.exe can show one PNG. The MSI can show a 493x58 banner and a
493x312 dialog background. Those are the only images the installer
will draw. Output goes to installer/generated/ and is not committed.
"""

from __future__ import annotations

import argparse
import struct
import zlib
from pathlib import Path

W, H = 720, 440
INDIGO = (76, 29, 149)
VIOLET = (124, 58, 237)
CYAN = (34, 211, 238)
AMBER = (245, 158, 11)
WHITE = (255, 255, 255)
INK = (30, 27, 75)
WASH = (247, 245, 255)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def blend(dst, src, alpha):
    a = alpha / 255
    return tuple(int(dst[i] * (1 - a) + src[i] * a) for i in range(3))


def rounded_mask(w, h, r, x0, y0, x1, y1):
    mask = [[0] * w for _ in range(h)]
    for y in range(max(0, y0), min(h, y1)):
        for x in range(max(0, x0), min(w, x1)):
            dx = 0
            dy = 0
            if x < x0 + r and y < y0 + r:
                dx, dy = x0 + r - x, y0 + r - y
            elif x >= x1 - r and y < y0 + r:
                dx, dy = x - (x1 - r - 1), y0 + r - y
            elif x < x0 + r and y >= y1 - r:
                dx, dy = x0 + r - x, y - (y1 - r - 1)
            elif x >= x1 - r and y >= y1 - r:
                dx, dy = x - (x1 - r - 1), y - (y1 - r - 1)
            if dx or dy:
                if dx * dx + dy * dy <= r * r:
                    mask[y][x] = 255
            else:
                mask[y][x] = 255
    return mask


def splash_png(path: Path):
    pixels = [[(0, 0, 0, 0) for _ in range(W)] for _ in range(H)]
    x0, y0, x1, y1, r = 36, 28, W - 36, H - 28, 28
    mask = rounded_mask(W, H, r, x0, y0, x1, y1)
    for y in range(H):
        for x in range(W):
            if not mask[y][x]:
                continue
            t = (x - x0) / max(1, (x1 - x0))
            u = (y - y0) / max(1, (y1 - y0))
            color = lerp(lerp(INDIGO, VIOLET, t), lerp(CYAN, AMBER, t), u * 0.35)
            # soft light wash on the right so the type stays readable
            if t > 0.42:
                color = lerp(color, WASH, min(1, (t - 0.42) / 0.4))
            pixels[y][x] = (*color, 255)
    # cyan progress pill, baked. The live bar is hidden so the
    # rounded corners stay. This pill does not move.
    for y in range(y1 - 36, y1 - 24):
        for x in range(x0 + 36, x1 - 36):
            if pixels[y][x][3] == 0:
                continue
            fill = x < x0 + 36 + int((x1 - x0 - 72) * 0.62)
            pixels[y][x] = ((34, 211, 238) if fill else (226, 219, 239)) + (255,)
    write_png(path, pixels)


def banner_bmp(path: Path):
    w, h = 493, 58
    rows = []
    for y in range(h):
        row = []
        for x in range(w):
            color = WASH
            if x > 430:
                color = lerp(WASH, CYAN, (x - 430) / 63)
            if y > 54:
                color = (226, 219, 239)
            row.append(color)
        rows.append(row)
    write_bmp(path, rows)


def dialog_bmp(path: Path):
    w, h = 493, 312
    rows = []
    for y in range(h):
        row = []
        for x in range(w):
            if x < 156:
                t = y / h
                color = lerp(INDIGO, VIOLET, t)
                if 70 < y < 130 and 40 < x < 116:
                    color = lerp(color, CYAN, 0.35)
            else:
                # Right side stays light. Windows paints black text here.
                color = WASH
            row.append(color)
        rows.append(row)
    write_bmp(path, rows)


def write_png(path: Path, pixels):
    h = len(pixels)
    w = len(pixels[0])
    raw = bytearray()
    for row in pixels:
        raw.append(0)
        for r, g, b, a in row:
            raw.extend((r, g, b, a))
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b"")
    path.write_bytes(png)


def write_bmp(path: Path, rows):
    h = len(rows)
    w = len(rows[0])
    stride = (w * 3 + 3) & ~3
    pixels = bytearray()
    for row in reversed(rows):
        line = bytearray()
        for r, g, b in row:
            line.extend((b, g, r))
        line.extend(b"\x00" * (stride - w * 3))
        pixels.extend(line)
    offset = 54
    size = offset + len(pixels)
    header = struct.pack("<2sIHHI", b"BM", size, 0, 0, offset)
    info = struct.pack("<IIIHHIIIIII", 40, w, h, 1, 24, 0, len(pixels), 2835, 2835, 0, 0)
    path.write_bytes(header + info + pixels)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    out = args.root / "installer" / "generated"
    out.mkdir(parents=True, exist_ok=True)
    splash_png(out / "splash.png")
    banner_bmp(out / "msi-banner.bmp")
    dialog_bmp(out / "msi-dialog.bmp")
    print(f"installer art written to {out}")


if __name__ == "__main__":
    main()
