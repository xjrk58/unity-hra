"""
The fart monster ("prdová příšera"): a hunched phantom of dense gas with a
skull-like head, burning eyes, a gaping maw full of fangs, long clawed arms and
a tail of smoke instead of legs. Original design, generated entirely from code.

    blender -b --factory-startup -P Tools/blender/build_monster.py -- [--out DIR] [--preview DIR]

The soft parts are metaballs (positive ones build the mass, negative ones carve
the eye sockets and the mouth), turned into meshes; fangs and claws are cones.

Parts (separate objects, each with its origin on the joint it turns about, so
the game can animate them by rotating transforms - there is no skeleton):

    Monster            root (on the floor, between the "feet")
      MonsterBody      tail, belly, chest, hunched back
      MonsterHead      pivot: neck
      MonsterJaw       pivot: jaw hinge     (the game hangs it under the head)
      MonsterArmL/R    pivot: shoulder

Material slots, addressed by name from the game: MonsterBody, MonsterEye,
MonsterMaw, MonsterTooth, MonsterClaw. Same export conventions as build_kit.py:
Blender -Y (the monster's front) becomes Unity +Z, Blender +Z becomes Unity +Y.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Models"))

MATS = ["MonsterBody", "MonsterEye", "MonsterMaw", "MonsterTooth", "MonsterClaw"]
SURFACE = 0.575      # where the surface of a lone metaball sits, as a fraction of its radius
                     # (threshold 0.6, stiffness 2: (1 - d^2/r^2)^3 * 2 = 0.6)

# joints
NECK = Vector((0.0, -0.28, 2.62))
HINGE = Vector((0.0, -0.60, 2.66))
SHOULDER = Vector((0.80, 0.02, 2.50))


# ----------------------------------------------------------------------------
# metaballs -> mesh
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


def lumpy(bm, rng, amount, scale):
    """Uneven, boiling surface: push the soft part in and out along its normals."""
    from mathutils import noise
    bm.normal_update()
    off = Vector((rng.uniform(0, 50), rng.uniform(0, 50), rng.uniform(0, 50)))
    for v in bm.verts:
        if all(f.material_index == 0 for f in v.link_faces):
            n = noise.noise(v.co * scale + off) + 0.5 * noise.noise(v.co * scale * 2.3 + off)
            v.co += v.normal * n * amount


# ----------------------------------------------------------------------------
# the monster
# ----------------------------------------------------------------------------

def build(materials):
    rng = random.Random(1307)

    # ---- body: ragged tails of smoke gather into a starved waist, a deep chest and a hunched back
    b = Blob(0.07)
    b.chain([(0.14, 0.40, 0.06), (0.10, 0.30, 0.34), (0.03, 0.18, 0.66), (0.0, 0.08, 1.00), (0.0, 0.04, 1.36)],
            [0.03, 0.09, 0.16, 0.22, 0.27])
    b.chain([(-0.34, 0.30, 0.30), (-0.22, 0.24, 0.62), (-0.08, 0.14, 1.00)], [0.025, 0.08, 0.15])     # side tatters
    b.chain([(0.40, 0.10, 0.52), (0.24, 0.12, 0.84), (0.08, 0.10, 1.16)], [0.02, 0.07, 0.14])
    b.ball((0.0, 0.06, 1.66), 0.30)                      # sunken belly
    b.ball((0.0, 0.02, 1.92), 0.36)
    b.ball((0.30, -0.08, 2.20), 0.42)                    # rib cage, two lobes for breadth
    b.ball((-0.30, -0.08, 2.20), 0.42)
    b.ball((0.0, 0.30, 2.46), 0.50)                      # the hump
    b.ball((0.0, 0.50, 2.10), 0.34)
    b.ball((0.70, 0.04, 2.52), 0.30)                     # shoulders
    b.ball((-0.70, 0.04, 2.52), 0.30)
    b.ball((0.0, -0.20, 2.58), 0.30)                     # root of the neck
    for x, y, z, r in [(0.26, 0.66, 2.52, 0.16), (-0.22, 0.70, 2.32, 0.14), (0.05, 0.78, 2.66, 0.13),
                       (-0.40, 0.52, 2.70, 0.15), (0.44, 0.46, 2.78, 0.12), (0.10, 0.70, 1.92, 0.14),
                       (-0.26, 0.50, 1.74, 0.12)]:
        b.ball((x, y, z), r)                             # boils on the back
    # a ridge of thin spines down the hump
    for (x, y, z), h in [((0.0, 0.52, 2.90), 0.52), ((0.0, 0.74, 2.62), 0.44), ((0.0, 0.80, 2.24), 0.34),
                         ((0.50, 0.30, 2.80), 0.36), ((-0.50, 0.30, 2.80), 0.36)]:
        b.chain([(x, y, z), (x * 1.1, y + 0.16, z + h * 0.55), (x * 1.2, y + 0.42, z + h)], [0.085, 0.045, 0.012])
    body = b.finish()
    lumpy(body, rng, 0.03, 2.8)

    # ---- head: a long skull thrust forward, heavy brow, deep sockets, the mouth torn wide open
    h = Blob(0.04)
    h.ball((0.0, -0.50, 2.98), 0.36)                     # cranium
    h.ball((0.0, -0.38, 2.74), 0.27)                     # back of the skull into the neck
    h.ball((0.0, -0.84, 2.86), 0.27)                     # face
    h.ball((0.0, -1.08, 2.80), 0.185)                    # muzzle
    h.ball((0.18, -0.86, 3.05), 0.135)                   # brow ridges
    h.ball((-0.18, -0.86, 3.05), 0.135)
    h.ball((0.0, -0.92, 3.02), 0.12)
    h.ball((0.29, -0.70, 2.79), 0.15)                    # cheekbones
    h.ball((-0.29, -0.70, 2.79), 0.15)
    h.ball((0.185, -1.00, 2.925), 0.125, negative=True)  # eye sockets
    h.ball((-0.185, -1.00, 2.925), 0.125, negative=True)
    h.ball((0.045, -1.27, 2.865), 0.05, negative=True)   # nostril slits
    h.ball((-0.045, -1.27, 2.865), 0.05, negative=True)
    h.ball((0.0, -1.02, 2.55), 0.25, negative=True)      # roof of the mouth
    h.ball((0.20, -0.84, 2.55), 0.20, negative=True)
    h.ball((-0.20, -0.84, 2.55), 0.20, negative=True)
    # horns: up from the temples, back, then hooking forward
    for s in (1, -1):
        h.chain([(0.27 * s, -0.48, 3.20), (0.40 * s, -0.30, 3.50), (0.46 * s, -0.20, 3.78), (0.40 * s, -0.34, 4.00),
                 (0.33 * s, -0.52, 4.08)], [0.10, 0.075, 0.05, 0.03, 0.010])
    head = h.finish()
    lumpy(head, rng, 0.012, 4.5)

    # eyes: slanted embers deep in the sockets
    for s in (1, -1):
        rot = Matrix.Rotation(math.radians(-30 * s), 4, 'Y')
        merge(head, ellipsoid((0.185 * s, -0.925, 2.925), (0.098, 0.035, 0.046), "MonsterEye", rot))
        merge(head, ellipsoid((0.185 * s, -0.958, 2.925), (0.013, 0.012, 0.043), "MonsterClaw"))    # slit pupil
    # glow in the throat
    merge(head, ellipsoid((0.0, -0.66, 2.60), (0.20, 0.16, 0.11), "MonsterMaw"))
    # upper fangs: needles of uneven length all along the lip, the canines longest
    for a in range(-80, 81, 10):
        ar = math.radians(a)
        canine = abs(a) == 30
        length = 0.30 if canine else rng.uniform(0.11, 0.20)
        base = Vector((math.sin(ar) * 0.25, -0.80 - math.cos(ar) * 0.37, 2.70))
        tip = base + Vector((math.sin(ar) * 0.03 + rng.uniform(-0.02, 0.02), -0.02 + rng.uniform(-0.02, 0.02), -length))
        merge(head, cone(base, tip, 0.030 if canine else rng.uniform(0.017, 0.023), "MonsterTooth"))

    # ---- lower jaw: a narrow trough with its own row of needles
    j = Blob(0.04)
    j.ball((0.0, -1.10, 2.47), 0.14)                     # chin
    j.ball((0.15, -0.98, 2.48), 0.125)
    j.ball((-0.15, -0.98, 2.48), 0.125)
    j.ball((0.23, -0.82, 2.51), 0.12)
    j.ball((-0.23, -0.82, 2.51), 0.12)
    j.ball((0.28, -0.66, 2.57), 0.115)                   # up to the hinge
    j.ball((-0.28, -0.66, 2.57), 0.115)
    j.ball((0.0, -0.96, 2.63), 0.20, negative=True)      # hollow
    j.ball((0.0, -0.76, 2.65), 0.21, negative=True)
    jaw = j.finish()
    lumpy(jaw, rng, 0.01, 4.5)
    for a in range(-75, 76, 10):
        ar = math.radians(a)
        tusk = abs(a) == 45
        length = 0.26 if tusk else rng.uniform(0.09, 0.17)
        base = Vector((math.sin(ar) * 0.215, -0.80 - math.cos(ar) * 0.325, 2.545))
        tip = base + Vector((math.sin(ar) * 0.02 + rng.uniform(-0.02, 0.02), -0.02 + rng.uniform(-0.02, 0.02), length))
        merge(jaw, cone(base, tip, 0.027 if tusk else rng.uniform(0.016, 0.021), "MonsterTooth"))

    # ---- arms: long and thin, hanging almost to the floor, with hands like rakes
    def arm(s):
        a = Blob(0.045)
        sh = Vector((0.80 * s, 0.02, 2.50))
        elbow = Vector((1.16 * s, 0.16, 1.66))
        wrist = Vector((1.02 * s, -0.44, 0.92))
        a.ball(sh, 0.20)
        a.chain([sh, elbow], [0.115, 0.085])
        a.ball(elbow, 0.115)                                             # knobbly elbow
        a.chain([elbow, wrist], [0.095, 0.115])
        hand = wrist + Vector((-0.02 * s, -0.09, -0.11))
        a.ball(hand, 0.155)
        bm = a.finish()
        lumpy(bm, rng, 0.016, 3.6)
        merge(bm, cone(elbow + Vector((0.05 * s, 0.07, 0.02)), elbow + Vector((0.17 * s, 0.40, 0.16)), 0.045, "MonsterClaw"))
        for spread, reach in [(-0.26, 0.56), (-0.085, 0.66), (0.085, 0.66), (0.26, 0.56)]:
            base = hand + Vector((spread * 0.5, -0.08, -0.06))
            tip = base + Vector((spread * 0.95 - 0.03 * s, -0.24, -reach))
            merge(bm, cone(base, tip, 0.04, "MonsterClaw", seg=8))
        merge(bm, cone(hand + Vector((-0.11 * s, 0.02, -0.02)), hand + Vector((-0.36 * s, -0.14, -0.30)), 0.036, "MonsterClaw"))  # thumb
        return bm

    root = bpy.data.objects.new("Monster", None)
    bpy.context.collection.objects.link(root)
    o_body = to_object("MonsterBody", body, (0, 0, 0), materials, root)
    o_head = to_object("MonsterHead", head, NECK, materials, root)
    # (the jaw hangs off the root in the file and is re-parented to the head by the game:
    # the exporter's axis baking only comes out right for direct children of the root)
    o_jaw = to_object("MonsterJaw", jaw, HINGE, materials, root)
    o_arm_l = to_object("MonsterArmL", arm(1), SHOULDER, materials, root)
    o_arm_r = to_object("MonsterArmR", arm(-1), Vector((-SHOULDER.x, SHOULDER.y, SHOULDER.z)), materials, root)
    return [root, o_body, o_head, o_jaw, o_arm_l, o_arm_r]


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


def palette():
    return {
        "MonsterBody": make_mat("MonsterBody", (0.030, 0.050, 0.026), rough=0.55),
        "MonsterEye": make_mat("MonsterEye", (1.0, 0.16, 0.02), emit=14.0),
        "MonsterMaw": make_mat("MonsterMaw", (0.35, 1.0, 0.08), emit=3.5),
        "MonsterTooth": make_mat("MonsterTooth", (0.80, 0.74, 0.54), rough=0.35),
        "MonsterClaw": make_mat("MonsterClaw", (0.20, 0.17, 0.11), rough=0.3),
    }


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


def preview(objs, out):
    """Renders of the monster the way it is met in the game: in the dark, lit from below."""
    s = bpy.context.scene
    s.render.engine = 'CYCLES'
    s.cycles.device = 'CPU'
    s.cycles.samples = 96
    s.cycles.use_denoising = False      # (this Blender build has no denoiser)
    s.cycles.max_bounces = 3
    s.render.resolution_x, s.render.resolution_y = 720, 900
    s.render.image_settings.file_format = 'PNG'
    w = bpy.data.worlds.new("Night")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.012, 0.02, 0.016, 1.0)
    w.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    s.world = w

    def lamp(name, loc, color, energy, size=1.5):
        d = bpy.data.lights.new(name, 'AREA')
        d.energy, d.color, d.size = energy, color, size
        o = bpy.data.objects.new(name, d)
        o.location = loc
        o.rotation_euler = (Vector((0, 0, 1.9)) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
        s.collection.objects.link(o)

    lamp("Under", (0.6, -2.2, 0.3), (0.45, 1.0, 0.25), 150)        # sickly glow from below
    lamp("Rim", (-2.6, 2.4, 3.6), (0.75, 0.85, 1.0), 420)          # cold rim light
    lamp("Fill", (2.8, -3.0, 2.6), (1.0, 0.9, 0.8), 45, 3.0)
    for o in objs:                                                  # the game never shows it with its mouth shut
        if o.name == "MonsterJaw":
            o.rotation_euler = (math.radians(26), 0, 0)

    me = bpy.data.meshes.new("Floor")
    me.from_pydata([(-9, -9, 0), (9, -9, 0), (9, 9, 0), (-9, 9, 0)], [], [(0, 1, 2, 3)])
    me.materials.append(make_mat("Floor", (0.22, 0.22, 0.21), rough=0.8))
    s.collection.objects.link(bpy.data.objects.new("Floor", me))

    def shoot(name, target, direction, dist, lens):
        cam = bpy.data.cameras.new("Cam")
        cam.lens = lens
        co = bpy.data.objects.new("Cam", cam)
        s.collection.objects.link(co)
        target = Vector(target)
        co.location = target + Vector(direction).normalized() * dist
        co.rotation_euler = (target - co.location).to_track_quat('-Z', 'Y').to_euler()
        s.camera = co
        s.render.filepath = os.path.join(out, name)
        bpy.ops.render.render(write_still=True)
        bpy.data.objects.remove(co)

    shoot("monster_front.png", (0, 0, 2.1), (0.0, -1, 0.02), 8.6, 50)
    shoot("monster_side.png", (0, -0.2, 2.1), (1, -0.25, 0.05), 8.6, 50)
    shoot("monster_face.png", (0, -0.8, 2.78), (0.22, -1, -0.18), 2.1, 35)
    # the player's view when it is upon them: eye height 1.7 m, looking up at it
    shoot("monster_victim.png", (0, -0.7, 2.6), (0.05, -1, -0.52), 2.2, 22)


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
    objs = build(palette())
    path = os.path.join(out, "Monster.fbx")
    export_fbx(objs, path)
    for o in objs:
        if o.type == 'MESH':
            o.data.calc_loop_triangles()
            print("[monster] %-12s tris=%-6d origin=(%.2f %.2f %.2f)" % (o.name, len(o.data.loop_triangles), *o.matrix_world.translation))
    print("[monster] total tris=%d -> %s" % (tri_count(objs), path))
    if prev:
        os.makedirs(prev, exist_ok=True)
        preview(objs, prev)


main()
