"""Construit le caisson d'hibernation SK-H2 dans Blender, peint ses textures, l'exporte en .mu
et rend une image d'aperçu.

Usage (sans ouvrir Blender) :
    blender -b --python tools/build_caisson.py -- <dossier_sortie> <image_apercu.png>

Le dossier de sortie reçoit caisson.mu et ses textures (PNG). Dimensions calquées sur la cabine Mk1 :
1,25 m de diamètre, points d'attache à +/- 0,9375 m. En Blender l'axe vertical est Z ; le module
d'export (io_object_mu) le convertit en Y pour KSP.
"""
import math
import os
import sys

import addon_utils
import bmesh
import bpy
import numpy as np
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
OUT_DIR = os.path.abspath(args[0])
PREVIEW = os.path.abspath(args[1]) if len(args) > 1 else None
os.makedirs(OUT_DIR, exist_ok=True)

# Dimensions (mètres)
R = 0.60            # rayon du corps
HALF = 0.85         # demi-hauteur du corps peint
FLANGE_R = 0.625    # rayon des anneaux (1,25 m de diamètre)
NODE = 0.9375       # hauteur des points d'attache
FRONT = -math.pi / 2   # la face avant regarde vers -Y

rng = np.random.default_rng(7)

bpy.ops.wm.read_factory_settings(use_empty=True)
addon_utils.enable("io_object_mu")


# ---------------------------------------------------------------- textures (numpy)

def smooth_noise(h, w, cell, octaves=4):
    """Bruit lissé (valeurs 0..1) par interpolation bilinéaire de grilles aléatoires."""
    out = np.zeros((h, w))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        c = max(2, int(cell / (2 ** o)))
        gh, gw = h // c + 2, w // c + 2
        grid = rng.random((gh, gw))
        ys = np.linspace(0, gh - 2, h)
        xs = np.linspace(0, gw - 2, w)
        y0, x0 = ys.astype(int), xs.astype(int)
        fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
        a = grid[y0][:, x0]
        b = grid[y0][:, x0 + 1]
        c_ = grid[y0 + 1][:, x0]
        d = grid[y0 + 1][:, x0 + 1]
        out += amp * (a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c_ * (1 - fx) * fy + d * fx * fy)
        total += amp
        amp *= 0.5
    return out / total


def save_image(name, arr):
    """arr : (h, w, 3 ou 4), ligne 0 = bas de l'image (convention de Blender)."""
    h, w = arr.shape[:2]
    if arr.shape[2] == 3:
        arr = np.concatenate([arr, np.ones((h, w, 1))], axis=2)
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(np.clip(arr, 0, 1).astype(np.float32).ravel())
    img.filepath_raw = os.path.join(OUT_DIR, name + ".png")
    img.file_format = "PNG"
    img.save()
    return img


def paint_rect(a, x0, x1, y0, y1, color, alpha=1.0):
    h, w = a.shape[:2]
    x0, x1 = max(0, int(x0)), min(w, int(x1))
    y0, y1 = max(0, int(y0)), min(h, int(y1))
    if x1 > x0 and y1 > y0:
        a[y0:y1, x0:x1, :3] = a[y0:y1, x0:x1, :3] * (1 - alpha) + np.array(color) * alpha


def paint_disc(a, cx, cy, r, color, alpha=1.0):
    h, w = a.shape[:2]
    ys, xs = np.ogrid[:h, :w]
    m = ((xs - cx) ** 2 + (ys - cy) ** 2) <= r * r
    a[m, :3] = a[m, :3] * (1 - alpha) + np.array(color) * alpha


def body_texture():
    W, H = 2048, 1024
    a = np.zeros((H, W, 3))
    a[:] = (0.80, 0.80, 0.77)
    # Salissures et usure
    grime = smooth_noise(H, W, 256)
    a *= (0.86 + 0.18 * grime)[..., None]
    fine = smooth_noise(H, W, 24, 2)
    a *= (0.95 + 0.07 * fine)[..., None]

    def U(angle):   # angle (radians) -> colonne de la texture
        return ((angle / (2 * math.pi)) % 1.0) * W

    def V(z):       # hauteur (m) -> ligne de la texture
        return (z + HALF) / (2 * HALF) * H

    front = U(FRONT)
    # Bandes orange usées (haut, milieu à droite, bas)
    wear = smooth_noise(H, W, 40, 3)
    orange = np.array((0.88, 0.45, 0.13))
    for z0, z1, u0, u1 in [(0.70, 0.78, 0, W), (-0.70, -0.62, 0, W), (0.02, 0.10, front + 260, front + 760)]:
        y0, y1 = int(V(z0)), int(V(z1))
        for x0, x1 in [(u0, min(u1, W)), (0, max(0, u1 - W))]:
            x0, x1 = int(x0), int(x1)
            if x1 <= x0:
                continue
            m = wear[y0:y1, x0:x1] > 0.38
            seg = a[y0:y1, x0:x1]
            seg[m] = seg[m] * 0.15 + orange * 0.85
    # Panneaux en relief de part et d'autre des hublots
    for side in (-1, 1):
        cx = front + side * 150
        paint_rect(a, cx - 70, cx + 70, V(-0.32), V(0.25), (0.70, 0.71, 0.70), 0.85)
        for x in (cx - 70, cx + 69):
            paint_rect(a, x, x + 2, V(-0.32), V(0.25), (0.35, 0.35, 0.34))
        for y in (V(-0.32), V(0.25) - 2):
            paint_rect(a, cx - 70, cx + 70, y, y + 2, (0.35, 0.35, 0.34))
    # Joints des panneaux (horizontaux et verticaux) et rivets le long des joints
    seams_z = [-0.55, -0.40, 0.30, 0.55]
    seams_u = [front + k * W / 6 for k in range(6)] + [front + 95, front - 95]
    rust = np.array((0.45, 0.25, 0.12))
    for z in seams_z:
        y = int(V(z))
        paint_rect(a, 0, W, y - 1, y + 2, (0.38, 0.38, 0.37))
        paint_rect(a, 0, W, y + 2, y + 3, (0.92, 0.92, 0.90), 0.6)
        for x in np.arange(10, W, 34):
            paint_disc(a, x + 1, y + 6, 3, (0.30, 0.30, 0.30), 0.8)
            paint_disc(a, x, y + 7, 3, (0.90, 0.90, 0.88))
    for u in seams_u:
        x = int(u) % W
        paint_rect(a, x - 1, x + 2, 0, H, (0.38, 0.38, 0.37))
        for y in np.arange(14, H, 40):
            paint_disc(a, x + 7, y - 1, 3, (0.30, 0.30, 0.30), 0.8)
            paint_disc(a, x + 6, y, 3, (0.90, 0.90, 0.88))
    # Coulures de rouille sous quelques rivets
    for _ in range(70):
        x, y = rng.integers(0, W), rng.integers(40, H)
        length, width = rng.integers(25, 90), rng.integers(2, 5)
        for k in range(length):
            alpha = 0.55 * (1 - k / length)
            paint_rect(a, x, x + width, y - k - 1, y - k, rust, alpha)
    # Rouille sur les bords haut et bas
    edge = smooth_noise(H, W, 20, 2)
    for y in range(H):
        d = min(y, H - 1 - y) / 40.0
        if d < 1:
            m = edge[y] > 0.3 + 0.6 * d
            a[y, m] = a[y, m] * 0.4 + rust * 0.6
    # Bandes jaunes et noires sous la plaque, étiquettes
    y0, y1 = int(V(-0.66)), int(V(-0.60))
    for x in range(int(front - 150), int(front + 150)):
        for y in range(y0, y1):
            stripe = ((x + y) // 14) % 2 == 0
            a[y, x % W] = (0.95, 0.80, 0.10) if stripe else (0.08, 0.08, 0.08)
    paint_rect(a, front + 175, front + 235, V(-0.36), V(-0.27), (0.95, 0.95, 0.93))
    paint_rect(a, front + 175, front + 235, V(-0.36), V(-0.34), (0.80, 0.15, 0.10))
    paint_rect(a, front + 175, front + 235, V(-0.47), V(-0.40), (0.95, 0.95, 0.93))
    for k in range(30):   # petit triangle jaune « danger »
        paint_rect(a, front - 230 + k * 0.5, front - 170 - k * 0.5, V(-0.40) + k, V(-0.40) + k + 1, (0.97, 0.80, 0.10))
    return save_image("caisson_corps", a)


def metal_texture():
    W = H = 512
    a = np.zeros((H, W, 3))
    a[:] = (0.62, 0.63, 0.64)
    brushed = smooth_noise(H, W, 6, 2)
    streak = np.repeat(rng.random((H, 1)), W, axis=1)
    a *= (0.88 + 0.10 * brushed + 0.06 * streak)[..., None]
    spots = smooth_noise(H, W, 64, 3)
    m = spots > 0.68
    a[m] = a[m] * 0.6 + np.array((0.45, 0.27, 0.14)) * 0.4
    return save_image("caisson_metal", a)


def glass_texture():
    W = H = 256
    ys, xs = np.mgrid[:H, :W]
    d = np.sqrt((xs - W / 2) ** 2 + (ys - H / 2) ** 2) / (W / 2)
    centre, bord = np.array((0.85, 0.98, 1.0)), np.array((0.25, 0.60, 0.90))
    a = centre * (1 - d[..., None]) + bord * d[..., None]
    frost = smooth_noise(H, W, 8, 3)
    a = a * (0.85 + 0.25 * frost[..., None])
    return save_image("caisson_hublot", a)


def solid_texture(name, color):
    a = np.zeros((8, 8, 3))
    a[:] = color
    return save_image(name, a)


# ---------------------------------------------------------------- matériaux

def make_material(name, image, shader="KSP/Diffuse", emission=0.0):
    """Matériau Blender (pour l'aperçu) + propriétés KSP (shader et _MainTex) pour l'export .mu."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes["Principled BSDF"]
    tex = nodes.new("ShaderNodeTexImage")
    tex.name = "_MainTex"
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.6
    if emission > 0:
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = emission
    props = mat.mumatprop
    props.shaderName = shader
    t = props.texture.properties.add()
    t.name = "_MainTex"
    t.tex = image.name
    if shader == "KSP/Diffuse":
        c = props.color.properties.add()
        c.name = "_Color"
        c.value = (1.0, 1.0, 1.0, 1.0)
    return mat


# ---------------------------------------------------------------- géométrie

root = bpy.data.objects.new("caisson", None)
bpy.context.scene.collection.objects.link(root)


def link(obj, mat=None):
    obj.parent = root
    if mat is not None:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    return obj


def cylinder(name, radius, z0, z1, segments=48, caps=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments, radius=radius, depth=z1 - z0,
                                        location=(0, 0, (z0 + z1) / 2),
                                        end_fill_type="NGON" if caps else "NOTHING")
    obj = bpy.context.active_object
    obj.name = name
    return obj


def box(name, size, location, mat):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return link(obj, mat)


def triangulate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(obj.data)
    bm.free()


tex_body = body_texture()
tex_metal = metal_texture()
tex_glass = glass_texture()
tex_logo = solid_texture("caisson_logo", (0.60, 0.29, 0.14))
mat_body = make_material("corps", tex_body)
mat_metal = make_material("metal", tex_metal, "KSP/Specular")
mat_glass = make_material("hublot", tex_glass, "KSP/Unlit", emission=1.5)
mat_logo = make_material("logo", tex_logo)

# Corps : cylindre sans couvercles, UV cylindrique (u = angle, v = hauteur)
body = link(cylinder("corps", R, -HALF, HALF, 64, caps=False), mat_body)
mesh = body.data
uv = mesh.uv_layers.active.data
for poly in mesh.polygons:
    us = []
    for li in poly.loop_indices:
        co = mesh.vertices[mesh.loops[li].vertex_index].co
        us.append(((math.atan2(co.y, co.x) / (2 * math.pi)) % 1.0, (co.z + HALF) / (2 * HALF)))
    # couture : éviter qu'une face fasse le tour complet de la texture
    if max(u for u, _ in us) - min(u for u, _ in us) > 0.5:
        us = [(u + 1.0 if u < 0.5 else u, v) for u, v in us]
    for li, (u, v) in zip(poly.loop_indices, us):
        uv[li].uv = (u, v)

# Anneaux et couvercles haut et bas
for sign in (1, -1):
    z_in, z_out = sign * HALF, sign * NODE
    lo, hi = min(z_in, z_out), max(z_in, z_out)
    link(cylinder(f"anneau_{'haut' if sign > 0 else 'bas'}", FLANGE_R, lo, hi, 48), mat_metal)
    lid_lo = hi if sign > 0 else lo - 0.0
    if sign > 0:
        link(cylinder("couvercle_haut", 0.46, NODE - 0.001, NODE + 0.02, 40), mat_metal)
    else:
        link(cylinder("couvercle_bas", 0.46, -NODE - 0.02, -NODE + 0.001, 40), mat_metal)
    # bourrelet intermédiaire
    z_b = sign * (HALF - 0.04)
    link(cylinder(f"bourrelet_{'haut' if sign > 0 else 'bas'}", R + 0.012, z_b - 0.015, z_b + 0.015, 48), mat_metal)

# Hublots : cadre métallique + vitre lumineuse, sur la face avant (-Y)
for z in (0.06, -0.20):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.10, depth=0.06, location=(0, -R - 0.005, z),
                                        rotation=(math.pi / 2, 0, 0))
    frame = link(bpy.context.active_object, mat_metal)
    frame.name = "cadre_hublot"
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=0.075, depth=0.02, location=(0, -R - 0.03, z),
                                        rotation=(math.pi / 2, 0, 0))
    glass = link(bpy.context.active_object, mat_glass)
    glass.name = "vitre_hublot"

# Plaque vierge à l'avant, en bas
box("plaque", (0.32, 0.02, 0.15), (0, -R - 0.005, -0.45), mat_metal)

# Côté droit (+X) : deux boîtiers et un tuyau vertical
box("boitier_haut", (0.06, 0.16, 0.20), (R + 0.02, 0.0, 0.40), mat_metal)
box("boitier_bas", (0.06, 0.14, 0.18), (R + 0.02, 0.0, -0.45), mat_metal)
for y in (-0.16, -0.12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.018, depth=0.62, location=(R + 0.02, y, -0.02))
    link(bpy.context.active_object, mat_metal).name = "tuyau"
# Côté gauche (-X) : poignée et deux petits boîtiers
bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.02, depth=0.45, location=(-R - 0.03, 0.0, 0.0))
link(bpy.context.active_object, mat_metal).name = "poignee"
box("boitier_gauche_haut", (0.05, 0.12, 0.12), (-R - 0.01, 0.0, 0.45), mat_metal)
box("boitier_gauche_bas", (0.05, 0.12, 0.12), (-R - 0.01, 0.0, -0.45), mat_metal)

# Logo NoireTulipe en relief, enroulé sur le cylindre à l'avant
bpy.ops.object.text_add(location=(0, 0, 0))
txt = bpy.context.active_object
txt.data.body = "NoireTulipe ©"
for path in ("C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf"):
    if os.path.exists(path):
        txt.data.font = bpy.data.fonts.load(path)
        break
txt.data.size = 0.13
txt.data.extrude = 0.004
txt.data.align_x = "CENTER"
txt.data.align_y = "CENTER"
txt.data.resolution_u = 4
bpy.ops.object.convert(target="MESH")
logo = bpy.context.active_object
logo.name = "logo"
# Texte dans le plan XY (x = largeur, y = hauteur, z = épaisseur) -> enroulé autour de l'axe Z
for v in logo.data.vertices:
    x, y, depth = v.co.x, v.co.y, v.co.z
    radius = R + 0.006 + depth
    theta = FRONT + x / R
    v.co = Vector((radius * math.cos(theta), radius * math.sin(theta), 0.42 + y))
link(logo, mat_logo)

# Collisionneur (invisible) : cylindre simple et convexe
col = cylinder("collider", FLANGE_R, -NODE, NODE, 16)
col.parent = root
col.muproperties.collider = "MU_COL_MESH"
col.muproperties.isConvex = True

# Sas pour l'EVA : déclencheur nommé « airlock », étiqueté Airlock (comme les pièces de KSP)
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, -R - 0.15, -0.05))
air = bpy.context.active_object
air.name = "airlock"
air.scale = (0.5, 0.2, 0.9)
bpy.ops.object.transform_apply(scale=True)
air.parent = root
air.muproperties.collider = "MU_COL_BOX"
air.muproperties.isTrigger = True
air.muproperties.size = (0.5, 0.2, 0.9)
air.muproperties.center = (0, 0, 0)
air.muproperties.tag = "Airlock"
air.muproperties.layer = 21

# Triangulation de tout ce qui est visible (KSP n'affiche que des triangles)
tris = 0
for obj in root.children:
    if obj.type == "MESH" and obj.name not in ("collider", "airlock"):
        triangulate(obj)
        tris += len(obj.data.polygons)
print(f"CAISSON triangles={tris}")

# ---------------------------------------------------------------- export .mu
bpy.ops.object.select_all(action="DESELECT")
root.select_set(True)
bpy.context.view_layer.objects.active = root
mu_path = os.path.join(OUT_DIR, "caisson.mu")
res = bpy.ops.export_object.ksp_mu(filepath=mu_path)
print("CAISSON export", res, os.path.getsize(mu_path))

# ---------------------------------------------------------------- aperçu
if PREVIEW:
    for name in ("collider", "airlock"):
        bpy.data.objects[name].hide_render = True
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.device = "CPU"
    scene.render.resolution_x, scene.render.resolution_y = 900, 1000
    world = bpy.data.worlds.new("fond")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.05, 0.06, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    scene.world = world
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (1.9, -3.6, 1.3)
    direction = Vector((0, 0, 0)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 50
    scene.camera = cam
    for loc, energy in [((3, -3, 4), 900), ((-4, -2, 1), 300), ((0, 4, 3), 400)]:
        ld = bpy.data.lights.new("lum", "POINT")
        ld.energy = energy
        lo = bpy.data.objects.new("lum", ld)
        lo.location = loc
        scene.collection.objects.link(lo)
    scene.render.filepath = PREVIEW
    bpy.ops.render.render(write_still=True)
    print("CAISSON apercu", PREVIEW)
