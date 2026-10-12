"""Mesure l'écran de la tablette et extrait ce qui le recouvre (le pouce) dans une image à part.

Usage : python tools/tablet_screen.py <tablette.png> <sortie_pouce.png>
Affiche le rectangle de l'écran (x, y, largeur, hauteur) en pixels de l'image, origine en haut à gauche.
"""
import struct, sys, zlib


def read_png(path):
    data = open(path, 'rb').read()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', "pas un PNG"
    i, idat = 8, b''
    while i < len(data):
        n = struct.unpack('>I', data[i:i + 4])[0]
        tag, chunk = data[i + 4:i + 8], data[i + 8:i + 8 + n]
        i += 12 + n
        if tag == b'IHDR':
            w, h, depth, ctype, _, _, inter = struct.unpack('>IIBBBBB', chunk)
            assert depth == 8 and ctype == 6 and inter == 0, "PNG RGBA 8 bits non entrelacé attendu"
        elif tag == b'IDAT':
            idat += chunk
    raw = zlib.decompress(idat)
    stride, bpp = w * 4, 4
    rows, prev, pos = [], bytearray(stride), 0
    for _ in range(h):
        f = raw[pos]
        line = bytearray(raw[pos + 1:pos + 1 + stride])
        pos += 1 + stride
        for x in range(stride):
            a = line[x - bpp] if x >= bpp else 0
            b = prev[x]
            c = prev[x - bpp] if x >= bpp else 0
            if f == 1: line[x] = (line[x] + a) & 255
            elif f == 2: line[x] = (line[x] + b) & 255
            elif f == 3: line[x] = (line[x] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    return w, h, rows


def write_png(path, w, h, rows):
    def chunk(tag, d):
        return struct.pack('>I', len(d)) + tag + d + struct.pack('>I', zlib.crc32(tag + d) & 0xFFFFFFFF)
    raw = b''.join(b'\x00' + bytes(r) for r in rows)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                           + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def dark(r, x):
    R, G, B, A = r[4 * x:4 * x + 4]
    return A > 200 and max(R, G, B) < 40


w, h, rows = read_png(sys.argv[1])
cy, cx = h // 2, w // 2 - 100   # point de départ : dans l'écran, à gauche du pouce

# Bords de l'écran : on part du centre et on avance tant que les pixels sont sombres
x0 = cx
while x0 > 0 and dark(rows[cy], x0 - 1): x0 -= 1
y0 = cy
while y0 > 0 and dark(rows[y0 - 1], cx): y0 -= 1
y1 = cy
while y1 < h - 1 and dark(rows[y1 + 1], cx): y1 += 1
# Bord droit : mesuré sur une ligne haute, au-dessus du pouce
yr = y0 + 20
x1 = cx
while x1 < w - 1 and dark(rows[yr], x1 + 1): x1 += 1

# Marge de sécurité pour ne pas mordre sur le cadre arrondi
m = 6
sx, sy, sw, sh = x0 + m, y0 + m, (x1 - x0 + 1) - 2 * m, (y1 - y0 + 1) - 2 * m
print(f"ecran x={sx} y={sy} largeur={sw} hauteur={sh} (image {w}x{h})")

# Pouce : pixels non sombres à l'intérieur de l'écran ; tout le reste devient transparent
out = []
count = 0
for y in range(h):
    line = bytearray(4 * w)
    if sy <= y < sy + sh:
        r = rows[y]
        for x in range(sx, sx + sw):
            if not dark(r, x):
                line[4 * x:4 * x + 4] = r[4 * x:4 * x + 4]
                count += 1
    out.append(line)
write_png(sys.argv[2], w, h, out)
print(f"pouce : {count} pixels extraits")
