"""Mesure l'écran de la tablette et crée l'image de superposition (tout sauf l'intérieur de l'écran).

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

# Superposition : toute l'image SAUF l'intérieur noir de l'écran. Le cadre, la main et le pouce
# sont ainsi redessinés devant l'interface, sans coupure possible. Le contour du pouce est élargi
# de DILATE pixels avec un fondu, pour englober son ombre sans bord dur.
DILATE = 3
inside = lambda x, y: sx <= x < sx + sw and sy <= y < sy + sh
# Masque du pouce (pixels non sombres à l'intérieur de l'écran)
thumb = [[inside(x, y) and not dark(rows[y], x) for x in range(w)] for y in range(h)]
# Distance (en pixels, approximation par carrés) au pouce, jusqu'à DILATE
dist = [[0 if thumb[y][x] else 99 for x in range(w)] for y in range(h)]
for step in range(1, DILATE + 1):
    for y in range(sy, sy + sh):
        for x in range(sx, sx + sw):
            if dist[y][x] == 99:
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        yy, xx = y + dy, x + dx
                        if 0 <= yy < h and 0 <= xx < w and dist[yy][xx] == step - 1:
                            dist[y][x] = step
out = []
count = 0
for y in range(h):
    r = rows[y]
    line = bytearray(4 * w)
    for x in range(w):
        R, G, B, A = r[4 * x:4 * x + 4]
        if not inside(x, y):
            line[4 * x:4 * x + 4] = r[4 * x:4 * x + 4]          # hors de l'écran : image telle quelle
        elif dist[y][x] <= DILATE:
            fade = 1.0 - dist[y][x] / (DILATE + 1.0)             # 1 sur le pouce, décroît dans l'ombre
            line[4 * x:4 * x + 4] = bytes([R, G, B, int(A * fade)])
            count += 1
    out.append(line)
write_png(sys.argv[2], w, h, out)
print(f"superposition : {count} pixels du pouce (avec contour) + tout le cadre")
