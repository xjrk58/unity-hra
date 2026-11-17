"""Blender: Poly Haven glTF -> single-mesh FBX (origin bottom centre, one material slot 'Prop')."""
import bpy, sys, os, glob
from mathutils import Vector
argv = sys.argv[sys.argv.index('--') + 1:]
RAW, OUT = argv[0], argv[1]
PROPS = {'Barrel_01': 'BarrelRed', 'barrel_03': 'BarrelBlue', 'concrete_road_barrier': 'Barrier',
         'plastic_crate_02': 'Crate', 'cardboard_box_01': 'Box'}
MAX_TRIS = 9000
os.makedirs(OUT, exist_ok=True)
for id, name in PROPS.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    g = glob.glob(os.path.join(RAW, id, '*.gltf'))[0]
    bpy.ops.import_scene.gltf(filepath=g)
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type='CLEAR_KEEP_TRANSFORM')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o != ob: bpy.data.objects.remove(o, do_unlink=True)
    me = ob.data
    nmat = len(me.materials)
    mat = bpy.data.materials.new('Prop')
    me.materials.clear(); me.materials.append(mat)
    for p in me.polygons: p.material_index = 0
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    if tris > MAX_TRIS:
        m = ob.modifiers.new('dec', 'DECIMATE'); m.ratio = MAX_TRIS / tris
        bpy.ops.object.modifier_apply(modifier='dec')
    # origin -> bottom centre
    co = [v.co.copy() for v in me.vertices]
    mn = Vector((min(c.x for c in co), min(c.y for c in co), min(c.z for c in co)))
    mx = Vector((max(c.x for c in co), max(c.y for c in co), max(c.z for c in co)))
    off = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    for v in me.vertices: v.co -= off
    ob.name = name; me.name = name
    tris2 = sum(len(p.vertices) - 2 for p in me.polygons)
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, object_types={'MESH'},
                             bake_anim=False, mesh_smooth_type='OFF', axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', use_mesh_modifiers=True)
    print(f'PROP {name}: mats_in={nmat} tris {tris}->{tris2} size {tuple(round(v,3) for v in (mx-mn))} uv={len(me.uv_layers)}')
