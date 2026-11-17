"""
Procedural arena kit for the game (railing, column, ceiling lamp, goal).

    blender -b --factory-startup -P Tools/blender/build_kit.py -- [--out DIR] [--preview DIR] [--only Railing,Goal]

Everything is generated from code (no downloads, no source .blend), so the
models can be tweaked here and re-exported at any time.

Conventions shared by every piece
  * Blender Z up, 1 unit = 1 m, transforms and modifiers applied.
  * Material slots are addressed BY NAME from the game code, which swaps in its
    own PBR materials. The colours set here only serve the preview renders.
  * UVs are a box projection at 1 UV unit per metre (the round column shaft uses
    a seamless cylindrical wrap at the same density instead).
  * The FBX is written Y-up with the axis conversion baked into the mesh data,
    so the imported root has an identity rotation and scale 1 in Unity.
    Blender -Y (front) ends up as Unity +Z, Blender +Z as Unity +Y.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Models"))

UV_BOX, UV_CYL = 0, 1
CYL_REPEATS = 2  # horizontal texture repeats around a cylindrically mapped shaft


# ----------------------------------------------------------------------------
# primitives: each returns a small temporary bmesh with outward-facing faces
# ----------------------------------------------------------------------------

def box(lo, hi, bevel=0.0):
    """Axis-aligned box from corner lo to corner hi, optionally chamfered."""
    lo, hi = Vector(lo), Vector(hi)
    size, centre = hi - lo, (hi + lo) * 0.5
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * size.x, v.co.y * size.y, v.co.z * size.z))
    if bevel > 0.0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type='OFFSET',
                        segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
    for v in bm.verts:
        v.co += centre
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    return bm


def _basis(axis):
    z = axis.normalized()
    ref = Vector((0, 0, 1)) if abs(z.z) < 0.99 else Vector((1, 0, 0))
    x = ref.cross(z).normalized()
    return x, z.cross(x), z


def cyl(p0, p1, r0, r1=None, seg=12, cap0=True, cap1=True, phase=0.0):
    """Cylinder / frustum between two points."""
    p0, p1 = Vector(p0), Vector(p1)
    r1 = r0 if r1 is None else r1
    x, y, _ = _basis(p1 - p0)
    bm = bmesh.new()
    ang = [phase + 2.0 * math.pi * i / seg for i in range(seg)]
    a = [bm.verts.new(p0 + (x * math.cos(t) + y * math.sin(t)) * r0) for t in ang]
    b = [bm.verts.new(p1 + (x * math.cos(t) + y * math.sin(t)) * r1) for t in ang]
    for i in range(seg):
        j = (i + 1) % seg
        bm.faces.new([a[i], a[j], b[j], b[i]])
    if cap0:
        bm.faces.new(list(reversed(a)))
    if cap1:
        bm.faces.new(b)
    return bm


def lathe(profile, seg=32, closed=False, cap_start=False, cap_end=False):
    """Revolve a list of (r, z) points around the Z axis.

    The surface normal points to the right of the travel direction when the
    profile is drawn with r to the right and z up: draw outer surfaces bottom
    to top, and closed cross-sections counter-clockwise.
    """
    bm = bmesh.new()
    rings = []
    for r, z in profile:
        if r < 1e-9:
            rings.append([bm.verts.new((0.0, 0.0, z))])
        else:
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / seg),
                                        r * math.sin(2 * math.pi * i / seg), z)) for i in range(seg)])
    n = len(rings)
    for k in range(n if closed else n - 1):
        ra, rb = rings[k], rings[(k + 1) % n]
        for i in range(seg):
            j = (i + 1) % seg
            if len(ra) == 1 and len(rb) == 1:
                continue
            if len(ra) == 1:
                bm.faces.new([ra[0], rb[j], rb[i]])
            elif len(rb) == 1:
                bm.faces.new([ra[i], ra[j], rb[0]])
            else:
                bm.faces.new([ra[i], ra[j], rb[j], rb[i]])
    if cap_start and len(rings[0]) > 1:
        bm.faces.new(list(reversed(rings[0])))
    if cap_end and len(rings[-1]) > 1:
        bm.faces.new(rings[-1])
    return bm


def ring(r_in, r_out, z0, z1, seg=32):
    """Flat ring with a rectangular cross-section (flange, collar, washer)."""
    return lathe([(r_in, z0), (r_out, z0), (r_out, z1), (r_in, z1)], seg=seg, closed=True)


def torus(R, r, z, seg=48, tube=8):
    prof = [(R + r * math.cos(2 * math.pi * k / tube), z + r * math.sin(2 * math.pi * k / tube))
            for k in range(tube)]
    return lathe(prof, seg=seg, closed=True)


def moved(bm, offset):
    off = Vector(offset)
    for v in bm.verts:
        v.co += off
    return bm


# ----------------------------------------------------------------------------
# part assembly
# ----------------------------------------------------------------------------

class Part:
    """Accumulates primitives into one mesh with named material slots."""

    def __init__(self, name, mats):
        self.name = name
        self.mats = list(mats)
        self.bm = bmesh.new()
        self.uvmode = self.bm.faces.layers.int.new("uvmode")

    def add(self, src, mat=None, uv=UV_BOX):
        """Merge a temp bmesh. mat=None keeps the material indices already set on src."""
        mi = None if mat is None else self.mats.index(mat)
        vmap = {v: self.bm.verts.new(v.co) for v in src.verts}
        for f in src.faces:
            nf = self.bm.faces.new([vmap[v] for v in f.verts])
            nf.material_index = f.material_index if mi is None else mi
            nf.smooth = True
            nf[self.uvmode] = uv
        src.free()

    def finish(self, materials, smooth_angle=50.0):
        bm = self.bm
        bm.normal_update()
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            if f[self.uvmode] == UV_CYL:
                ang = [math.atan2(l.vert.co.y, l.vert.co.x) for l in f.loops]
                if max(ang) - min(ang) > math.pi:
                    ang = [a + 2 * math.pi if a < 0 else a for a in ang]
                for l, a in zip(f.loops, ang):
                    l[uv].uv = (a / (2 * math.pi) * CYL_REPEATS, l.vert.co.z)
                continue
            n = f.normal
            ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
            for l in f.loops:
                c = l.vert.co
                if az >= ax and az >= ay:
                    l[uv].uv = (c.x, c.y)
                elif ax >= ay:
                    l[uv].uv = (c.y, c.z)
                else:
                    l[uv].uv = (c.x, c.z)

        me = bpy.data.meshes.new(self.name)
        bm.to_mesh(me)
        bm.free()
        for name in self.mats:
            me.materials.append(materials[name])
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.collection.objects.link(obj)

        # smooth shading with hard edges above the angle; weighted normals keep
        # large flat faces flat while chamfers pick up the highlight
        me.use_auto_smooth = True
        me.auto_smooth_angle = math.radians(smooth_angle)
        mod = obj.modifiers.new("WeightedNormal", 'WEIGHTED_NORMAL')
        mod.keep_sharp = True
        mod.mode = 'FACE_AREA'
        mod.weight = 50
        bpy.context.view_layer.objects.active = obj
        with bpy.context.temp_override(object=obj, active_object=obj, selected_objects=[obj]):
            bpy.ops.object.modifier_apply(modifier=mod.name)
        return obj


def weld(bm, dist=1e-5):
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=dist)
    return bm


# ----------------------------------------------------------------------------
# materials (preview colours only - the game replaces them by slot name)
# ----------------------------------------------------------------------------

def make_mat(name, color, metallic=0.0, rough=0.5, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1.0)
    b.inputs["Metallic"].default_value = metallic
    b.inputs["Roughness"].default_value = rough
    if emit > 0.0:
        b.inputs["Emission Color"].default_value = (*color, 1.0)
        b.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = (*color, 1.0)
    return m


def palette(hazard_a):
    return {
        "Metal": make_mat("Metal", (0.56, 0.58, 0.60), metallic=1.0, rough=0.42),
        "Concrete": make_mat("Concrete", (0.52, 0.51, 0.49), rough=0.9),
        "HazardA": make_mat("HazardA", hazard_a, rough=0.55),
        "HazardB": make_mat("HazardB", (0.03, 0.03, 0.035), rough=0.6),
        "LampBody": make_mat("LampBody", (0.86, 0.87, 0.88), rough=0.4),
        "LampGlow": make_mat("LampGlow", (1.0, 0.97, 0.9), rough=0.3, emit=6.0),
        "GoalPaint": make_mat("GoalPaint", (0.93, 0.33, 0.03), metallic=0.0, rough=0.38),
        "Glow": make_mat("Glow", (1.0, 0.42, 0.04), rough=0.3, emit=9.0),
    }


YELLOW = (0.92, 0.68, 0.04)
ORANGE = (1.0, 0.36, 0.02)


# ----------------------------------------------------------------------------
# Railing: 2 m module of welded steel baluster barrier
# ----------------------------------------------------------------------------

def build_railing():
    mats = palette(YELLOW)
    p = Part("Railing", ["Metal"])
    half = 0.98            # 1.96 m section, leaves a 4 cm joint in a 2 m module
    post_x = 0.925
    top = 1.10
    rail_r = 0.024         # 48 mm handrail
    rail_z = top - rail_r
    post_r = 0.025         # 50 mm post
    plate_t = 0.010
    low_z = 0.10

    # handrail with slightly inset end plugs
    p.add(cyl((-half + 0.004, 0, rail_z), (half - 0.004, 0, rail_z), rail_r, seg=16), "Metal")
    for s in (-1, 1):
        p.add(cyl((s * (half - 0.004), 0, rail_z), (s * half, 0, rail_z), rail_r * 0.86, rail_r * 0.8,
                  seg=14, cap0=False), "Metal")

    for s in (-1, 1):
        x = s * post_x
        # base plate, anchor bolts with washers, weld collar, post
        p.add(box((x - 0.05, -0.05, 0.0), (x + 0.05, 0.05, plate_t), bevel=0.002), "Metal")
        for bx in (-0.034, 0.034):
            for by in (-0.034, 0.034):
                p.add(cyl((x + bx, by, plate_t), (x + bx, by, plate_t + 0.002), 0.0115, seg=8, cap0=False), "Metal")
                p.add(cyl((x + bx, by, plate_t + 0.002), (x + bx, by, plate_t + 0.010), 0.0075, seg=6,
                          cap0=False), "Metal")
        p.add(cyl((x, 0, plate_t), (x, 0, plate_t + 0.012), post_r + 0.008, post_r + 0.002, seg=14,
                  cap0=False, cap1=True), "Metal")
        p.add(cyl((x, 0, plate_t), (x, 0, rail_z), post_r, seg=14, cap0=False, cap1=False), "Metal")
        # saddle where the post meets the handrail
        p.add(cyl((x, 0, rail_z - rail_r - 0.012), (x, 0, rail_z - rail_r + 0.006), post_r + 0.005, seg=14), "Metal")

    # bottom rail: flat bar on edge
    p.add(box((-post_x, -0.008, low_z - 0.02), (post_x, 0.008, low_z + 0.02), bevel=0.0015), "Metal")

    # balusters
    count = 16
    pitch = (2 * post_x) / (count + 1)
    for k in range(1, count + 1):
        x = -post_x + k * pitch
        p.add(cyl((x, 0, low_z), (x, 0, rail_z - rail_r * 0.5), 0.007, seg=8, cap0=False, cap1=False), "Metal")

    return [p.finish(mats)], None


# ----------------------------------------------------------------------------
# Column: round concrete column with plinth, flared capital and hazard band
# ----------------------------------------------------------------------------

def build_column():
    mats = palette(YELLOW)
    names = ["Concrete", "HazardA", "HazardB"]
    p = Part("Column", names)
    seg = 36
    R = 0.30
    H = 4.5
    band_lo, band_hi = 0.15, 1.00
    rows = 16                       # lattice rows in the band
    stripe = 3                      # stripe thickness in rows (36 / 3 = 12 stripes -> seamless wrap)
    row_h = (band_hi - band_lo) / rows
    flare_lo = H - 0.45
    abacus_lo = H - 0.17

    # plinth and abacus (drop panel)
    p.add(box((-0.40, -0.40, 0.0), (0.40, 0.40, 0.12), bevel=0.018), "Concrete")
    p.add(box((-0.55, -0.55, abacus_lo), (0.55, 0.55, H), bevel=0.02), "Concrete")

    shaft = bmesh.new()

    def merge(src, mi):
        vmap = {v: shaft.verts.new(v.co) for v in src.verts}
        for f in src.faces:
            shaft.faces.new([vmap[v] for v in f.verts]).material_index = mi
        src.free()

    # plain shaft below the band
    merge(lathe([(R, 0.10), (R, band_lo)], seg=seg), 0)

    # hazard band: diagonal stripes whose edges run through lattice points, so
    # the band has no T-junctions against the plain shaft above and below
    def pt(i, row):
        a = 2 * math.pi * (i % seg) / seg
        return (R * math.cos(a), R * math.sin(a), band_lo + row * row_h)

    for i in range(seg):
        k_lo = math.floor((-i - stripe - 1) / stripe)
        k_hi = math.ceil((rows - i) / stripe)
        for k in range(k_lo, k_hi + 1):
            b = i + stripe * k      # row where this stripe's lower edge meets the left side of the strip
            l_lo, l_hi = max(b, 0), min(b + stripe, rows)
            r_lo, r_hi = max(b + 1, 0), min(b + 1 + stripe, rows)
            poly = []
            if l_lo <= l_hi:
                poly.append((i, l_lo))
            if r_lo <= r_hi:
                poly.append((i + 1, r_lo))
                if r_hi != r_lo:
                    poly.append((i + 1, r_hi))
            if l_lo <= l_hi and l_hi != l_lo:
                poly.append((i, l_hi))
            if len(poly) < 3:
                continue
            f = shaft.faces.new([shaft.verts.new(pt(*q)) for q in poly])
            f.material_index = 1 + (k % 2)

    # plain shaft above the band, flowing into the flared capital
    prof = [(R, band_hi), (R, flare_lo)]
    steps = 5
    for s in range(1, steps + 1):
        a = (math.pi / 2) * s / steps
        prof.append((R + 0.20 * (1 - math.cos(a)), flare_lo + (abacus_lo + 0.01 - flare_lo) * math.sin(a)))
    merge(lathe(prof, seg=seg), 0)

    weld(shaft)
    p.add(shaft, None, uv=UV_CYL)
    return [p.finish(mats)], None


# ----------------------------------------------------------------------------
# Lamp: ceiling mounted LED batten, origin at the mounting point on the ceiling
# ----------------------------------------------------------------------------

def build_lamp():
    mats = palette(YELLOW)
    p = Part("Lamp", ["LampBody", "LampGlow"])
    L = 0.625               # half length
    cap = 0.028             # end cap length
    w = 0.07                # half width
    body_top, body_bot = -0.032, -0.074
    drop = -0.12

    # mounting brackets against the ceiling
    for s in (-1, 1):
        x = s * 0.40
        p.add(box((x - 0.03, -0.052, body_top - 0.004), (x + 0.03, 0.052, 0.0), bevel=0.003), "LampBody")
        p.add(box((x - 0.045, -0.02, -0.005), (x + 0.045, 0.02, 0.0)), "LampBody")

    # body tray and end caps
    p.add(box((-L + cap - 0.002, -w, body_bot), (L - cap + 0.002, w, body_top), bevel=0.005), "LampBody")
    for s in (-1, 1):
        x0, x1 = sorted((s * (L - cap), s * L))
        p.add(box((x0, -w - 0.002, drop), (x1, w + 0.002, body_top + 0.002), bevel=0.006), "LampBody")

    # rounded diffuser under the body
    seg = 10
    x0, x1 = -L + cap - 0.001, L - cap + 0.001
    ry, rz = w - 0.006, abs(drop - body_bot) - 0.004
    d = bmesh.new()
    a_ring, b_ring = [], []
    for k in range(seg + 1):
        t = math.pi * k / seg
        y, z = ry * math.cos(t), body_bot - rz * math.sin(t)
        a_ring.append(d.verts.new((x0, y, z)))
        b_ring.append(d.verts.new((x1, y, z)))
    for k in range(seg):
        d.faces.new([a_ring[k], b_ring[k], b_ring[k + 1], a_ring[k + 1]])
    p.add(d, "LampGlow")

    return [p.finish(mats)], None


# ----------------------------------------------------------------------------
# Goal: orange extraction hood over a marked steel pad
# ----------------------------------------------------------------------------

def oct_pt(apothem, deg, z):
    """Point on a regular octagon whose flats face 0, 45, 90 ... degrees."""
    t = ((deg + 22.5) % 45.0) - 22.5
    rho = apothem / math.cos(math.radians(t))
    return (rho * math.cos(math.radians(deg)), rho * math.sin(math.radians(deg)), z)


def build_goal():
    mats = palette(ORANGE)
    names = ["GoalPaint", "Metal", "HazardA", "HazardB", "Glow"]
    PAINT, METAL, HA, HB, GLOW = range(5)
    p = Part("GoalStructure", names)

    # ---- pad -------------------------------------------------------------
    pad = bmesh.new()
    z_top, z_edge = 0.05, 0.022
    a_edge, a_out, a_in, a_mid = 2.40, 2.355, 1.99, 1.95
    n = 64
    step = 360.0 / n
    slant = 2                                   # stripe lean, in segments

    def quad(pts, mi):
        pad.faces.new([pad.verts.new(q) for q in pts]).material_index = mi

    centre = pad.verts.new((0, 0, z_top))
    for i in range(n):
        a0, a1 = i * step, (i + 1) * step
        # painted centre
        pad.faces.new([centre, pad.verts.new(oct_pt(a_mid, a0, z_top)),
                       pad.verts.new(oct_pt(a_mid, a1, z_top))]).material_index = PAINT
        # bare steel divider
        quad([oct_pt(a_mid, a0, z_top), oct_pt(a_in, a0, z_top), oct_pt(a_in, a1, z_top), oct_pt(a_mid, a1, z_top)], METAL)
        # leaning hazard stripe
        quad([oct_pt(a_out, a0 + slant * step, z_top), oct_pt(a_out, a1 + slant * step, z_top),
              oct_pt(a_in, a1, z_top), oct_pt(a_in, a0, z_top)], HA if i % 2 == 0 else HB)
        # chamfered steel edge and side
        quad([oct_pt(a_out, a0, z_top), oct_pt(a_edge, a0, z_edge), oct_pt(a_edge, a1, z_edge), oct_pt(a_out, a1, z_top)], METAL)
        quad([oct_pt(a_edge, a0, z_edge), oct_pt(a_edge, a0, 0.0), oct_pt(a_edge, a1, 0.0), oct_pt(a_edge, a1, z_edge)], METAL)
    weld(pad)
    p.add(pad, None)

    # ---- posts, frame ------------------------------------------------------
    P = 2.2                 # post offset
    post_r = 0.06
    post_top = 3.0
    beam_z = 2.92
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * P, sy * P
            p.add(box((x - 0.15, y - 0.15, 0.0), (x + 0.15, y + 0.15, 0.014), bevel=0.003), "Metal")
            for bx in (-0.105, 0.105):
                for by in (-0.105, 0.105):
                    p.add(cyl((x + bx, y + by, 0.014), (x + bx, y + by, 0.017), 0.022, seg=8, cap0=False), "Metal")
                    p.add(cyl((x + bx, y + by, 0.017), (x + bx, y + by, 0.034), 0.014, seg=6, cap0=False), "Metal")
            # four gussets
            for gx, gy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                g0 = Vector((x + gx * 0.055, y + gy * 0.055, 0.014))
                g1 = Vector((x + gx * 0.13, y + gy * 0.13, 0.014))
                t = Vector((-gy, gx, 0)) * 0.005
                gb = bmesh.new()
                tri_a = [gb.verts.new(g0 + t), gb.verts.new(g1 + t), gb.verts.new(g0 + t + Vector((0, 0, 0.11)))]
                tri_b = [gb.verts.new(g0 - t), gb.verts.new(g1 - t), gb.verts.new(g0 - t + Vector((0, 0, 0.11)))]
                gb.faces.new(tri_a)
                gb.faces.new(list(reversed(tri_b)))
                gb.faces.new([tri_a[1], tri_b[1], tri_b[2], tri_a[2]])
                gb.faces.new([tri_a[0], tri_b[0], tri_b[1], tri_a[1]])
                gb.faces.new([tri_a[0], tri_a[2], tri_b[2], tri_b[0]])
                bmesh.ops.recalc_face_normals(gb, faces=list(gb.faces))
                p.add(gb, "Metal")
            p.add(cyl((x, y, 0.014), (x, y, post_top), post_r, seg=16, cap0=False), "Metal")
            # painted band low on the post so it reads from a distance
            p.add(cyl((x, y, 0.35), (x, y, 1.15), post_r + 0.003, seg=16, cap0=True, cap1=True), "GoalPaint")
            # welded node block where the beams meet the post, cap plate + beacon
            p.add(box((x - 0.085, y - 0.085, beam_z - 0.10), (x + 0.085, y + 0.085, post_top), bevel=0.008), "Metal")
            p.add(cyl((x, y, post_top), (x, y, post_top + 0.015), post_r + 0.035, seg=16, cap0=True), "Metal")
            p.add(cyl((x, y, post_top + 0.015), (x, y, post_top + 0.055), 0.07, 0.064, seg=16, cap0=False), "Metal")
            dome = [(0.056, post_top + 0.055), (0.056, post_top + 0.155)]
            for s in range(1, 4):
                a = (math.pi / 2) * s / 3
                dome.append((0.056 * math.cos(a), post_top + 0.155 + 0.045 * math.sin(a)))
            p.add(moved(lathe(dome, seg=14), (x, y, 0)), "Glow")
            # knee braces up to the beams (all above 2.6 m)
            p.add(cyl((x, y, 2.67), (x - sx * 0.42, y, beam_z - 0.05), 0.028, seg=8), "Metal")
            p.add(cyl((x, y, 2.67), (x, y - sy * 0.42, beam_z - 0.05), 0.028, seg=8), "Metal")

    for s in (-1, 1):
        y = s * P
        p.add(box((-P, y - 0.05, beam_z - 0.07), (P, y + 0.05, beam_z + 0.07), bevel=0.008), "Metal")
        p.add(box((y - 0.05, -P, beam_z - 0.07), (y + 0.05, P, beam_z + 0.07), bevel=0.008), "Metal")

    # ---- hood ------------------------------------------------------------
    r_open = 1.60           # inner radius of the opening (3.2 m across)
    th = 0.016              # sheet thickness
    lip_z = 2.90
    corner_z = 3.06
    neck_r, neck_z, collar_z = 0.35, 3.70, 3.77
    seg = 56

    def hood_profile(off):
        """Outer skin for off=th, inner skin for off=0 (bottom to top)."""
        r0 = r_open + off
        c = Vector((r0, corner_z))
        end = Vector((neck_r + off, neck_z))
        d = (end - c).normalized()
        pts = [(r0, lip_z)]
        a, b = c - Vector((0, 0.09)), c + d * 0.09
        for s in range(0, 4):               # rounded shoulder
            t = s / 3
            q = a * (1 - t) ** 2 + c * 2 * t * (1 - t) + b * t ** 2
            pts.append((q.x, q.y))
        e0, e1 = end - d * 0.07, end + Vector((0, 0.05))
        for s in range(0, 3):               # rounded neck
            t = s / 2
            q = e0 * (1 - t) ** 2 + end * 2 * t * (1 - t) + e1 * t ** 2
            pts.append((q.x, q.y))
        pts.append((neck_r + off, collar_z))
        return pts

    outer = hood_profile(th)
    inner = hood_profile(0.0)
    # counter-clockwise cross-section: up the outer skin, back down the inner skin
    p.add(lathe(outer + list(reversed(inner)), seg=seg, closed=True), "GoalPaint")
    # rolled rim on the lip
    p.add(torus(r_open + th * 0.5, 0.022, lip_z, seg=seg, tube=6), "GoalPaint")
    # glowing strip just inside the lip
    p.add(lathe([(r_open + 0.002, lip_z + 0.03), (r_open - 0.03, lip_z + 0.03),
                 (r_open - 0.03, lip_z + 0.085), (r_open + 0.002, lip_z + 0.085)][::-1], seg=seg, closed=True), "Glow")

    # carrier ring hugging the skirt, tied back to beams and posts
    ring_R, ring_z = r_open + th + 0.04, 2.95
    p.add(torus(ring_R, 0.04, ring_z, seg=seg, tube=6), "Metal")
    for k in range(4):
        a = math.radians(90 * k)
        p.add(cyl((ring_R * math.cos(a), ring_R * math.sin(a), ring_z),
                  (P * math.cos(a), P * math.sin(a), beam_z), 0.036, seg=10), "Metal")
        a = math.radians(45 + 90 * k)
        sx, sy = (1 if math.cos(a) > 0 else -1), (1 if math.sin(a) > 0 else -1)
        p.add(cyl((ring_R * math.cos(a), ring_R * math.sin(a), ring_z),
                  (sx * P, sy * P, beam_z + 0.02), 0.036, seg=10), "Metal")
        # tie rod from the post top up to the duct flange, clear of the hood
        p.add(cyl((sx * (P - 0.06), sy * (P - 0.06), post_top - 0.02), (0.41 * math.cos(a), 0.41 * math.sin(a), 4.115), 0.016, seg=8), "Metal")

    # ---- duct --------------------------------------------------------------
    duct_r, duct_top = 0.35, 4.45
    p.add(lathe([(duct_r - 0.012, neck_z - 0.02), (duct_r, neck_z - 0.02), (duct_r, duct_top),
                 (duct_r - 0.012, duct_top)], seg=40, closed=True), "Metal")
    for z in (collar_z + 0.005, 4.10, duct_top - 0.03):
        p.add(ring(duct_r - 0.002, duct_r + 0.07, z, z + 0.03, seg=40), "Metal")
    # blanking plate so nobody sees daylight up the duct
    p.add(cyl((0, 0, duct_top - 0.02), (0, 0, duct_top - 0.005), duct_r - 0.006, seg=40), "HazardB")
    # motor spider above the fan
    for a in (0.0, math.pi / 2):
        cx, sy = math.cos(a), math.sin(a)
        p.add(cyl((-0.34 * cx, -0.34 * sy, 4.03), (0.34 * cx, 0.34 * sy, 4.03), 0.016, seg=8), "Metal")
    p.add(cyl((0, 0, 3.97), (0, 0, 4.10), 0.075, seg=16), "Metal")

    structure = p.finish(mats)

    # ---- fan (separate object so the game can spin it around its up axis) ----
    hub_z = 3.93
    f = Part("GoalFan", ["Metal"])
    f.add(cyl((0, 0, -0.045), (0, 0, 0.045), 0.07, seg=16), "Metal")
    f.add(cyl((0, 0, -0.07), (0, 0, -0.045), 0.035, 0.07, seg=16, cap1=False), "Metal")
    blades = 6
    for k in range(blades):
        a = 2 * math.pi * k / blades
        ca, sa = math.cos(a), math.sin(a)
        radial, tang = Vector((ca, sa, 0)), Vector((-sa, ca, 0))
        b = bmesh.new()
        stations = [(0.06, 0.05, 34.0), (0.19, 0.085, 24.0), (0.30, 0.09, 16.0)]   # radius, half chord, pitch
        rows = []
        for r, hc, pitch in stations:
            c = Vector((math.cos(math.radians(pitch)), 0, math.sin(math.radians(pitch))))
            chord = tang * c.x + Vector((0, 0, 1)) * c.z
            up = chord.cross(radial).normalized() * 0.004
            base = radial * r
            rows.append([b.verts.new(base - chord * hc - up), b.verts.new(base + chord * hc - up),
                         b.verts.new(base + chord * hc + up), b.verts.new(base - chord * hc + up)])
        for s in range(len(rows) - 1):
            for e in range(4):
                g = (e + 1) % 4
                b.faces.new([rows[s][e], rows[s][g], rows[s + 1][g], rows[s + 1][e]])
        b.faces.new(rows[0])
        b.faces.new(list(reversed(rows[-1])))
        bmesh.ops.recalc_face_normals(b, faces=list(b.faces))
        f.add(b, "Metal")
    fan = f.finish(mats)
    fan.location = (0, 0, hub_z)

    root = bpy.data.objects.new("Goal", None)
    bpy.context.collection.objects.link(root)
    structure.parent = root
    fan.parent = root
    return [root, structure, fan], None


# ----------------------------------------------------------------------------
# export / preview
# ----------------------------------------------------------------------------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


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


def bounds(objs):
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    bpy.context.view_layer.update()
    for o in objs:
        if o.type != 'MESH':
            continue
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
    return lo, hi


def setup_render(samples=160, res=(800, 600)):
    s = bpy.context.scene
    s.render.engine = 'CYCLES'
    s.cycles.device = 'CPU'
    s.cycles.samples = samples
    s.cycles.use_denoising = False
    try:
        s.cycles.denoiser = 'OPENIMAGEDENOISE'
        s.cycles.use_denoising = True
        s.cycles.samples = max(32, samples // 3)
    except Exception:
        pass
    s.cycles.max_bounces = 4
    s.render.resolution_x, s.render.resolution_y = res
    s.render.resolution_percentage = 100
    s.render.image_settings.file_format = 'PNG'
    for vt in ('AgX', 'Filmic', 'Standard'):
        try:
            s.view_settings.view_transform = vt
            break
        except Exception:
            continue
    w = bpy.data.worlds.new("PreviewSky")
    w.use_nodes = True
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.62, 0.72, 0.9, 1.0)
    bg.inputs["Strength"].default_value = 1.0
    s.world = w
    sun = bpy.data.lights.new("PreviewSun", 'SUN')
    sun.energy = 3.2
    sun.angle = math.radians(4)
    so = bpy.data.objects.new("PreviewSun", sun)
    so.rotation_euler = (math.radians(52), 0, math.radians(28))
    s.collection.objects.link(so)


def add_plane(z, size, color, flip=False, rough=0.85):
    me = bpy.data.meshes.new("PreviewPlane")
    h = size * 0.5
    verts = [(-h, -h, z), (h, -h, z), (h, h, z), (-h, h, z)]
    me.from_pydata(verts, [], [(3, 2, 1, 0)] if flip else [(0, 1, 2, 3)])
    me.materials.append(make_mat("PreviewGround", color, rough=rough))
    o = bpy.data.objects.new("PreviewPlane", me)
    bpy.context.collection.objects.link(o)
    return o


def shoot(path, target, direction, dist, lens=50):
    s = bpy.context.scene
    cam = bpy.data.cameras.new("PreviewCam")
    cam.lens = lens
    co = bpy.data.objects.new("PreviewCam", cam)
    s.collection.objects.link(co)
    target = Vector(target)
    co.location = target + Vector(direction).normalized() * dist
    co.rotation_euler = (target - co.location).to_track_quat('-Z', 'Y').to_euler()
    s.camera = co
    s.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(co)


def figure(x, y):
    """1.8 m grey stand-in for scale."""
    me = bpy.data.meshes.new("PreviewFigure")
    bm = cyl((x, y, 0), (x, y, 1.55), 0.2, seg=16)
    extra = lathe([(0.0, 1.56), (0.11, 1.6), (0.12, 1.68), (0.09, 1.77), (0.0, 1.8)], seg=12)
    for v in extra.verts:
        v.co += Vector((x, y, 0))
    bm.to_mesh(me)
    o = bpy.data.objects.new("PreviewFigure", me)
    me2 = bpy.data.meshes.new("PreviewHead")
    extra.to_mesh(me2)
    o2 = bpy.data.objects.new("PreviewHead", me2)
    m = make_mat("PreviewFigure", (0.25, 0.4, 0.75), rough=0.7)
    me.materials.append(m)
    me2.materials.append(m)
    bpy.context.collection.objects.link(o)
    bpy.context.collection.objects.link(o2)


def preview_railing(objs, out):
    setup_render()
    add_plane(0.0, 40, (0.42, 0.42, 0.41))
    src = objs[0]
    for dx in (-2.0, 2.0):
        c = bpy.data.objects.new("PreviewCopy", src.data)
        c.location = (dx, 0, 0)
        bpy.context.collection.objects.link(c)
    shoot(os.path.join(out, "Railing_a.png"), (0.1, 0, 0.6), (0.55, -1.0, 0.35), 3.6)
    shoot(os.path.join(out, "Railing_b.png"), (0.95, 0, 0.12), (0.5, -1.0, 0.75), 0.75, lens=45)
    shoot(os.path.join(out, "Railing_c.png"), (0.95, 0, 1.0), (0.4, -1.0, 0.4), 0.9, lens=45)


def preview_column(objs, out):
    setup_render(res=(600, 800))
    add_plane(0.0, 40, (0.42, 0.42, 0.41))
    add_plane(4.5, 8, (0.5, 0.5, 0.49), flip=True)
    figure(1.2, -0.3)
    shoot(os.path.join(out, "Column_a.png"), (0.3, 0, 2.25), (0.5, -1.0, 0.02), 11.5)
    setup = bpy.context.scene
    setup.render.resolution_x, setup.render.resolution_y = 800, 600
    shoot(os.path.join(out, "Column_b.png"), (0, 0, 0.55), (0.6, -1.0, 0.45), 2.6, lens=40)
    shoot(os.path.join(out, "Column_c.png"), (0, 0, 4.2), (0.6, -1.0, -0.45), 3.0, lens=40)


def preview_lamp(objs, out):
    setup_render()
    add_plane(0.0, 6, (0.5, 0.5, 0.49), flip=True)
    add_plane(-3.0, 40, (0.42, 0.42, 0.41))
    shoot(os.path.join(out, "Lamp_a.png"), (0, 0, -0.06), (0.55, -1.0, -0.5), 1.9)
    shoot(os.path.join(out, "Lamp_b.png"), (0.5, 0, -0.06), (0.9, -0.6, -0.25), 0.55, lens=40)


def preview_goal(objs, out):
    setup_render()
    add_plane(0.0, 60, (0.42, 0.42, 0.41))
    figure(0.6, -0.4)
    shoot(os.path.join(out, "Goal_a.png"), (0, 0, 2.1), (0.75, -1.0, 0.28), 12.5, lens=40)
    shoot(os.path.join(out, "Goal_b.png"), (0, 0, 3.4), (0.4, -1.0, -0.42), 6.2, lens=28)
    shoot(os.path.join(out, "Goal_c.png"), (0, 0, 0.3), (0.3, -1.0, 1.6), 9.5, lens=40)
    shoot(os.path.join(out, "Goal_d.png"), (2.2, -2.2, 0.25), (0.5, -1.0, 0.6), 1.6, lens=40)
    shoot(os.path.join(out, "Goal_e.png"), (0, 0, 3.6), (0.25, -0.3, -1.0), 2.0, lens=20)
    shoot(os.path.join(out, "Goal_f.png"), (0, 0, 2.0), (0.3, -1.0, -0.02), 11.0, lens=35)


PIECES = {
    "Railing": (build_railing, preview_railing),
    "Column": (build_column, preview_column),
    "Lamp": (build_lamp, preview_lamp),
    "Goal": (build_goal, preview_goal),
}


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out, preview, only = DEFAULT_OUT, None, None
    i = 0
    while i < len(argv):
        if argv[i] == "--out":
            out = argv[i + 1]; i += 2
        elif argv[i] == "--preview":
            preview = argv[i + 1]; i += 2
        elif argv[i] == "--only":
            only = argv[i + 1].split(","); i += 2
        else:
            raise SystemExit("unknown argument " + argv[i])
    os.makedirs(out, exist_ok=True)
    if preview:
        os.makedirs(preview, exist_ok=True)

    for name, (build, shots) in PIECES.items():
        if only and name not in only:
            continue
        reset_scene()
        objs, _ = build()
        path = os.path.join(out, name + ".fbx")
        export_fbx(objs, path)
        lo, hi = bounds(objs)
        print("[kit] %-8s tris=%-6d bbox=(%.3f %.3f %.3f)..(%.3f %.3f %.3f) -> %s"
              % (name, tri_count(objs), lo.x, lo.y, lo.z, hi.x, hi.y, hi.z, path))
        if preview:
            shots(objs, preview)


main()
