"""
Procedurally builds the game's character ("prdoch") and exports it for Unity.

    blender -b -P Tools/blender/build_character.py
    blender -b -P Tools/blender/build_character.py -- --render /some/dir   # + preview PNGs
    blender -b -P Tools/blender/build_character.py -- --out /tmp/Test.fbx

Everything is generated from code (no downloads, no .blend file): a chubby
cartoon humanoid in a jumpsuit with a fart-collecting backpack and glass tank.

Conventions the Unity side relies on
  * stands on Z=0, feet centred on the origin, faces -Y, 1 unit = 1 m
  * armature "Armature" with bones Root, Hips, Spine, Chest, Head,
    UpperArm/LowerArm .L/.R, UpperLeg/LowerLeg .L/.R
  * skinned mesh "Body"; rigid "Backpack", "Tank", "TankGas", "Hose" are
    bone-parented to Chest
  * "TankGas" has its origin at the bottom centre of the gas column and its
    local +Y axis pointing up, so scaling local Y from 0..1 fills the tank
  * material slot names: Skin, Suit, SuitTrim, Boot, Glove, EyeWhite, Pupil,
    Hair, PackFabric, PackMetal, TankGlass, Gas
"""
import bpy
import bmesh
import math
import os
import sys
from math import sin, cos, pi, radians, copysign
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Models", "Character.fbx"))

ARGV = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []


def arg(name, default=None):
    if name in ARGV:
        i = ARGV.index(name)
        return ARGV[i + 1] if i + 1 < len(ARGV) else default
    return default


OUT = arg("--out", DEFAULT_OUT)
RENDER_DIR = arg("--render")
UV_SCALE = 2.0  # 1 UV unit per 0.5 m


# =============================================================================
# small maths helpers
# =============================================================================
def V(x, y, z):
    return Vector((x, y, z))


def clamp01(x):
    return 0.0 if x < 0.0 else (1.0 if x > 1.0 else x)


def sstep(a, b, x):
    t = clamp01((x - a) / (b - a))
    return t * t * (3.0 - 2.0 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def linspace(a, b, n):
    return [a + (b - a) * i / (n - 1) for i in range(n)]


def catmull(ctrl, per_seg):
    """Uniform Catmull-Rom through every control tuple; returns sampled tuples."""
    pts = [ctrl[0]] + list(ctrl) + [ctrl[-1]]
    out = []
    for i in range(1, len(pts) - 2):
        p0, p1, p2, p3 = pts[i - 1], pts[i], pts[i + 1], pts[i + 2]
        for k in range(per_seg):
            t = k / per_seg
            out.append(tuple(
                0.5 * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t)
                for a, b, c, d in zip(p0, p1, p2, p3)))
    out.append(tuple(ctrl[-1]))
    return out


def catmull_v(points, per_seg):
    return [Vector(p) for p in catmull([tuple(p) for p in points], per_seg)]


def frame_from(z_axis, x_hint=None):
    """3x3 rotation whose local Z is z_axis and local X is as close to x_hint as possible."""
    z = Vector(z_axis).normalized()
    h = Vector(x_hint) if x_hint is not None else (V(1, 0, 0) if abs(z.x) < 0.9 else V(0, 1, 0))
    x = (h - z * h.dot(z)).normalized()
    y = z.cross(x)
    return Matrix((x, y, z)).transposed()


def superellipse(a, power):
    c, s = cos(a), sin(a)
    if power != 2.0:
        e = 2.0 / power
        c = copysign(abs(c) ** e, c)
        s = copysign(abs(s) ** e, s)
    return c, s


# =============================================================================
# mesh generators -- all return (verts, faces)
# =============================================================================
def loft(rings, pole0=None, pole1=None, cap0=True, cap1=True, loop=False):
    """Skin a list of equally sized vertex rings. Rings run 'upwards' with points
    counter-clockwise seen from above, which gives outward facing quads."""
    n, m = len(rings[0]), len(rings)
    verts = [Vector(v) for r in rings for v in r]
    faces = []
    for i in range(m if loop else m - 1):
        i2 = (i + 1) % m
        for j in range(n):
            j2 = (j + 1) % n
            faces.append((i * n + j, i * n + j2, i2 * n + j2, i2 * n + j))
    if not loop:
        if cap0:
            c = Vector(pole0) if pole0 is not None else sum(rings[0], Vector()) / n
            verts.append(c)
            ci = len(verts) - 1
            for j in range(n):
                faces.append((ci, (j + 1) % n, j))
        if cap1:
            c = Vector(pole1) if pole1 is not None else sum(rings[-1], Vector()) / n
            verts.append(c)
            ci = len(verts) - 1
            b = (m - 1) * n
            for j in range(n):
                faces.append((ci, b + j, b + (j + 1) % n))
    return verts, faces


def ering(cy, z, rx, ryf, ryb, n, cx=0.0, power=2.0):
    """Horizontal ring: half-width rx, front (-Y) depth ryf, back (+Y) depth ryb."""
    pts = []
    for j in range(n):
        c, s = superellipse(2 * pi * j / n, power)
        pts.append(V(cx + rx * c, cy + (ryb if s >= 0 else ryf) * s, z))
    return pts


def profile_loft(ctrl, per_seg, n, zmap=lambda z: z):
    """Body of 'revolution' from control tuples (z, rx, ry_front, ry_back, centre_y).
    First and last tuples are the poles (radius 0)."""
    samples = catmull(ctrl, per_seg)
    rings = [ering(cy, zmap(z), max(rx, 1e-4), max(ryf, 1e-4), max(ryb, 1e-4), n)
             for (z, rx, ryf, ryb, cy) in samples[1:-1]]
    p0 = V(0, samples[0][4], zmap(samples[0][0]))
    p1 = V(0, samples[-1][4], zmap(samples[-1][0]))
    return loft(rings, pole0=p0, pole1=p1)


def ellipsoid(center, radii, rot=None, nu=20, nv=12, phi0=0.0, phi1=pi):
    """UV ellipsoid, poles on local Z. phi0..phi1 limits the polar range (0 = +Z pole);
    a cut-off end is closed with a flat fan."""
    R = rot if rot is not None else Matrix.Identity(3)
    center = Vector(center)
    rings, pole0, pole1 = [], None, None
    for i in range(nv + 1):
        phi = phi1 + (phi0 - phi1) * i / nv
        if phi > pi - 1e-5:
            pole0 = center + R @ V(0, 0, -radii[2])
            continue
        if phi < 1e-5:
            pole1 = center + R @ V(0, 0, radii[2])
            continue
        rings.append([center + R @ V(radii[0] * sin(phi) * cos(2 * pi * j / nu),
                                     radii[1] * sin(phi) * sin(2 * pi * j / nu),
                                     radii[2] * cos(phi)) for j in range(nu)])
    return loft(rings, pole0=pole0, pole1=pole1)


def sweep(path, ra, rb=None, n=10, normals=None, power=2.0, loop=False, caps=True):
    """Sweep a (super)elliptic section along a path. ra = half size along the
    normal, rb = half size along the binormal (floats or per-point lists)."""
    m = len(path)
    ra = list(ra) if isinstance(ra, (list, tuple)) else [ra] * m
    rb = ra if rb is None else (list(rb) if isinstance(rb, (list, tuple)) else [rb] * m)
    T = []
    for i in range(m):
        if loop:
            t = path[(i + 1) % m] - path[(i - 1) % m]
        else:
            t = path[min(i + 1, m - 1)] - path[max(i - 1, 0)]
        T.append(t.normalized())
    N = []
    if normals is None:
        h = V(0, 0, 1) if abs(T[0].z) < 0.9 else V(1, 0, 0)
        nrm = (h - T[0] * h.dot(T[0])).normalized()
        for i in range(m):
            nrm = (nrm - T[i] * nrm.dot(T[i])).normalized()
            N.append(nrm.copy())
    else:
        for i in range(m):
            nn = Vector(normals[i])
            N.append((nn - T[i] * nn.dot(T[i])).normalized())
    rings = []
    for i in range(m):
        B = T[i].cross(N[i])
        rings.append([path[i] + N[i] * (ra[i] * c) + B * (rb[i] * s)
                      for c, s in (superellipse(2 * pi * k / n, power) for k in range(n))])
    return loft(rings, cap0=caps, cap1=caps, loop=loop)


def lathe(origin, axis, profile, n=24, x_hint=None, cap0=True, cap1=True):
    """Revolve a profile [(height, radius), ...] around an arbitrary axis.
    A radius of 0 at either end becomes a pole."""
    R = frame_from(axis, x_hint)
    origin = Vector(origin)
    rings, pole0, pole1 = [], None, None
    for idx, (h, r) in enumerate(profile):
        if r <= 1e-6:
            p = origin + R @ V(0, 0, h)
            if idx == 0:
                pole0 = p
            else:
                pole1 = p
            continue
        rings.append([origin + R @ V(r * cos(2 * pi * j / n), r * sin(2 * pi * j / n), h) for j in range(n)])
    return loft(rings, pole0=pole0, pole1=pole1, cap0=cap0, cap1=cap1)


def rbox(center, size, bevel=0.01, seg=3, rot=None):
    """Box with rounded edges."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = V(v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type='OFFSET',
                        segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    bm.verts.index_update()
    center = Vector(center)
    verts = [(rot @ v.co if rot is not None else v.co.copy()) + center for v in bm.verts]
    faces = [tuple(v.index for v in f.verts) for f in bm.faces]
    bm.free()
    return verts, faces


def fix_normals(verts, faces):
    """Make face winding consistent and outward facing (for closed shapes)."""
    bm = bmesh.new()
    bv = [bm.verts.new(v) for v in verts]
    bf = []
    for f in faces:
        try:
            bf.append(bm.faces.new([bv[i] for i in f]))
        except ValueError:
            pass
    bm.normal_update()
    bmesh.ops.recalc_face_normals(bm, faces=bf)
    bm.verts.index_update()
    out = [tuple(v.index for v in f.verts) for f in bf]
    bm.free()
    return out


def fixed(vf):
    return vf[0], fix_normals(vf[0], vf[1])


def make_bvh(*vfs):
    verts, faces = [], []
    for v, f in vfs:
        base = len(verts)
        verts += [tuple(p) for p in v]
        faces += [tuple(base + i for i in face) for face in f]
    return BVHTree.FromPolygons(verts, faces)


def patch(bvh, cx, cz, w, h, lift, edge=0.012, corner=0.016, nx=14, nz=12, rot=0.0):
    """Pillow-shaped patch (pocket, flap) projected along +Y onto a surface."""
    verts, faces = [], []
    cr, sr = cos(rot), sin(rot)
    for iz in range(nz + 1):
        for ix in range(nx + 1):
            u = -w / 2 + w * ix / nx
            v = -h / 2 + h * iz / nz
            qx = abs(u) - (w / 2 - corner)
            qy = abs(v) - (h / 2 - corner)
            sdf = math.hypot(max(qx, 0), max(qy, 0)) + min(max(qx, qy), 0) - corner
            x = cx + u * cr - v * sr
            z = cz + u * sr + v * cr
            loc, nrm, _, _ = bvh.ray_cast(V(x, -3, z), V(0, 1, 0))
            if loc is None:
                loc, nrm = V(x, 0, z), V(0, -1, 0)
            d = -sdf
            l = -0.004 if d <= 0 else lift * (1 - (1 - min(d / edge, 1.0)) ** 2)
            verts.append(loc + nrm * l)
    row = nx + 1
    for iz in range(nz):
        for ix in range(nx):
            i = iz * row + ix
            faces.append((i, i + 1, i + 1 + row, i + row))
    return verts, faces


# =============================================================================
# materials (preview colours only -- Unity swaps them by name)
# =============================================================================
MATS = {
    'Skin': dict(color=(0.87, 0.63, 0.50), rough=0.55, sss=0.12),
    'Suit': dict(color=(0.70, 0.70, 0.70), rough=0.85),
    'SuitTrim': dict(color=(0.085, 0.09, 0.105), rough=0.7),
    'Boot': dict(color=(0.21, 0.11, 0.055), rough=0.45),
    'Glove': dict(color=(0.63, 0.44, 0.21), rough=0.6),
    'EyeWhite': dict(color=(0.95, 0.95, 0.93), rough=0.12),
    'Pupil': dict(color=(0.012, 0.012, 0.016), rough=0.08),
    'Hair': dict(color=(0.15, 0.08, 0.04), rough=0.6),
    'PackFabric': dict(color=(0.42, 0.37, 0.22), rough=0.9),
    'PackMetal': dict(color=(0.86, 0.63, 0.27), rough=0.3, metal=1.0),
    'TankGlass': dict(color=(0.92, 0.98, 0.96), rough=0.03, trans=1.0),
    'Gas': dict(color=(0.45, 0.85, 0.15), rough=0.6, emit=1.2),
}


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    spec = MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    col = (*spec['color'], 1.0)
    m.diffuse_color = col
    b = m.node_tree.nodes.get('Principled BSDF')

    def setin(key, val):
        if key in b.inputs:
            b.inputs[key].default_value = val

    setin('Base Color', col)
    setin('Roughness', spec.get('rough', 0.5))
    setin('Metallic', spec.get('metal', 0.0))
    if 'sss' in spec:
        setin('Subsurface Weight', spec['sss'])
        setin('Subsurface Radius', (0.02, 0.008, 0.004))
    if 'trans' in spec:
        setin('Transmission Weight', spec['trans'])
        setin('IOR', 1.45)
    if 'emit' in spec:
        setin('Emission Color', col)
        setin('Emission Strength', spec['emit'])
    return m


# =============================================================================
# mesh accumulator
# =============================================================================
class MeshAcc:
    """Collects parts (with materials and optional skin weights) into one object."""

    def __init__(self, name, smooth_angle=60.0):
        self.name = name
        self.verts, self.faces, self.fmat, self.w = [], [], [], []
        self.smooth_angle = smooth_angle

    def add(self, vf, mat, w=None, recalc=True):
        verts, faces = vf
        if recalc:
            faces = fix_normals(verts, faces)
        base = len(self.verts)
        for v in verts:
            v = Vector(v)
            self.verts.append(v)
            if w is None:
                self.w.append(None)
            else:
                ww = w(v) if callable(w) else w
                tot = sum(x for x in ww.values() if x > 1e-4)
                self.w.append({k: x / tot for k, x in ww.items() if x > 1e-4})
        for f in faces:
            self.faces.append(tuple(base + i for i in f))
            if callable(mat):
                self.fmat.append(mat(sum((Vector(verts[i]) for i in f), Vector()) / len(f)))
            else:
                self.fmat.append(mat)
        return verts, faces

    def tris(self):
        return sum(len(f) - 2 for f in self.faces)

    def build(self):
        me = bpy.data.meshes.new(self.name)
        me.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        names = []
        for m in self.fmat:
            if m not in names:
                names.append(m)
        for nm in names:
            me.materials.append(get_mat(nm))
        me.polygons.foreach_set('material_index', [names.index(m) for m in self.fmat])
        me.polygons.foreach_set('use_smooth', [True] * len(me.polygons))
        me.update()

        # box-projected UVs, constant texel density
        uv = me.uv_layers.new(name='UVMap')
        for poly in me.polygons:
            nrm = poly.normal
            ax = max(range(3), key=lambda i: abs(nrm[i]))
            for li in poly.loop_indices:
                co = me.vertices[me.loops[li].vertex_index].co
                if ax == 0:
                    u, v = co.y, co.z
                elif ax == 1:
                    u, v = co.x, co.z
                else:
                    u, v = co.x, co.y
                uv.data[li].uv = (u * UV_SCALE, v * UV_SCALE)

        if hasattr(me, 'use_auto_smooth'):
            me.use_auto_smooth = True
            me.auto_smooth_angle = radians(self.smooth_angle)

        ob = bpy.data.objects.new(self.name, me)
        bpy.context.scene.collection.objects.link(ob)
        if any(w is not None for w in self.w):
            groups = {}
            for i, w in enumerate(self.w):
                for bone, val in w.items():
                    g = groups.get(bone)
                    if g is None:
                        g = groups[bone] = ob.vertex_groups.new(name=bone)
                    g.add([i], val, 'REPLACE')
        return ob


# =============================================================================
# character dimensions
# =============================================================================
YC = -0.02                                   # y of the spine line
HIP_Z, SPINE_Z, CHEST_Z, NECK_Z = 0.52, 0.62, 0.74, 1.12
LEG_X, LEG_Y, KNEE_Z, ANKLE_Z = 0.135, -0.03, 0.33, 0.10
SHOULDER = V(0.215, YC, 1.03)
ARM_ANGLE = radians(57)                      # below horizontal (A-pose)
ARM_UP, ARM_LO = 0.19, 0.155
HZ0, HH = 1.14, 0.56                         # head bottom, head height


def arm_dir(sx):
    return V(sx * cos(ARM_ANGLE), 0, -sin(ARM_ANGLE))


def hz(t):
    return HZ0 + t * HH


# (z, half width, front depth, back depth, centre y)
TORSO = [
    (0.440, 0.000, 0.000, 0.000, -0.030),
    (0.447, 0.110, 0.100, 0.085, -0.030),
    (0.470, 0.215, 0.200, 0.150, -0.030),
    (0.520, 0.302, 0.292, 0.190, -0.030),
    (0.590, 0.342, 0.332, 0.205, -0.028),
    (0.665, 0.340, 0.330, 0.203, -0.026),
    (0.750, 0.314, 0.296, 0.192, -0.024),
    (0.838, 0.280, 0.246, 0.180, -0.022),
    (0.925, 0.248, 0.200, 0.168, -0.020),
    (1.003, 0.218, 0.175, 0.152, -0.020),
    (1.062, 0.185, 0.150, 0.132, -0.020),
    (1.100, 0.135, 0.112, 0.100, -0.020),
    (1.118, 0.000, 0.000, 0.000, -0.020),
]

# (t along head height, half width, front depth, back depth, centre y)
HEAD = [
    (0.000, 0.000, 0.000, 0.000, -0.035),
    (0.015, 0.100, 0.095, 0.085, -0.035),
    (0.080, 0.205, 0.195, 0.170, -0.035),
    (0.190, 0.265, 0.250, 0.212, -0.034),
    (0.320, 0.287, 0.268, 0.236, -0.032),
    (0.460, 0.283, 0.266, 0.246, -0.030),
    (0.600, 0.262, 0.250, 0.244, -0.028),
    (0.740, 0.225, 0.218, 0.224, -0.026),
    (0.860, 0.170, 0.168, 0.176, -0.025),
    (0.950, 0.098, 0.098, 0.104, -0.025),
    (1.000, 0.000, 0.000, 0.000, -0.025),
]

W_HEAD = {'Head': 1.0}
W_CHEST = {'Chest': 1.0}


def w_torso(p):
    wh = 1.0 - sstep(0.52, 0.66, p.z)
    wc = sstep(0.66, 0.80, p.z)
    return {'Hips': wh, 'Spine': max(0.0, 1.0 - wh - wc), 'Chest': wc}


def w_arm(S, D, side):
    def f(p):
        wl = sstep(ARM_UP - 0.035, ARM_UP + 0.035, (p - S).dot(D))
        return {'UpperArm.' + side: 1.0 - wl, 'LowerArm.' + side: wl}
    return f


def w_leg(side):
    def f(p):
        wl = 1.0 - sstep(KNEE_Z - 0.03, KNEE_Z + 0.03, p.z)
        return {'UpperLeg.' + side: 1.0 - wl, 'LowerLeg.' + side: wl}
    return f


# =============================================================================
# body
# =============================================================================
def build_body():
    B = MeshAcc('Body', smooth_angle=62)

    # ---- torso ------------------------------------------------------------
    torso = fixed(profile_loft(TORSO, 4, 48))
    B.add(torso, 'Suit', w_torso, recalc=False)
    torso_bvh = make_bvh(torso)

    def front(x, z):
        loc, nrm, _, _ = torso_bvh.ray_cast(V(x, -3, z), V(0, 1, 0))
        return loc, nrm

    balls = []
    for sx, side in ((1, 'L'), (-1, 'R')):
        S = V(sx * SHOULDER.x, SHOULDER.y, SHOULDER.z)
        D = arm_dir(sx)
        wa = w_arm(S, D, side)
        W_UP = {'UpperArm.' + side: 1.0}
        W_LO = {'LowerArm.' + side: 1.0}
        fwd = V(0, -1, 0)

        # shoulder ball + sleeve
        ball = fixed(ellipsoid(S, (0.082, 0.082, 0.082), nu=22, nv=12))
        balls.append(ball)
        B.add(ball, 'Suit', W_UP, recalc=False)
        ts = linspace(-0.02, 0.335, 16)
        B.add(sweep([S + D * t for t in ts], [lerp(0.074, 0.060, clamp01(t / 0.335)) for t in ts], n=20), 'Suit', wa)
        # sleeve cuff
        B.add(lathe(S + D * 0.296, D, [(0, 0.056), (0.004, 0.066), (0.012, 0.070), (0.030, 0.070),
                                        (0.038, 0.066), (0.042, 0.054)], n=20), 'SuitTrim', W_LO)
        # glove: wrist band, mitten, thumb
        R = frame_from(D, fwd.cross(D))
        B.add(lathe(S + D * 0.326, D, [(0, 0.048), (0.003, 0.062), (0.012, 0.0645), (0.034, 0.057),
                                        (0.05, 0.048)], n=18), 'Glove', W_LO)
        B.add(ellipsoid(S + D * 0.415, (0.040, 0.057, 0.078), rot=R, nu=18, nv=12), 'Glove', W_LO)
        Rt = frame_from((D * 0.72 + fwd * 0.69).normalized(), fwd.cross(D))
        B.add(ellipsoid(S + D * 0.388 + fwd * 0.050, (0.021, 0.022, 0.040), rot=Rt, nu=12, nv=8), 'Glove', W_LO)

        # ---- leg + boot ---------------------------------------------------
        L = V(sx * LEG_X, LEG_Y, 0)
        W_LL = {'LowerLeg.' + side: 1.0}
        zs = linspace(0.60, 0.20, 11)
        B.add(sweep([V(L.x, L.y, z) for z in zs], [lerp(0.120, 0.094, sstep(0.60, 0.27, z)) for z in zs], n=24),
              'Suit', w_leg(side))

        def sole_ring(z, sc, n=28):
            pts = []
            for j in range(n):
                c, s = superellipse(2 * pi * j / n, 2.5)
                pts.append(L + V((0.097 - 0.013 * s) * sc * c, -0.062 + 0.172 * sc * s, z))
            return pts

        B.add(loft([sole_ring(0.0, 0.95), sole_ring(0.007, 1.0), sole_ring(0.038, 1.0), sole_ring(0.047, 0.97)]),
              'SuitTrim', W_LL)
        B.add(ellipsoid(L + V(0, -0.125, 0.04), (0.094, 0.105, 0.088), nu=22, nv=7, phi1=pi / 2), 'Boot', W_LL)
        B.add(ellipsoid(L + V(0, -0.020, 0.04), (0.090, 0.125, 0.125), nu=22, nv=7, phi1=pi / 2), 'Boot', W_LL)
        B.add(lathe(L + V(0, 0.008, 0.03), V(0, 0, 1),
                    [(0.0, 0.094), (0.10, 0.098), (0.17, 0.103), (0.205, 0.110), (0.222, 0.117),
                     (0.232, 0.118), (0.239, 0.114), (0.238, 0.107), (0.205, 0.078)], n=24), 'Boot', W_LL)
        # ankle strap with a brass buckle on the outside
        n = 32
        sc = L + V(0, 0.008, 0.160)
        B.add(sweep([sc + V(0.1035 * cos(2 * pi * j / n), 0.1035 * sin(2 * pi * j / n), 0) for j in range(n)],
                    0.0045, 0.014, n=8, normals=[V(cos(2 * pi * j / n), sin(2 * pi * j / n), 0) for j in range(n)],
                    power=5.0, loop=True), 'SuitTrim', W_LL)
        B.add(rbox(sc + V(sx * 0.109, -0.010, 0), (0.012, 0.038, 0.038), bevel=0.004, seg=2), 'PackMetal', W_LL)

    # ---- collar, belt, zip, pocket ----------------------------------------
    n = 40
    path = [V(0.131 * cos(2 * pi * j / n), -0.022 + 0.117 * sin(2 * pi * j / n), 1.108) for j in range(n)]
    nrms = [V(cos(2 * pi * j / n), sin(2 * pi * j / n), 0) for j in range(n)]
    B.add(sweep(path, 0.026, 0.040, n=12, normals=nrms, power=2.6, loop=True), 'SuitTrim', W_CHEST)

    belt_z = 0.63
    best = min(catmull(TORSO, 16), key=lambda s: abs(s[0] - belt_z))
    rx, ryf, ryb, cy = best[1], best[2], best[3], best[4]
    n = 56
    path, nrms = [], []
    for j in range(n):
        c, s = cos(2 * pi * j / n), sin(2 * pi * j / n)
        ry = ryb if s >= 0 else ryf
        nn = V(c / rx, s / ry, 0).normalized()
        path.append(V(rx * c, cy + ry * s, belt_z) + nn * 0.006)
        nrms.append(nn)
    B.add(sweep(path, 0.010, 0.027, n=10, normals=nrms, power=5.0, loop=True), 'SuitTrim', w_torso(V(0, 0, belt_z)))
    by = cy - ryf
    wb = w_torso(V(0, 0, belt_z))
    B.add(rbox(V(0, by - 0.017, belt_z), (0.092, 0.022, 0.072), bevel=0.009, seg=3), 'PackMetal', wb)
    B.add(rbox(V(0, by - 0.021, belt_z), (0.058, 0.020, 0.040), bevel=0.006, seg=2), 'SuitTrim', wb)

    # zip down the front
    zs = linspace(1.07, 0.665, 26)
    hits = [front(0.0, z) for z in zs]
    B.add(sweep([h[0] + h[1] * 0.002 for h in hits], 0.0045, 0.009, n=8, normals=[h[1] for h in hits], power=4.0),
          'SuitTrim', w_torso)
    loc, nrm = front(0.0, 1.045)
    B.add(rbox(loc + nrm * 0.008 + V(0, 0, -0.012), (0.017, 0.008, 0.038), bevel=0.003, seg=2), 'PackMetal', W_CHEST)

    # chest pocket (character's left) with flap and brass stud
    B.add(patch(torso_bvh, 0.074, 0.895, 0.088, 0.088, 0.0075), 'Suit', w_torso, recalc=False)
    B.add(patch(torso_bvh, 0.074, 0.932, 0.096, 0.036, 0.011, edge=0.009, corner=0.012, nx=14, nz=8),
          'SuitTrim', w_torso, recalc=False)
    loc, nrm = front(0.074, 0.926)
    B.add(ellipsoid(loc + nrm * 0.011, (0.008, 0.008, 0.008), nu=10, nv=6), 'PackMetal', W_CHEST)

    # ---- head -------------------------------------------------------------
    head = fixed(profile_loft(HEAD, 4, 48, zmap=hz))
    B.add(head, 'Skin', W_HEAD, recalc=False)
    head_bvh = make_bvh(head)

    def face(x, z):
        loc, nrm, _, _ = head_bvh.ray_cast(V(x, -3, z), V(0, 1, 0))
        return loc, nrm

    # eyes: (side, upper lid edge elevation, lid roll)
    EYE_R = 0.064
    eye_z = hz(0.565)
    for sx, lid_e, in ((1, 30.0), (-1, 9.0)):
        loc, nrm = face(sx * 0.103, eye_z)
        c = loc - nrm * 0.030
        gaze = V(-sx * 0.10, -1, -0.03).normalized()
        B.add(ellipsoid(c, (EYE_R,) * 3, rot=frame_from(gaze, V(1, 0, 0)), nu=24, nv=14),
              (lambda cen, c=c, g=gaze: 'Pupil' if (cen - c).normalized().dot(g) > cos(radians(27)) else 'EyeWhite'),
              W_HEAD)
        # upper lid: pole tilted back 14 deg and rolled so the edge dips toward the nose
        beta = 14.0
        pole = V(-sx * 0.16, sin(radians(beta)), cos(radians(beta))).normalized()
        B.add(ellipsoid(c, (EYE_R + 0.0045,) * 3, rot=frame_from(pole, V(1, 0, 0)), nu=24, nv=8,
                        phi1=radians(90 - lid_e + beta)), 'Skin', W_HEAD)
        # lower lid
        pole = V(0, 0.2, -1).normalized()
        B.add(ellipsoid(c, (EYE_R + 0.004,) * 3, rot=frame_from(pole, V(1, 0, 0)), nu=24, nv=6,
                        phi1=radians(57)), 'Skin', W_HEAD)

    # brows: left one raised and arched, right one low and straight
    for sx, zb, arch, slope in ((1, hz(0.775), 0.022, -0.010), (-1, hz(0.705), 0.006, 0.016)):
        pts, nr, ra, rb = [], [], [], []
        for u in linspace(-1, 1, 11):
            x = sx * 0.103 + 0.078 * u
            inner = -u * sx                      # +1 at the nose end
            z = zb + arch * (1 - u * u) - slope * inner
            loc, nrm = face(x, z)
            pts.append(loc + nrm * 0.006)
            nr.append(nrm)
            k = 0.35 + 0.65 * (1 - u * u) ** 0.5
            ra.append(0.011 * k)
            rb.append(0.019 * k)
        B.add(sweep(pts, ra, rb, n=10, normals=nr), 'Hair', W_HEAD)

    # nose: bridge, bulb, nostril wings
    loc, nrm = face(0, hz(0.415))
    ny = loc.y
    B.add(ellipsoid(V(0, ny - 0.034, hz(0.415)), (0.070, 0.080, 0.064), nu=26, nv=16), 'Skin', W_HEAD)
    B.add(ellipsoid(V(0, ny + 0.004, hz(0.52)), (0.036, 0.040, 0.062), nu=16, nv=10), 'Skin', W_HEAD)
    for sx in (1, -1):
        B.add(ellipsoid(V(sx * 0.052, ny - 0.008, hz(0.395)), (0.036, 0.040, 0.031), nu=16, nv=10), 'Skin', W_HEAD)

    # mouth: closed smirk rising on the character's left, plus a cheek bulge
    pts, nr = [], []
    for u in linspace(0, 1, 17):
        x = lerp(-0.105, 0.138, u)
        z = hz(0.238) - 0.006 * sin(u * pi) + 0.040 * sstep(0.40, 1.0, u) ** 1.4
        loc, nrm = face(x, z)
        pts.append(loc + nrm * 0.0015)
        nr.append(nrm)
    tap = [0.45 + 0.55 * sin(pi * clamp01(u * 1.1)) ** 0.5 for u in linspace(0, 1, 17)]
    B.add(sweep(pts, [0.0055 * t for t in tap], [0.008 * t for t in tap], n=8, normals=nr), 'Hair', W_HEAD)
    loc, nrm = face(0.170, hz(0.335))
    B.add(ellipsoid(loc - nrm * 0.046, (0.064, 0.064, 0.056), nu=18, nv=12), 'Skin', W_HEAD)
    loc, nrm = face(0.012, hz(0.205))
    B.add(ellipsoid(loc - nrm * 0.016, (0.058, 0.026, 0.021), nu=16, nv=8), 'Skin', W_HEAD)

    # ears
    for sx in (1, -1):
        loc, nrm, _, _ = head_bvh.ray_cast(V(sx * 3, -0.015, hz(0.47)), V(-sx, 0, 0))
        rot = Matrix.Rotation(radians(-sx * 32), 3, 'Z')
        ec = loc + V(sx * 0.010, 0.012, 0)
        B.add(ellipsoid(ec, (0.013, 0.044, 0.063), rot=rot, nu=18, nv=10), 'Skin', W_HEAD)
        n = 22
        rim = [ec + rot @ V(sx * 0.006, 0.039 * cos(2 * pi * j / n), 0.058 * sin(2 * pi * j / n)) for j in range(n)]
        B.add(sweep(rim, 0.010, 0.0105, n=8, normals=[rot @ V(sx, 0, 0)] * n, loop=True), 'Skin', W_HEAD)
        B.add(ellipsoid(ec + rot @ V(sx * 0.008, -0.026, -0.006), (0.010, 0.012, 0.016), rot=rot, nu=10, nv=6),
              'Skin', W_HEAD)

    # horseshoe of combed hair round the back of the head, temple to temple
    nt, nv = 72, 9
    verts, faces = [], []
    for iv in range(nv + 1):
        v = iv / nv
        for it in range(nt + 1):
            u = it / nt
            th = radians(lerp(-12, 192, u))
            back = max(0.0, sin(th))
            t_lo = 0.330 - 0.165 * back ** 1.5 + 0.012 * sin(th * 9.0)
            t_hi = 0.535 + 0.135 * back ** 2 + 0.016 * sin(th * 7.0 + 1.0)
            z = hz(lerp(t_lo, t_hi, v))
            d = V(cos(th), sin(th), 0)
            loc, nrm, _, _ = head_bvh.ray_cast(V(0, -0.03, z) + d * 2.0, -d)
            e = (sstep(0.0, 0.07, min(u, 1 - u)) * sstep(0.0, 0.22, min(v, 1 - v))) ** 0.5
            comb = 0.72 + 0.28 * sin(th * 22.0 + 2.5 * v)
            edge = it in (0, nt) or iv in (0, nv)
            verts.append(loc + nrm * (-0.004 if edge else 0.003 + 0.012 * e * comb))
    row = nt + 1
    for iv in range(nv):
        for it in range(nt):
            i = iv * row + it
            faces.append((i, i + 1, i + 1 + row, i + row))
    B.add((verts, faces), 'Hair', W_HEAD, recalc=False)

    # hair tuft
    top = V(0, -0.028, hz(1.0) - 0.008)
    strands = [
        [(0, 0, 0), (0.004, -0.012, 0.035), (0.022, -0.036, 0.062), (0.050, -0.048, 0.060), (0.062, -0.040, 0.040)],
        [(0.008, 0.010, 0), (0.000, 0.022, 0.034), (-0.022, 0.040, 0.058), (-0.048, 0.044, 0.056), (-0.060, 0.034, 0.038)],
        [(-0.010, -0.004, 0), (-0.016, -0.004, 0.030), (-0.020, -0.012, 0.055), (-0.010, -0.028, 0.068), (0.004, -0.034, 0.060)],
    ]
    for st in strands:
        pts = catmull_v([top + Vector(p) for p in st], 5)
        m = len(pts)
        B.add(sweep(pts, [lerp(0.013, 0.003, i / (m - 1)) for i in range(m)], n=8), 'Hair', W_HEAD)

    return B, torso, balls


# =============================================================================
# backpack, tank, gas, hose
# =============================================================================
TANK_Y = 0.290
TANK_R = 0.112
GAS_Z0, GAS_Z1, GAS_R = 0.672, 0.978, 0.104


def build_pack(torso, balls):
    P = MeshAcc('Backpack', smooth_angle=50)
    T = MeshAcc('Tank', smooth_angle=50)
    G = MeshAcc('TankGas', smooth_angle=50)
    H = MeshAcc('Hose', smooth_angle=50)
    body_bvh = make_bvh(torso, *balls)

    def snap(g):
        """Outermost point of the body along the ray from the spine line through g."""
        g = Vector(g)
        c = V(0, YC, min(max(g.z, 0.60), 0.95))
        d = (g - c).normalized()
        loc, nrm, _, _ = body_bvh.ray_cast(c + d * 3.0, -d)
        return (loc, nrm) if loc is not None else (g, d)

    # ---- pack body, bedroll, side pouch -------------------------------------
    P.add(rbox(V(0, 0.175, 0.82), (0.37, 0.13, 0.44), bevel=0.035, seg=4), 'PackFabric')
    P.add(rbox(V(0, 0.178, 1.018), (0.378, 0.142, 0.052), bevel=0.022, seg=3), 'PackFabric')
    P.add(lathe(V(-0.17, 0.185, 1.085), V(1, 0, 0),
                [(0, 0), (0.004, 0.028), (0.012, 0.042), (0.03, 0.046), (0.31, 0.046), (0.328, 0.042),
                 (0.336, 0.028), (0.34, 0)], n=20), 'PackFabric')
    for x in (-0.085, 0.085):
        P.add(lathe(V(x - 0.011, 0.185, 1.085), V(1, 0, 0),
                    [(0, 0.040), (0, 0.0495), (0.022, 0.0495), (0.022, 0.040)], n=20), 'Boot')
        P.add(rbox(V(x, 0.185 + 0.049, 1.085), (0.028, 0.008, 0.026), bevel=0.003, seg=2), 'PackMetal')
    P.add(rbox(V(0.203, 0.175, 0.715), (0.062, 0.108, 0.165), bevel=0.022, seg=3), 'PackFabric')
    P.add(rbox(V(0.208, 0.175, 0.782), (0.062, 0.116, 0.060), bevel=0.020, seg=3), 'Boot')
    P.add(ellipsoid(V(0.241, 0.175, 0.770), (0.008, 0.009, 0.009), nu=10, nv=6), 'PackMetal')

    # ---- leather bands cradling the tank ------------------------------------
    for z in (0.745, 0.905):
        angs = linspace(radians(-18), radians(198), 30)
        path = [V(-(TANK_R + 0.0035) * cos(a), TANK_Y + (TANK_R + 0.0035) * sin(a), z) for a in angs]
        nrms = [V(-cos(a), sin(a), 0) for a in angs]
        P.add(sweep(path, 0.0035, 0.012, n=8, normals=nrms, power=5.0), 'Boot')
        P.add(rbox(V(0, TANK_Y + TANK_R + 0.007, z), (0.034, 0.008, 0.032), bevel=0.003, seg=2), 'PackMetal')

    # ---- shoulder straps -------------------------------------
    for sx in (1, -1):
        guide = [(0.150, 0.150, 1.02), (0.178, 0.085, 1.09), (0.184, 0.010, 1.115), (0.180, -0.070, 1.085),
                 (0.166, -0.135, 1.02), (0.153, -0.175, 0.95), (0.150, -0.200, 0.88), (0.175, -0.205, 0.82),
                 (0.235, -0.160, 0.775), (0.285, -0.070, 0.75), (0.300, 0.030, 0.735), (0.265, 0.110, 0.72),
                 (0.200, 0.160, 0.71)]
        dense = catmull_v([(sx * x, y, z) for x, y, z in guide], 6)
        hits = [snap(p) for p in dense]
        pts = [h[0] + h[1] * 0.008 for h in hits]
        nr = [h[1] for h in hits]
        for _ in range(3):                       # relax kinks where torso meets shoulder
            pts = [pts[0]] + [(pts[i - 1] + pts[i] * 2 + pts[i + 1]) / 4 for i in range(1, len(pts) - 1)] + [pts[-1]]
            nr = [nr[0]] + [(nr[i - 1] + nr[i] * 2 + nr[i + 1]).normalized() for i in range(1, len(nr) - 1)] + [nr[-1]]
        P.add(sweep(pts, 0.0055, 0.021, n=8, normals=nr, power=5.0), 'Boot')
        loc, nrm = snap((sx * 0.151, -0.19, 0.915))
        P.add(rbox(loc + nrm * 0.014, (0.050, 0.010, 0.034), bevel=0.004, seg=2,
                   rot=frame_from(V(0, 0, 1), nrm.cross(V(0, 0, 1)))), 'PackMetal')
    # ---- glass tank with brass caps and valve ---------------------------------
    R, o = TANK_R, V(0, TANK_Y, 0)
    T.add(lathe(o, V(0, 0, 1), [(0.612, 0), (0.619, 0.050), (0.635, 0.090), (0.655, 0.108), (0.677, R),
                                (0.973, R), (0.995, 0.108), (1.015, 0.090), (1.031, 0.050), (1.038, 0)], n=32),
          'TankGlass')
    cap = [(0.0, 0), (0.002, 0.062), (0.014, 0.099), (0.034, 0.1165), (0.074, 0.119), (0.078, 0.1215),
           (0.086, 0.1215), (0.089, 0.108)]
    T.add(lathe(V(0, TANK_Y, 0.602), V(0, 0, 1), cap, n=32, cap1=False), 'PackMetal')
    T.add(lathe(V(0, TANK_Y, 1.048), V(0, 0, -1), cap, n=32, cap1=False), 'PackMetal')
    T.add(lathe(V(0, TANK_Y, 1.044), V(0, 0, 1), [(0, 0.024), (0.012, 0.024), (0.014, 0.014), (0.028, 0.014),
                                                   (0.030, 0.009), (0.040, 0.009), (0.041, 0)], n=16), 'PackMetal')
    n = 24
    T.add(sweep([V(0.034 * cos(2 * pi * j / n), TANK_Y + 0.034 * sin(2 * pi * j / n), 1.078) for j in range(n)],
                0.0055, n=8, normals=[V(0, 0, 1)] * n, loop=True), 'PackMetal')
    for k in range(3):
        a = 2 * pi * k / 3 + 0.5
        T.add(sweep([V(0, TANK_Y, 1.078), V(0.034 * cos(a), TANK_Y + 0.034 * sin(a), 1.078)], 0.004, n=6), 'PackMetal')
    # hose port on the character's right
    T.add(lathe(V(-0.080, TANK_Y, 1.012), V(-1, 0, 0), [(0, 0.021), (0.030, 0.021), (0.032, 0.026), (0.046, 0.026),
                                                         (0.048, 0.016)], n=16), 'PackMetal')

    # ---- gas column: local +Y is up, origin at the bottom centre ---------------
    G.add(lathe(V(0, 0, 0), V(0, 1, 0), [(0, 0), (0, GAS_R), (GAS_Z1 - GAS_Z0, GAS_R), (GAS_Z1 - GAS_Z0, 0)], n=28),
          'Gas')

    # ---- ribbed hose down the right side to a brass exhaust horn ----------------
    guide = [(-0.122, 0.290, 1.012), (-0.172, 0.286, 1.004), (-0.212, 0.268, 0.962), (-0.230, 0.242, 0.885),
             (-0.234, 0.226, 0.785), (-0.231, 0.226, 0.695), (-0.216, 0.246, 0.622), (-0.196, 0.272, 0.580)]
    pts = catmull_v(guide, 9)
    H.add(sweep(pts, [0.0165 + (0.0025 if i % 2 else 0.0) for i in range(len(pts))], n=10), 'Boot')
    d = (pts[-1] - pts[-3]).normalized()
    H.add(lathe(pts[-1], d, [(-0.012, 0.0195), (0.0, 0.022), (0.028, 0.025), (0.056, 0.034), (0.080, 0.050),
                             (0.088, 0.057), (0.091, 0.055), (0.084, 0.046), (0.058, 0.027), (0.030, 0.017),
                             (0.030, 0)], n=24), 'PackMetal')
    return P, T, G, H


# =============================================================================
# armature
# =============================================================================
def build_armature():
    arm = bpy.data.armatures.new('Armature')
    ob = bpy.data.objects.new('Armature', arm)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm.edit_bones

    def bone(name, parent, head, tail, z_axis):
        b = eb.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        b.align_roll(Vector(z_axis))
        if parent:
            b.parent = eb[parent]
        b.use_connect = False
        b.use_deform = True
        return b

    fwd, back = (0, -1, 0), (0, 1, 0)
    # upright bones: local X = +X (character's left), local Y = up, local Z = forward
    bone('Root', None, (0, 0, 0), (0, 0, 0.25), fwd)
    bone('Hips', 'Root', (0, YC, HIP_Z), (0, YC, SPINE_Z), fwd)
    bone('Spine', 'Hips', (0, YC, SPINE_Z), (0, YC, CHEST_Z), fwd)
    bone('Chest', 'Spine', (0, YC, CHEST_Z), (0, YC, NECK_Z), fwd)
    bone('Head', 'Chest', (0, YC, NECK_Z), (0, YC, NECK_Z + 0.30), fwd)
    for sx, side in ((1, 'L'), (-1, 'R')):
        S = V(sx * SHOULDER.x, SHOULDER.y, SHOULDER.z)
        D = arm_dir(sx)
        E = S + D * ARM_UP
        # arms: local Y along the arm, local Z = forward, local X = elbow hinge
        bone('UpperArm.' + side, 'Chest', S, E, fwd)
        bone('LowerArm.' + side, 'UpperArm.' + side, E, E + D * ARM_LO, fwd)
        # legs: local Y = down, local X = +X (hinge), local Z = backward
        bone('UpperLeg.' + side, 'Hips', (sx * LEG_X, LEG_Y, HIP_Z), (sx * LEG_X, LEG_Y, KNEE_Z), back)
        bone('LowerLeg.' + side, 'UpperLeg.' + side, (sx * LEG_X, LEG_Y, KNEE_Z), (sx * LEG_X, LEG_Y, ANKLE_Z), back)
    bpy.ops.object.mode_set(mode='OBJECT')
    return ob


def bone_parent(ob, arm_ob, bone_name):
    """Rigidly attach ob to a bone without moving it."""
    b = arm_ob.data.bones[bone_name]
    pm = arm_ob.matrix_world @ b.matrix_local @ Matrix.Translation((0, b.length, 0))
    ob.parent = arm_ob
    ob.parent_type = 'BONE'
    ob.parent_bone = bone_name
    ob.matrix_parent_inverse = pm.inverted()


# =============================================================================
# preview renders
# =============================================================================
def look_at(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def render_previews(arm, gas, outdir):
    os.makedirs(outdir, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.device = 'CPU'
    sc.cycles.samples = int(arg('--samples', '96'))
    sc.cycles.use_denoising = False
    sc.cycles.max_bounces = 6
    sc.render.resolution_x, sc.render.resolution_y = 760, 950
    sc.render.image_settings.file_format = 'PNG'
    sc.view_settings.view_transform = 'Standard'

    world = bpy.data.worlds.new('W')
    world.use_nodes = True
    bg = world.node_tree.nodes['Background']
    bg.inputs[0].default_value = (0.72, 0.80, 0.95, 1)
    bg.inputs[1].default_value = 0.65
    sc.world = world

    gm = bpy.data.meshes.new('Ground')
    gm.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
    go = bpy.data.objects.new('Ground', gm)
    mat = bpy.data.materials.new('GroundPreview')
    mat.use_nodes = True
    mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.33, 0.34, 0.36, 1)
    mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value = 0.8
    gm.materials.append(mat)
    sc.collection.objects.link(go)

    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 2.3
    sun.data.angle = radians(6)
    sun.location = (-3, -4, 6)
    look_at(sun, (0, 0, 0.8))
    sc.collection.objects.link(sun)
    fill = bpy.data.objects.new('Fill', bpy.data.lights.new('Fill', 'AREA'))
    fill.data.energy = 180
    fill.data.size = 4
    fill.location = (3.5, 3.5, 3.0)
    look_at(fill, (0, 0, 0.9))
    sc.collection.objects.link(fill)

    cam = bpy.data.objects.new('Cam', bpy.data.cameras.new('Cam'))
    sc.collection.objects.link(cam)
    sc.camera = cam

    def shot(name, pos, target=(0, 0, 0.90), lens=50):
        cam.data.lens = lens
        cam.location = pos
        look_at(cam, target)
        sc.render.filepath = os.path.join(outdir, name + '.png')
        bpy.ops.render.render(write_still=True)

    gas.scale = (1, 0.62, 1)     # preview a partly filled tank
    only = arg('--only')
    views = [
        ('front', (0, -3.45, 1.0)),
        ('side', (3.45, 0, 1.0)),
        ('back', (0, 3.45, 1.05)),
        ('threeq', (-2.15, -2.65, 1.35)),
        ('back_threeq', (-2.3, 2.5, 1.45)),
    ]
    for name, pos in views:
        if only and name not in only:
            continue
        shot(name, pos)
    if not only or 'face' in only:
        shot('face', (-0.55, -1.75, 1.47), target=(0, -0.1, 1.40), lens=70)

    if not only or 'run' in only:
        pb = arm.pose.bones
        pose = {
            'Spine': (10, 0, 0), 'Chest': (4, 8, 0), 'Head': (-8, -10, 0),
            'UpperLeg.L': (-42, 0, 0), 'LowerLeg.L': (30, 0, 0),
            'UpperLeg.R': (32, 0, 0), 'LowerLeg.R': (40, 0, 0),
            'UpperArm.R': (50, 0, 0), 'LowerArm.R': (65, 0, 0),
            'UpperArm.L': (-40, 0, 0), 'LowerArm.L': (25, 0, 0),
        }
        for name, e in pose.items():
            pb[name].rotation_mode = 'XYZ'
            pb[name].rotation_euler = tuple(radians(a) for a in e)
        shot('run', (-2.3, -2.55, 1.25))
        shot('run_side', (3.45, 0, 1.0))
        for name in pose:
            pb[name].rotation_euler = (0, 0, 0)
    gas.scale = (1, 1, 1)


# =============================================================================
# main
# =============================================================================
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.system = 'METRIC'
    sc.unit_settings.scale_length = 1.0

    B, torso, balls = build_body()
    P, T, G, H = build_pack(torso, balls)

    arm = build_armature()
    body = B.build()
    body.modifiers.new('Armature', 'ARMATURE').object = arm

    pack, tank, hose = P.build(), T.build(), H.build()
    gas = G.build()
    gas.rotation_euler = (pi / 2, 0, 0)
    gas.location = (0, TANK_Y, GAS_Z0)
    bpy.context.view_layer.update()
    for ob in (pack, tank, gas, hose):
        bone_parent(ob, arm, 'Chest')
    bpy.context.view_layer.update()

    # ---- report -------------------------------------------------------------
    total = 0
    for acc in (B, P, T, G, H):
        total += acc.tris()
        print(f"[char] {acc.name:9s} verts={len(acc.verts):6d} tris={acc.tris():6d} mats={sorted(set(acc.fmat))}")
    print(f"[char] total tris = {total}")
    lo = Vector((1e9,) * 3)
    hi = Vector((-1e9,) * 3)
    for ob in (body, pack, tank, gas, hose):
        for v in ob.data.vertices:
            w = ob.matrix_world @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    print(f"[char] bounds min=({lo.x:.3f},{lo.y:.3f},{lo.z:.3f}) max=({hi.x:.3f},{hi.y:.3f},{hi.z:.3f})")
    unweighted = sum(1 for v in body.data.vertices if not v.groups)
    print(f"[char] unweighted Body verts = {unweighted}")
    for b in arm.data.bones:
        m = b.matrix_local.to_3x3()
        print(f"[char] bone {b.name:11s} head=({b.head_local.x:+.3f},{b.head_local.y:+.3f},{b.head_local.z:+.3f}) "
              f"X=({m.col[0].x:+.2f},{m.col[0].y:+.2f},{m.col[0].z:+.2f}) "
              f"Y=({m.col[1].x:+.2f},{m.col[1].y:+.2f},{m.col[1].z:+.2f})")

    # ---- export --------------------------------------------------------------
    if arg('--no-export') is None and '--no-export' not in ARGV:
        os.makedirs(os.path.dirname(OUT), exist_ok=True)
        bpy.ops.export_scene.fbx(
            filepath=OUT, use_selection=False, object_types={'ARMATURE', 'MESH'},
            add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False,
            mesh_smooth_type='OFF', axis_forward='-Z', axis_up='Y',
            apply_scale_options='FBX_SCALE_UNITS', use_mesh_modifiers=True,
            path_mode='AUTO', embed_textures=False)
        print(f"[char] exported {OUT} ({os.path.getsize(OUT)} bytes)")

    if RENDER_DIR:
        render_previews(arm, gas, RENDER_DIR)


main()
