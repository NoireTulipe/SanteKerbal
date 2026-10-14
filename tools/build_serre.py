"""Construit la serre hydroponique SK-G2 dans Blender, l'exporte en .mu et rend un aperçu.

Usage : blender -b --python tools/build_serre.py -- <dossier_sortie> [image_apercu.png]

Dimensions du Convert-O-Tron 125 qu'elle remplace : 1,25 m de diamètre, points d'attache à +/- 0,9 m.
Inspirée de l'icône « serre » de la tablette : coupole vitrée, salades, socle métallique, réservoir vert.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sk_blender as sk  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
sk.start(args[0], seed=11)
PREVIEW = os.path.abspath(args[1]) if len(args) > 1 else None

R, FLANGE, NODE = 0.60, 0.625, 0.90
FRONT = -math.pi / 2
BASE_Z0, BASE_Z1 = -0.86, -0.36      # socle peint
GLASS_Z0, GLASS_Z1 = -0.34, 0.70     # vitre
TOP_Z0 = 0.70                        # toit métallique

# --- Textures et matériaux
base_h = BASE_Z1 - BASE_Z0
U, V, a = sk.panel_texture("serre_socle", base_h, FRONT, w=2048, h=512,
                           bands=[(0.12, 0.19, 0, 2 * math.pi)], seams_z=[-0.18], rust_streaks=30)
# Bandes jaunes et noires sous le logo, à l'avant
front = U(FRONT)
for x in range(int(front - 120), int(front + 120)):
    for y in range(int(V(-0.22)), int(V(-0.19))):
        a[y, x % a.shape[1]] = (0.95, 0.80, 0.10) if ((x + y) // 12) % 2 == 0 else (0.08, 0.08, 0.08)
mat_base = sk.material("socle", sk.save_image("serre_socle", a))
mat_metal = sk.material("metal", sk.metal_texture("serre_metal"), "KSP/Specular")

import numpy as np  # noqa: E402
g = np.zeros((256, 256, 4))
g[..., :3] = (0.80, 0.95, 1.00)
g[..., 3] = 0.18 + 0.10 * sk.smooth_noise(256, 256, 64, 2)          # légers reflets
for x in range(0, 256, 64):                                           # traînées de reflet verticales
    g[:, x:x + 6, 3] += 0.12
mat_glass = sk.material("vitre", sk.save_image("serre_vitre", g), "KSP/Alpha/Translucent", alpha=0.25)
mat_leaf = sk.material("feuilles", sk.noise_texture("serre_feuilles", (0.20, 0.50, 0.12), (0.55, 0.85, 0.30), cell=10))
mat_soil = sk.material("terreau", sk.noise_texture("serre_terreau", (0.18, 0.11, 0.06), (0.35, 0.22, 0.12), cell=8))
mat_lamp = sk.material("lampe", sk.solid_texture("serre_lampe", (0.95, 0.55, 1.0)), "KSP/Unlit", emission=3.0)
mat_tank = sk.material("reservoir", sk.noise_texture("serre_liquide", (0.25, 0.85, 0.20), (0.45, 1.0, 0.35), cell=20), "KSP/Unlit", emission=0.8)
mat_logo = sk.material("logo", sk.solid_texture("serre_logo", (0.60, 0.29, 0.14)))

# --- Géométrie
sk.make_root("serre")
socle = sk.cylinder("socle", R, BASE_Z0, BASE_Z1, mat_base, 64, caps=False)
sk.cylindrical_uv(socle, base_h)
sk.cylinder("anneau_bas", FLANGE, -NODE, BASE_Z0, mat_metal)
sk.cylinder("couvercle_bas", 0.46, -NODE - 0.02, -NODE + 0.001, mat_metal, 40)
sk.cylinder("anneau_milieu", FLANGE, BASE_Z1, GLASS_Z0, mat_metal)
sk.cylinder("plancher", R - 0.01, GLASS_Z0 - 0.005, GLASS_Z0 + 0.005, mat_metal, 48)

# Vitre (sans couvercles) et montants
sk.cylinder("vitre", R - 0.005, GLASS_Z0, GLASS_Z1, mat_glass, 48, caps=False)
for k in range(8):
    ang = FRONT + math.pi / 8 + k * math.pi / 4
    sk.box("montant", (0.035, 0.035, GLASS_Z1 - GLASS_Z0), (R * math.cos(ang), R * math.sin(ang), (GLASS_Z0 + GLASS_Z1) / 2),
           mat_metal, rotation=(0, 0, ang))

# Toit, rampe lumineuse et couvercle haut
sk.cylinder("toit", FLANGE, TOP_Z0, NODE, mat_metal)
sk.cylinder("couvercle_haut", 0.46, NODE - 0.001, NODE + 0.02, mat_metal, 40)
sk.cylinder("lampe", 0.42, TOP_Z0 - 0.025, TOP_Z0 - 0.005, mat_lamp, 40)

# Intérieur : bac de terreau, salades, colonne hydroponique
sk.cylinder("bac", 0.52, GLASS_Z0, GLASS_Z0 + 0.10, mat_metal, 40)
sk.cylinder("terreau", 0.50, GLASS_Z0 + 0.10, GLASS_Z0 + 0.115, mat_soil, 40)
salades = [(0, 0)] + [(0.30 * math.cos(k * math.pi / 3), 0.30 * math.sin(k * math.pi / 3)) for k in range(6)]
for i, (x, y) in enumerate(salades):
    s = 0.15 if i == 0 else 0.12
    for j in range(3):   # trois boules de feuilles par salade, légèrement décalées
        dx, dy = 0.04 * math.cos(j * 2.1), 0.04 * math.sin(j * 2.1)
        sk.sphere("salade", s * (1 - 0.15 * j), (x + dx, y + dy, GLASS_Z0 + 0.17 + 0.03 * j), mat_leaf,
                  scale=(1, 1, 0.65), subdiv=2)
sk.cylinder("colonne", 0.035, GLASS_Z0 + 0.1, TOP_Z0, mat_metal, 16)

# Réservoir vert sur le côté (+X), avec tuyaux, et logo sur le socle
sk.cylinder("reservoir_cuve", 0.11, -0.78, -0.42, mat_tank, 24, location=(R + 0.10, 0.10))
sk.cylinder("reservoir_haut", 0.12, -0.42, -0.39, mat_metal, 24, location=(R + 0.10, 0.10))
sk.cylinder("reservoir_bas", 0.12, -0.81, -0.78, mat_metal, 24, location=(R + 0.10, 0.10))
sk.cylinder("tuyau", 0.02, 0, 0.16, mat_metal, 12, location=(R + 0.02, 0.10, -0.47), rotation=(0, math.pi / 2, 0))
sk.wrapped_text("logo", "NoireTulipe ©", R, FRONT, -0.63, 0.09, mat_logo)

sk.mesh_collider(FLANGE, -NODE, NODE)
print(f"SERRE triangles={sk.triangulate_all()}")
print("SERRE export", sk.export("serre.mu"))
if PREVIEW:
    sk.preview(PREVIEW, cam_location=(1.9, -3.5, 1.4))
    print("SERRE apercu", PREVIEW)
