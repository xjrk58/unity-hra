"""
Re-imports the exported Character.fbx into an empty scene and prints what a
consumer (Unity) will see: hierarchy, bones, materials, size, triangle count
and skin-weight coverage.

    blender -b -P Tools/blender/verify_character.py [-- path/to/Character.fbx]
"""
import bpy
import os
import sys
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
path = argv[0] if argv else os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Models", "Character.fbx"))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=path)
bpy.context.view_layer.update()

print(f"[verify] {path}")
lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
tris = 0
for ob in sorted(bpy.data.objects, key=lambda o: o.name):
    parent = ob.parent.name if ob.parent else "-"
    if ob.parent and ob.parent_type == 'BONE':
        parent += ":" + ob.parent_bone
    line = f"[verify] object {ob.name:9s} type={ob.type:8s} parent={parent}"
    if ob.type == 'MESH':
        me = ob.data
        t = sum(len(p.vertices) - 2 for p in me.polygons)
        tris += t
        for v in me.vertices:
            w = ob.matrix_world @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
        mods = [m.type + ":" + (m.object.name if getattr(m, "object", None) else "?") for m in ob.modifiers]
        unweighted = sum(1 for v in me.vertices if not v.groups)
        over4 = sum(1 for v in me.vertices if len(v.groups) > 4)
        line += (f" tris={t} mats={[s.name for s in ob.material_slots]} uv={len(me.uv_layers)} mods={mods} "
                 f"groups={len(ob.vertex_groups)} unweighted={unweighted} >4bones={over4}")
    print(line)

arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for b in arm.data.bones:
    h = arm.matrix_world @ b.head_local
    print(f"[verify] bone {b.name:11s} parent={(b.parent.name if b.parent else '-'):11s} "
          f"head=({h.x:+.3f},{h.y:+.3f},{h.z:+.3f}) deform={b.use_deform}")

gas = bpy.data.objects.get("TankGas")
if gas:
    m = gas.matrix_world
    up = (m.to_3x3() @ Vector((0, 1, 0))).normalized()
    o = m.translation
    d = gas.dimensions
    print(f"[verify] TankGas origin=({o.x:+.3f},{o.y:+.3f},{o.z:+.3f}) local+Y in world=({up.x:+.2f},{up.y:+.2f},{up.z:+.2f}) "
          f"local dims=({d.x:.3f},{d.y:.3f},{d.z:.3f}) scale={tuple(round(s, 3) for s in gas.scale)}")

print(f"[verify] bounds min=({lo.x:.3f},{lo.y:.3f},{lo.z:.3f}) max=({hi.x:.3f},{hi.y:.3f},{hi.z:.3f})")
print(f"[verify] total tris={tris} materials={sorted(m.name for m in bpy.data.materials)}")
