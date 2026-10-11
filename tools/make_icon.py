"""Génère l'icône de barre d'outils (cœur blanc avec tracé de pouls, 38x38, fond transparent)."""
import struct, sys, zlib

W = H = 38
SS = 4  # suréchantillonnage pour l'anticrénelage

def heart(x, y):
    # Courbe du cœur : (x²+y²-1)³ - x²y³ <= 0, sur [-1.3,1.3]
    return (x * x + y * y - 1) ** 3 - x * x * y ** 3 <= 0

def pulse(x, y):
    # Ligne de pouls (zigzag) découpée dans le cœur
    pts = [(-1.4, 0.15), (-0.45, 0.15), (-0.25, 0.55), (0.0, -0.45), (0.2, 0.35), (0.35, 0.15), (1.4, 0.15)]
    for (x1, y1), (x2, y2) in zip(pts, pts[1:]):
        dx, dy = x2 - x1, y2 - y1
        t = max(0.0, min(1.0, ((x - x1) * dx + (y - y1) * dy) / (dx * dx + dy * dy)))
        if (x - x1 - t * dx) ** 2 + (y - y1 - t * dy) ** 2 < 0.11 ** 2:
            return True
    return False

rows = []
for py in range(H):
    row = bytearray([0])
    for px in range(W):
        cover = 0
        for sy in range(SS):
            for sx in range(SS):
                x = ((px + (sx + 0.5) / SS) / W - 0.5) * 2.7
                y = -((py + (sy + 0.5) / SS) / H - 0.5) * 2.7 + 0.12
                if heart(x, y) and not pulse(x, y):
                    cover += 1
        row += bytes([255, 255, 255, round(255 * cover / (SS * SS))])
    rows.append(bytes(row))

def chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0)) \
    + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b"")
with open(sys.argv[1], "wb") as f:
    f.write(png)
