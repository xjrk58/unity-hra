"""
The horse - and, with its horn shown, the unicorn. A small, round, friendly
pony built from metaballs; original design, generated entirely from code.

    blender -b --factory-startup -P Tools/blender/build_horse.py -- [--out DIR] [--preview DIR]

Parts (separate objects, all direct children of the root, each with its origin
on the joint it turns about; the game animates them by rotating transforms):

    Horse
      HorseBody
      HorseHead        pivot: base of the neck (the head and neck move as one)
      HorseLegFL/FR/BL/BR   pivot: top of the leg
      HorseTail        pivot: root of the tail
      HorseHorn        pivot: its base on the forehead (the game shows it on unicorns
                       only and hangs it under the head)

Material slots, addressed by name from the game: HorseBody, HorseMane,
HorseHoof, HorseEye, HorseHorn. Same export conventions as the other model
scripts: Blender -Y (the animal's front) becomes Unity +Z, Blender +Z Unity +Y.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Models"))

MATS = ["HorseBody", "HorseMane", "HorseHoof", "HorseEye", "HorseHorn"]
SURFACE = 0.575      # where the surface of a lone metaball sits, as a fraction of its radius

NECK = Vector((0.0, -0.56, 1.22))
TAIL = Vector((0.0, 0.92, 1.22))
HORN = Vector((0.0, -1.10, 1.98))


# ----------------------------------------------------------------------------
# metaballs -> mesh (shared with build_monster.py)
# ----------------------------------------------------------------------------

class Blob:
    """One metaball family, polygonised into a mesh on finish()."""
    count = 0

    def __init__(self, resolution):
        Blob.count += 1
        self.mb = bpy.data.metaballs.new("blob%d_" % Blob.count)
        self.mb.resolution = resolution
        self.mb.render_resolution = resolution
        self.mb.threshold = 0.6
        self.obj = bpy.data.objects.new("blob%d_" % Blob.count, self.mb)
        bpy.context.collection.objects.link(self.obj)

    def ball(self, p, r, negative=False, stiffness=2.0):
        e = self.mb.elements.new(type='BALL')
        e.co = p
        e.radius = r / SURFACE
        e.stiffness = stiffness
        e.use_negative = negative
        return e

    def chain(self, points, radii, step=0.55):
        """A tapering limb: balls strung along a polyline, radii interpolated."""
        for (a, b), (ra, rb) in zip(zip(points, points[1:]), zip(radii, radii[1:])):
            a, b = Vector(a), Vector(b)
            n = max(1, int((b - a).length / (min(ra, rb) * step)))
            for i in range(n):
                t = i / n
                self.ball(a.lerp(b, t), ra + (rb - ra) * t)
        self.ball(points[-1], radii[-1])

    def finish(self):
        bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(self.obj.evaluated_get(dg))
        bm = bmesh.new()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        bpy.data.objects.remove(self.obj)
        bpy.data.metaballs.remove(self.mb)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-4)
        for f in bm.faces:
            f.material_index = 0
            f.smooth = True
        return bm


def cone(base, tip, r, mat, seg=7):
    """A fang or a claw from base to tip."""
    base, tip = Vector(base), Vector(tip)
    axis = tip - base
    z = axis.normalized()
    ref = Vector((0, 0, 1)) if abs(z.z) < 0.95 else Vector((1, 0, 0))
    x = ref.cross(z).normalized()
    y = z.cross(x)
    bm = bmesh.new()
    ring = [bm.verts.new(base + (x * math.cos(2 * math.pi * i / seg) + y * math.sin(2 * math.pi * i / seg)) * r)
            for i in range(seg)]
    mid = [bm.verts.new(base + axis * 0.55 + (x * math.cos(2 * math.pi * i / seg) + y * math.sin(2 * math.pi * i / seg)) * r * 0.5)
           for i in range(seg)]
    apex = bm.verts.new(tip)
    for i in range(seg):
        j = (i + 1) % seg
        bm.faces.new([ring[i], ring[j], mid[j], mid[i]])
        bm.faces.new([mid[i], mid[j], apex])
    bm.faces.new(list(reversed(ring)))
    for f in bm.faces:
        f.material_index = MATS.index(mat)
        f.smooth = True
    return bm


def ellipsoid(centre, size, mat, rot=None, seg=14):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2 + 2, radius=1.0)
    m = Matrix.Translation(Vector(centre)) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*size, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=list(bm.verts))
    for f in bm.faces:
        f.material_index = MATS.index(mat)
        f.smooth = True
    return bm


def merge(dst, src):
    vmap = {v: dst.verts.new(v.co) for v in src.verts}
    for f in src.faces:
        nf = dst.faces.new([vmap[v] for v in f.verts])
        nf.material_index = f.material_index
        nf.smooth = f.smooth
    src.free()


def to_object(name, bm, pivot, materials, parent=None, parent_pivot=None):
    """Mesh object with its origin on the joint."""
    pivot = Vector(pivot)
    for v in bm.verts:
        v.co -= pivot
    bmesh.ops.recalc_face_normals(bm, faces=[f for f in bm.faces if f.material_index == 0])
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in MATS:
        me.materials.append(materials[m])
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    obj.location = pivot - (Vector(parent_pivot) if parent_pivot is not None else Vector())
    if parent is not None:
        obj.parent = parent
    return obj




def recolour(bm, mat):
    for f in bm.faces:
        f.material_index = MATS.index(mat)
    return bm


# ----------------------------------------------------------------------------
# the horse
# ----------------------------------------------------------------------------

def build(materials):
    # ---- barrel: deep chest, round belly, high rump
    b = Blob(0.075)
    b.ball((0.0, -0.50, 1.10), 0.36)
    b.ball((0.0, -0.10, 1.04), 0.40)
    b.ball((0.0, 0.32, 1.06), 0.39)
    b.ball((0.0, 0.66, 1.14), 0.37)
    b.ball((0.0, -0.58, 1.28), 0.24)                     # withers, where the neck starts
    body = b.finish()

    # ---- neck and head in one piece: a short thick neck, a big soft head
    h = Blob(0.055)
    h.chain([(0.0, -0.56, 1.22), (0.0, -0.78, 1.52), (0.0, -0.92, 1.76)], [0.25, 0.20, 0.17])
    h.ball((0.0, -1.00, 1.86), 0.23)                     # skull
    h.chain([(0.0, -1.08, 1.82), (0.0, -1.24, 1.70), (0.0, -1.38, 1.60)], [0.19, 0.155, 0.125])   # muzzle
    h.ball((0.085, -1.44, 1.585), 0.035, negative=True)  # nostrils
    h.ball((-0.085, -1.44, 1.585), 0.035, negative=True)
    head = h.finish()
    for s in (1, -1):
        merge(head, cone((0.10 * s, -0.94, 2.02), (0.15 * s, -0.93, 2.22), 0.065, "HorseBody", seg=8))   # ears
        merge(head, ellipsoid((0.222 * s, -1.11, 1.905), (0.035, 0.055, 0.062), "HorseEye"))
    # mane down the back of the neck, and a forelock
    m = Blob(0.05)
    m.chain([(0.0, -0.95, 2.10), (0.0, -0.80, 1.98), (0.0, -0.66, 1.76), (0.0, -0.52, 1.52), (0.0, -0.44, 1.34)],
            [0.085, 0.10, 0.105, 0.10, 0.07])
    m.chain([(0.0, -1.02, 2.08), (0.03, -1.14, 2.02), (0.05, -1.20, 1.93)], [0.075, 0.06, 0.035])
    merge(head, recolour(m.finish(), "HorseMane"))

    # ---- legs: sturdy, with a hoof
    def leg(x, y):
        l = Blob(0.06)
        l.chain([(x, y, 0.98), (x, y - 0.02, 0.55), (x, y, 0.16)], [0.135, 0.085, 0.075])
        bm = l.finish()
        hoof = bmesh.new()
        bmesh.ops.create_cone(hoof, cap_ends=True, segments=10, radius1=0.105, radius2=0.085, depth=0.16,
                              matrix=Matrix.Translation((x, y, 0.08)))
        for f in hoof.faces:
            f.material_index = MATS.index("HorseHoof")
            f.smooth = False
        merge(bm, hoof)
        return bm

    # ---- tail: thick at the root, falling in a curve
    t = Blob(0.05)
    t.chain([(0.0, 0.92, 1.22), (0.0, 1.10, 1.12), (0.0, 1.20, 0.86), (0.0, 1.22, 0.56), (0.0, 1.18, 0.34)],
            [0.085, 0.11, 0.105, 0.08, 0.03])
    tail = recolour(t.finish(), "HorseMane")

    # ---- the unicorn's horn: a long twisted cone
    horn = cone(HORN, HORN + Vector((0.0, -0.20, 0.52)), 0.05, "HorseHorn", seg=10)
    for i in range(1, 5):                                 # ridges of the spiral
        p = HORN + Vector((0.0, -0.20, 0.52)) * (i / 5.2)
        r = 0.05 * (1 - i / 5.6) + 0.012
        merge(horn, ellipsoid(p, (r, r, 0.018), "HorseHorn", Matrix.Rotation(math.radians(21), 4, 'X')))

    root = bpy.data.objects.new("Horse", None)
    bpy.context.collection.objects.link(root)
    objs = [root,
            to_object("HorseBody", body, (0, 0, 0), materials, root),
            to_object("HorseHead", head, NECK, materials, root),
            to_object("HorseLegFL", leg(0.20, -0.46), (0.20, -0.46, 0.98), materials, root),
            to_object("HorseLegFR", leg(-0.20, -0.46), (-0.20, -0.46, 0.98), materials, root),
            to_object("HorseLegBL", leg(0.20, 0.58), (0.20, 0.58, 0.98), materials, root),
            to_object("HorseLegBR", leg(-0.20, 0.58), (-0.20, 0.58, 0.98), materials, root),
            to_object("HorseTail", tail, TAIL, materials, root),
            to_object("HorseHorn", horn, HORN, materials, root)]
    return objs


# ----------------------------------------------------------------------------
# materials, export, preview
# ----------------------------------------------------------------------------

def make_mat(name, color, rough=0.5, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    if emit > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*color, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*color, 1.0)
    return m


def export_fbx(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        bake_anim=False, mesh_smooth_type='OFF', axis_forward='-Z', axis_up='Y',
        apply_scale_options='FBX_SCALE_UNITS', use_mesh_modifiers=True,
        bake_space_transform=True, path_mode='AUTO', embed_textures=False)


def tri_count(objs):
    n = 0
    for o in objs:
        if o.type == 'MESH':
            o.data.calc_loop_triangles()
            n += len(o.data.loop_triangles)
    return n




def palette(unicorn):
    if unicorn:
        return {
            "HorseBody": make_mat("HorseBody", (0.92, 0.92, 0.96), rough=0.45),
            "HorseMane": make_mat("HorseMane", (0.95, 0.35, 0.75), rough=0.5),
            "HorseHoof": make_mat("HorseHoof", (0.85, 0.70, 0.25), rough=0.3),
            "HorseEye": make_mat("HorseEye", (0.02, 0.02, 0.03), rough=0.1),
            "HorseHorn": make_mat("HorseHorn", (1.0, 0.82, 0.25), rough=0.25, emit=1.5),
        }
    return {
        "HorseBody": make_mat("HorseBody", (0.33, 0.18, 0.08), rough=0.5),
        "HorseMane": make_mat("HorseMane", (0.05, 0.03, 0.02), rough=0.6),
        "HorseHoof": make_mat("HorseHoof", (0.06, 0.05, 0.05), rough=0.4),
        "HorseEye": make_mat("HorseEye", (0.02, 0.02, 0.03), rough=0.1),
        "HorseHorn": make_mat("HorseHorn", (1.0, 0.82, 0.25), rough=0.25),
    }


def preview(objs, out, name):
    s = bpy.context.scene
    s.render.engine = 'CYCLES'
    s.cycles.device = 'CPU'
    s.cycles.samples = 64
    s.cycles.use_denoising = False
    s.render.resolution_x, s.render.resolution_y = 800, 640
    s.render.image_settings.file_format = 'PNG'
    w = bpy.data.worlds.new("Day")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.62, 0.72, 0.9, 1.0)
    s.world = w
    sun = bpy.data.lights.new("Sun", 'SUN')
    sun.energy = 3.0
    so = bpy.data.objects.new("Sun", sun)
    so.rotation_euler = (math.radians(50), 0, math.radians(35))
    s.collection.objects.link(so)
    me = bpy.data.meshes.new("Floor")
    me.from_pydata([(-9, -9, 0), (9, -9, 0), (9, 9, 0), (-9, 9, 0)], [], [(0, 1, 2, 3)])
    me.materials.append(make_mat("Floor", (0.45, 0.45, 0.43), rough=0.8))
    s.collection.objects.link(bpy.data.objects.new("Floor", me))
    cam = bpy.data.cameras.new("Cam")
    cam.lens = 50
    co = bpy.data.objects.new("Cam", cam)
    s.collection.objects.link(co)
    target = Vector((0, -0.1, 1.05))
    co.location = target + Vector((1.0, -0.95, 0.28)).normalized() * 5.4
    co.rotation_euler = (target - co.location).to_track_quat('-Z', 'Y').to_euler()
    s.camera = co
    s.render.filepath = os.path.join(out, name)
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out, prev = DEFAULT_OUT, None
    i = 0
    while i < len(argv):
        if argv[i] == "--out":
            out = argv[i + 1]; i += 2
        elif argv[i] == "--preview":
            prev = argv[i + 1]; i += 2
        else:
            raise SystemExit("unknown argument " + argv[i])
    os.makedirs(out, exist_ok=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = build(palette(False))
    path = os.path.join(out, "Horse.fbx")
    export_fbx(objs, path)
    print("[horse] total tris=%d -> %s" % (tri_count(objs), path))
    if prev:
        os.makedirs(prev, exist_ok=True)
        for o in objs:
            if o.name == "HorseHorn":
                o.hide_render = True
        preview(objs, prev, "horse.png")
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs = build(palette(True))
        preview(objs, prev, "unicorn.png")


main()
