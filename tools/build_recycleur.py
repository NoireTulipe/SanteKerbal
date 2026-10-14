"""Construit le recycleur d'eau SK-R3 dans Blender, l'exporte en .mu et rend un aperçu.

Usage : blender -b --python tools/build_recycleur.py -- <dossier_sortie> [image_apercu.png]

Module empilable de 1,25 m de diamètre et 0,8 m de haut (points d'attache à +/- 0,4 m).
Inspiré de l'icône « recycleur d'eau » de la tablette : réservoir d'eau bleue sous vitre, cuves grises
rouillées, tuyaux, cadran et symbole du recyclage.
"""
import math
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sk_blender as sk  # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
sk.start(args[0], seed=23)
PREVIEW = os.path.abspath(args[1]) if len(args) > 1 else None

FLANGE, NODE = 0.625, 0.40
PLATE = 0.34          # face intérieure des plaques haut et bas

# --- Textures et matériaux
mat_metal = sk.material("metal", sk.metal_texture("recycleur_metal", base=(0.60, 0.62, 0.64), rust=0.74, rust_cell=20), "KSP/Specular")
mat_rust = sk.material("cuve", sk.metal_texture("recycleur_cuve", base=(0.55, 0.56, 0.55), rust=0.62, rust_cell=24), "KSP/Specular")
mat_orange = sk.material("liseré", sk.noise_texture("recycleur_orange", (0.80, 0.38, 0.10), (0.95, 0.55, 0.18), cell=12))

g = np.zeros((128, 128, 4))
g[..., :3] = (0.85, 0.95, 1.0)
g[..., 3] = 0.15 + 0.08 * sk.smooth_noise(128, 128, 32, 2)
mat_glass = sk.material("vitre", sk.save_image("recycleur_vitre", g), "KSP/Alpha/Translucent", alpha=0.2)

w = np.zeros((256, 256, 4))
n = sk.smooth_noise(256, 256, 24, 3)[..., None]
w[..., :3] = np.array((0.10, 0.45, 0.85)) * (1 - n) + np.array((0.35, 0.75, 1.0)) * n
w[..., 3] = 0.75
mat_water = sk.material("eau", sk.save_image("recycleur_eau", w), "KSP/Alpha/Translucent", alpha=0.75)

# Cadran : fond blanc, graduations, zone rouge, aiguille
d = np.ones((128, 128, 3)) * 0.93
ys, xs = np.mgrid[:128, :128]
ang = np.arctan2(ys - 64, xs - 64)
rad = np.sqrt((xs - 64) ** 2 + (ys - 64) ** 2)
d[rad > 60] = (0.25, 0.25, 0.27)
for k in range(11):   # graduations sur un arc de 270°
    t = math.radians(225 - k * 27)
    for r in range(44, 56):
        d[int(64 + r * math.sin(t)), int(64 + r * math.cos(t))] = (0.1, 0.1, 0.1)
red = (rad > 44) & (rad < 56) & (ang > math.radians(-45)) & (ang < math.radians(10))
d[red] = (0.85, 0.15, 0.10)
needle = math.radians(60)
for r in range(0, 48):
    for o in (-1, 0, 1):
        d[int(64 + r * math.sin(needle)) + o, int(64 + r * math.cos(needle))] = (0.80, 0.10, 0.08)
mat_dial = sk.material("cadran", sk.save_image("recycleur_cadran", d))
mat_green = sk.material("symbole", sk.solid_texture("recycleur_symbole", (0.20, 0.75, 0.25)))
mat_logo = sk.material("logo", sk.solid_texture("recycleur_logo", (0.60, 0.29, 0.14)))

# --- Géométrie
sk.make_root("recycleur")
sk.cylinder("plaque_bas", FLANGE, -NODE, -PLATE, mat_metal)
sk.cylinder("plaque_haut", FLANGE, PLATE, NODE, mat_metal)
sk.cylinder("couvercle_haut", 0.46, NODE - 0.001, NODE + 0.02, mat_metal, 40)
sk.cylinder("couvercle_bas", 0.46, -NODE - 0.02, -NODE + 0.001, mat_metal, 40)
for x in (-0.56, 0.56):   # montants latéraux
    sk.cylinder("montant", 0.03, -PLATE, PLATE, mat_metal, 12, location=(x, 0))

# Réservoir d'eau central : vitre, eau, bagues
sk.cylinder("reservoir_vitre", 0.30, -PLATE + 0.03, PLATE - 0.03, mat_glass, 40, caps=False)
sk.cylinder("eau", 0.285, -PLATE + 0.03, 0.12, mat_water, 40)
sk.cylinder("bague_bas", 0.315, -PLATE, -PLATE + 0.04, mat_metal, 40)
sk.cylinder("bague_haut", 0.315, PLATE - 0.04, PLATE, mat_metal, 40)

# Cuves rouillées à l'arrière, avec dômes et tuyaux vers le réservoir
for x in (-0.42, 0.42):   # sur les côtés, un peu en arrière : visibles de face
    sk.cylinder("cuve", 0.14, -PLATE, 0.18, mat_rust, 24, location=(x, 0.12))
    sk.sphere("dome", 0.14, (x, 0.12, 0.18), mat_rust, scale=(1, 1, 0.6), subdiv=2)
    sk.cylinder("cerclage", 0.145, -0.05, -0.02, mat_metal, 24, location=(x, 0.12))
    sk.cylinder("tuyau", 0.025, 0, 0.14, mat_metal, 12, location=(x * 0.62, 0.07, 0.05),
                rotation=(0, math.pi / 2, 0))
    sk.cylinder("tuyau_vertical", 0.02, 0.24, PLATE, mat_metal, 12, location=(x, 0.12))
# Liseré orange usé sur le bord des plaques
sk.cylinder("lisere_haut", FLANGE + 0.003, PLATE + 0.012, PLATE + 0.045, mat_orange, 48, caps=False)
sk.cylinder("lisere_bas", FLANGE + 0.003, -PLATE - 0.045, -PLATE - 0.012, mat_orange, 48, caps=False)

# Boîtier de commande à l'avant (-Y) : cadran, symbole du recyclage, logo
sk.box("boitier", (0.36, 0.12, 0.50), (0, -0.44, -0.04), mat_metal)
dial = sk.cylinder("cadran", 0.065, 0, 0.02, mat_dial, 32, location=(0, -0.505, 0.12), rotation=(math.pi / 2, 0, 0))
sk.planar_uv(dial, axis_u=0, axis_v=1)
sk.flat_text("symbole_recyclage", "\u267B", (0, -0.502, -0.08), 0.32, mat_green,
             font_paths=("C:/Windows/Fonts/seguisym.ttf", "C:/Windows/Fonts/seguiemj.ttf"), depth=0.006)
sk.flat_text("logo", "NoireTulipe ©", (0, -0.502, -0.235), 0.045, mat_logo)

sk.mesh_collider(FLANGE, -NODE, NODE)
print(f"RECYCLEUR triangles={sk.triangulate_all()}")
print("RECYCLEUR export", sk.export("recycleur.mu"))
if PREVIEW:
    sk.preview(PREVIEW, cam_location=(1.6, -2.6, 1.0), size=(900, 800))
    print("RECYCLEUR apercu", PREVIEW)
