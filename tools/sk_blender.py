"""Outils communs pour construire les pièces de SantéKerbal dans Blender et les exporter en .mu.

Utilisé par build_caisson.py, build_serre.py et build_recycleur.py. En Blender, l'axe vertical est Z ;
le module d'export (io_object_mu) le convertit en Y pour KSP.
"""
import math
import os

import addon_utils
import bmesh
import bpy
import numpy as np
from mathutils import Vector

rng = np.random.default_rng(7)
OUT_DIR = "."


def start(out_dir, seed=7):
    """Scène vide, module .mu activé, dossier de sortie."""
    global OUT_DIR, rng
    OUT_DIR = os.path.abspath(out_dir)
    os.makedirs(OUT_DIR, exist_ok=True)
    rng = np.random.default_rng(seed)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    addon_utils.enable("io_object_mu")


# ------------------------------------------------------------------ textures

def smooth_noise(h, w, cell, octaves=4):
    """Bruit lissé (0..1) : grilles aléatoires interpolées, plusieurs octaves."""
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
    """arr : (h, w, 3 ou 4), ligne 0 = bas de l'image (convention de Blender). Enregistre name.png."""
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


def panel_texture(name, height, front_angle, w=2048, h=1024, bands=(), seams_z=(), rust_streaks=60):
    """
    Peinture « panneaux rivetés » d'un cylindre de hauteur `height` (m), centré en z=0 :
    blanc cassé usé, joints et rivets, bandes orange (liste de (z0, z1, angle0, angle1)), rouille.
    Renvoie (image, U, V, tableau) pour permettre d'ajouter des détails avant enregistrement.
    """
    half = height / 2
    a = np.zeros((h, w, 3))
    a[:] = (0.80, 0.80, 0.77)
    a *= (0.86 + 0.18 * smooth_noise(h, w, 256))[..., None]
    a *= (0.95 + 0.07 * smooth_noise(h, w, 24, 2))[..., None]

    def U(angle):
        return ((angle / (2 * math.pi)) % 1.0) * w

    def V(z):
        return (z + half) / height * h

    orange = np.array((0.88, 0.45, 0.13))
    wear = smooth_noise(h, w, 40, 3)
    for z0, z1, a0, a1 in bands:
        y0, y1 = int(V(z0)), int(V(z1))
        u0, u1 = U(a0), U(a0) + ((a1 - a0) / (2 * math.pi)) * w
        for x0, x1 in [(u0, min(u1, w)), (0, max(0, u1 - w))]:
            x0, x1 = int(x0), int(x1)
            if x1 <= x0:
                continue
            m = wear[y0:y1, x0:x1] > 0.38
            seg = a[y0:y1, x0:x1]
            seg[m] = seg[m] * 0.15 + orange * 0.85
    front = U(front_angle)
    for z in seams_z:
        y = int(V(z))
        paint_rect(a, 0, w, y - 1, y + 2, (0.38, 0.38, 0.37))
        paint_rect(a, 0, w, y + 2, y + 3, (0.92, 0.92, 0.90), 0.6)
        for x in np.arange(10, w, 34):
            paint_disc(a, x + 1, y + 6, 3, (0.30, 0.30, 0.30), 0.8)
            paint_disc(a, x, y + 7, 3, (0.90, 0.90, 0.88))
    for k in range(6):
        x = int(front + k * w / 6 + w / 12) % w
        paint_rect(a, x - 1, x + 2, 0, h, (0.38, 0.38, 0.37))
        for y in np.arange(14, h, 40):
            paint_disc(a, x + 7, y - 1, 3, (0.30, 0.30, 0.30), 0.8)
            paint_disc(a, x + 6, y, 3, (0.90, 0.90, 0.88))
    rust = np.array((0.45, 0.25, 0.12))
    for _ in range(rust_streaks):
        x, y = rng.integers(0, w), rng.integers(40, h)
        length, width = rng.integers(25, 90), rng.integers(2, 5)
        for k in range(length):
            paint_rect(a, x, x + width, y - k - 1, y - k, rust, 0.55 * (1 - k / length))
    edge = smooth_noise(h, w, 20, 2)
    for y in range(h):
        d = min(y, h - 1 - y) / 40.0
        if d < 1:
            m = edge[y] > 0.3 + 0.6 * d
            a[y, m] = a[y, m] * 0.4 + rust * 0.6
    return U, V, a


def metal_texture(name="metal", base=(0.62, 0.63, 0.64), rust=0.68, rust_cell=64):
    w = h = 512
    a = np.zeros((h, w, 3))
    a[:] = base
    brushed = smooth_noise(h, w, 6, 2)
    streak = np.repeat(rng.random((h, 1)), w, axis=1)
    a *= (0.88 + 0.10 * brushed + 0.06 * streak)[..., None]
    m = smooth_noise(h, w, rust_cell, 3) > rust
    a[m] = a[m] * 0.6 + np.array((0.45, 0.27, 0.14)) * 0.4
    return save_image(name, a)


def solid_texture(name, color, size=8):
    a = np.zeros((size, size, len(color)))
    a[:] = color
    return save_image(name, a)


def noise_texture(name, c0, c1, cell=16, size=256, alpha=None):
    n = smooth_noise(size, size, cell, 3)[..., None]
    a = np.array(c0) * (1 - n) + np.array(c1) * n
    if alpha is not None:
        a = np.concatenate([a, np.full((size, size, 1), alpha)], axis=2)
    return save_image(name, a)


# ------------------------------------------------------------------ matériaux

def material(name, image, shader="KSP/Diffuse", emission=0.0, alpha=None):
    """Matériau Blender (pour l'aperçu) + propriétés KSP (shader, _MainTex) pour l'export .mu."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = nodes["Principled BSDF"]
    tex = nodes.new("ShaderNodeTexImage")
    tex.name = "_MainTex"
    tex.image = image
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.55
    if emission > 0:
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = emission
    if alpha is not None:
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Roughness"].default_value = 0.05
        if hasattr(mat, "surface_render_method"):   # Blender 4.2 et plus
            mat.surface_render_method = "BLENDED"
    props = mat.mumatprop
    props.shaderName = shader
    t = props.texture.properties.add()
    t.name = "_MainTex"
    t.tex = image.name
    if shader in ("KSP/Diffuse", "KSP/Specular", "KSP/Alpha/Translucent"):
        c = props.color.properties.add()
        c.name = "_Color"
        c.value = (1.0, 1.0, 1.0, 1.0)
    return mat


# ------------------------------------------------------------------ géométrie

ROOT = None


def make_root(name):
    global ROOT
    ROOT = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(ROOT)
    return ROOT


def link(obj, mat=None, name=None):
    obj.parent = ROOT
    if name:
        obj.name = name
    if mat is not None and obj.type == "MESH":
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    return obj


def cylinder(name, radius, z0, z1, mat=None, segments=48, caps=True, location=(0, 0), rotation=None):
    """Cylindre vertical de z0 à z1 (ou orienté par rotation, centré sur location)."""
    kw = {}
    if rotation is not None:
        kw["rotation"] = rotation
        loc = location
    else:
        loc = (location[0], location[1], (z0 + z1) / 2)
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments, radius=radius, depth=abs(z1 - z0), location=loc,
                                        end_fill_type="NGON" if caps else "NOTHING", **kw)
    return link(bpy.context.active_object, mat, name)


def box(name, size, location, mat, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.scale = size
    bpy.ops.object.transform_apply(scale=True)
    return link(obj, mat, name)


def sphere(name, radius, location, mat, scale=(1, 1, 1), subdiv=2):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=location)
    obj = bpy.context.active_object
    obj.scale = scale
    bpy.ops.object.transform_apply(scale=True)
    return link(obj, mat, name)


def cylindrical_uv(obj, height):
    """
    UV d'un cylindre vertical : u = angle autour de Z, v = hauteur (0 en bas, 1 en haut).
    Les coordonnées des sommets sont relatives au centre de l'objet (cylindre centré sur son origine).
    """
    mesh = obj.data
    uv = mesh.uv_layers.active.data
    half = height / 2
    for poly in mesh.polygons:
        us = []
        for li in poly.loop_indices:
            co = mesh.vertices[mesh.loops[li].vertex_index].co
            us.append(((math.atan2(co.y, co.x) / (2 * math.pi)) % 1.0, (co.z + half) / height))
        if max(u for u, _ in us) - min(u for u, _ in us) > 0.5:
            us = [(u + 1.0 if u < 0.5 else u, v) for u, v in us]
        for li, (u, v) in zip(poly.loop_indices, us):
            uv[li].uv = (u, v)


def planar_uv(obj, axis_u=0, axis_v=2):
    """UV plane : projette les coordonnées sur deux axes, ramenées à 0..1."""
    mesh = obj.data
    cos = [v.co for v in mesh.vertices]
    mins = [min(c[i] for c in cos) for i in range(3)]
    maxs = [max(c[i] for c in cos) for i in range(3)]
    uv = mesh.uv_layers.active.data
    for loop in mesh.loops:
        c = mesh.vertices[loop.vertex_index].co
        u = (c[axis_u] - mins[axis_u]) / max(1e-6, maxs[axis_u] - mins[axis_u])
        v = (c[axis_v] - mins[axis_v]) / max(1e-6, maxs[axis_v] - mins[axis_v])
        uv[loop.index].uv = (u, v)


def wrapped_text(name, text, radius, angle, z, size, mat, font_paths=("C:/Windows/Fonts/arialbd.ttf",), depth=0.004):
    """Texte en relief enroulé sur un cylindre vertical de rayon `radius`, centré sur `angle` et `z`."""
    bpy.ops.object.text_add(location=(0, 0, 0))
    txt = bpy.context.active_object
    txt.data.body = text
    for path in font_paths:
        if os.path.exists(path):
            txt.data.font = bpy.data.fonts.load(path)
            break
    txt.data.size = size
    txt.data.extrude = depth
    txt.data.align_x = "CENTER"
    txt.data.align_y = "CENTER"
    txt.data.resolution_u = 4
    bpy.ops.object.convert(target="MESH")
    obj = bpy.context.active_object
    for v in obj.data.vertices:
        x, y, d = v.co.x, v.co.y, v.co.z
        r = radius + 0.006 + d
        theta = angle + x / radius
        v.co = Vector((r * math.cos(theta), r * math.sin(theta), z + y))
    return link(obj, mat, name)


def flat_text(name, text, location, size, mat, rotation=(math.pi / 2, 0, 0), font_paths=("C:/Windows/Fonts/arialbd.ttf",), depth=0.004):
    """Texte en relief plat (par défaut debout, face vers -Y)."""
    bpy.ops.object.text_add(location=location, rotation=rotation)
    txt = bpy.context.active_object
    txt.data.body = text
    for path in font_paths:
        if os.path.exists(path):
            txt.data.font = bpy.data.fonts.load(path)
            break
    txt.data.size = size
    txt.data.extrude = depth
    txt.data.align_x = "CENTER"
    txt.data.align_y = "CENTER"
    txt.data.resolution_u = 4
    bpy.ops.object.convert(target="MESH")
    return link(bpy.context.active_object, mat, name)


def mesh_collider(radius, z0, z1, segments=16):
    col = cylinder("collider", radius, z0, z1, None, segments)
    col.muproperties.collider = "MU_COL_MESH"
    col.muproperties.isConvex = True
    return col


def triangulate_all(skip=("collider", "airlock")):
    tris = 0
    for obj in ROOT.children:
        if obj.type == "MESH" and obj.name not in skip:
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            bmesh.ops.triangulate(bm, faces=bm.faces[:])
            bm.to_mesh(obj.data)
            bm.free()
            tris += len(obj.data.polygons)
    return tris


def export(filename):
    bpy.ops.object.select_all(action="DESELECT")
    ROOT.select_set(True)
    bpy.context.view_layer.objects.active = ROOT
    path = os.path.join(OUT_DIR, filename)
    res = bpy.ops.export_object.ksp_mu(filepath=path)
    return res, os.path.getsize(path)


def preview(path, cam_location=(1.9, -3.6, 1.3), target=(0, 0, 0), size=(900, 1000), hide=("collider", "airlock")):
    """Rendu Cycles de la pièce sur fond sombre, éclairage trois points."""
    for name in hide:
        if name in bpy.data.objects:
            bpy.data.objects[name].hide_render = True
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 48
    scene.cycles.device = "CPU"
    scene.render.resolution_x, scene.render.resolution_y = size
    world = bpy.data.worlds.new("fond")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.05, 0.06, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    scene.world = world
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = cam_location
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 50
    scene.camera = cam
    for loc, energy in [((3, -3, 4), 900), ((-4, -2, 1), 300), ((0, 4, 3), 400)]:
        ld = bpy.data.lights.new("lum", "POINT")
        ld.energy = energy
        lo = bpy.data.objects.new("lum", ld)
        lo.location = loc
        scene.collection.objects.link(lo)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
