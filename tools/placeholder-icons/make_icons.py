#!/usr/bin/env python3
"""Draws simple placeholder wheel icons (white glyphs on transparent, 128x128 RGBA PNG).

Stdlib only. Usage:
    python3 tools/placeholder-icons/make_icons.py OUT_DIR GLYPH:BADGE:"Icon Name" ...

GLYPH is one of: hatch, drop, cross, gauge, valve, wrench, generic, exhaust, dismantle.
BADGE is one of: none, on, off (a tick or a slash in the lower-right corner).
For each icon it writes "<Icon Name>.png" (white) and "<Icon Name> (Highlighted).png" (amber);
with --disabled (anywhere in the arguments) also "<Icon Name> (Disabled).png" (grey).
All art is generated geometry; nothing is copied from any other mod or game.
"""
import math
import os
import struct
import sys
import zlib

SIZE = 128
SS = 3  # supersampling per axis

def seg_dist(px, py, ax, ay, bx, by):
    vx, vy = bx - ax, by - ay
    wx, wy = px - ax, py - ay
    t = max(0.0, min(1.0, (wx * vx + wy * vy) / (vx * vx + vy * vy or 1.0)))
    dx, dy = px - (ax + t * vx), py - (ay + t * vy)
    return math.hypot(dx, dy)

def ring(cx, cy, r, w):
    return lambda x, y: abs(math.hypot(x - cx, y - cy) - r) <= w / 2

def disc(cx, cy, r):
    return lambda x, y: math.hypot(x - cx, y - cy) <= r

def line(ax, ay, bx, by, w):
    return lambda x, y: seg_dist(x, y, ax, ay, bx, by) <= w / 2

def rect(x0, y0, x1, y1):
    return lambda x, y: x0 <= x <= x1 and y0 <= y <= y1

def union(*fs):
    return lambda x, y: any(f(x, y) for f in fs)

def minus(a, b):
    return lambda x, y: a(x, y) and not b(x, y)

def drop():
    # circle bottom + triangle top
    c = disc(56, 70, 24)
    def tri(x, y):
        if y < 18 or y > 70:
            return False
        half = (y - 18) / 52.0 * 24
        return abs(x - 56) <= half
    return minus(union(c, tri), disc(48, 74, 7))

GLYPHS = {
    # hatch: round door with handwheel spokes
    "hatch": lambda: union(ring(56, 60, 38, 8), ring(56, 60, 12, 6),
                           line(56, 30, 56, 90, 5), line(26, 60, 86, 60, 5)),
    "drop": drop,
    "cross": lambda: union(rect(46, 22, 66, 98), rect(18, 50, 94, 70)),
    "gauge": lambda: union(ring(56, 62, 36, 8), line(56, 62, 78, 40, 7), disc(56, 62, 7)),
    "valve": lambda: union(ring(56, 60, 34, 8), line(30, 34, 82, 86, 7), line(30, 86, 82, 34, 7)),
    "wrench": lambda: union(line(36, 88, 74, 50, 14), minus(disc(80, 42, 20), rect(76, 14, 112, 46))),
    "generic": lambda: union(ring(56, 60, 36, 8), disc(56, 60, 10)),
    # exhaust (WS4): smoke puffs over an arrow into a tank whose water is being pushed down
    "exhaust": lambda: union(disc(34, 22, 9), disc(50, 16, 11), disc(68, 22, 9),
                             line(56, 30, 56, 66, 6), line(56, 68, 44, 56, 6), line(56, 68, 68, 56, 6),
                             minus(rect(18, 54, 94, 106), rect(25, 61, 87, 99)), rect(29, 88, 83, 95)),
    # dismantle (WS5): a spanner with loose pieces falling away from it
    "dismantle": lambda: union(line(26, 92, 58, 60, 12), minus(disc(64, 52, 18), rect(60, 26, 96, 56)),
                               rect(80, 80, 94, 94), rect(98, 62, 110, 74), rect(66, 100, 78, 112)),
}

BADGES = {
    "none": None,
    "on": lambda: union(line(92, 106, 102, 116, 7), line(102, 116, 122, 92, 7)),
    "off": lambda: union(ring(106, 104, 16, 6), line(95, 115, 117, 93, 6)),
}

def render(shape, rgb):
    rows = []
    n = SS * SS
    for py in range(SIZE):
        row = bytearray([0])  # filter: none
        for px in range(SIZE):
            hit = 0
            for sy in range(SS):
                for sx in range(SS):
                    if shape(px + (sx + 0.5) / SS, py + (sy + 0.5) / SS):
                        hit += 1
            a = round(255 * hit / n)
            row += bytes((rgb[0], rgb[1], rgb[2], a))
        rows.append(bytes(row))
    raw = b"".join(rows)
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    ihdr = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")

def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    disabled = "--disabled" in argv
    argv = [a for a in argv if a != "--disabled"]
    out = argv[1]
    os.makedirs(out, exist_ok=True)
    variants = [("", (255, 255, 255)), (" (Highlighted)", (255, 196, 64))]
    if disabled:
        variants.append((" (Disabled)", (128, 128, 128)))
    for spec in argv[2:]:
        glyph, badge, name = spec.split(":", 2)
        shape = GLYPHS[glyph]()
        b = BADGES[badge]
        if b is not None:
            shape = union(shape, b())
        for suffix, rgb in variants:
            path = os.path.join(out, name + suffix + ".png")
            with open(path, "wb") as f:
                f.write(render(shape, rgb))
            print("wrote", path)
    return 0

if __name__ == "__main__":
    sys.exit(main(sys.argv))
