"""Convert Poly Haven texture sets into Unity/URP-ready maps.
   <Name>_albedo.jpg, <Name>_normal.jpg (OpenGL), <Name>_mask.png (R=metal G=AO A=smoothness)"""
import os, sys, shutil, glob
import numpy as np
from PIL import Image
RAW, OUT = sys.argv[1], sys.argv[2]
SETS = {  # id -> (Name, subdir, neutral_albedo)
    'concrete_floor_worn_001': ('ConcreteFloor', '', False),
    'concrete_layers_02': ('ConcreteWall', '', False),
    'asphalt_02': ('Asphalt', '', False),
    'aerial_grass_rock': ('Ground', '', False),
    'denmin_fabric_02': ('Fabric', '', True),
    'brown_leather': ('Leather', '', False),
    'metal_plate': ('TreadPlate', '', False),
    'Barrel_01': ('BarrelRed', 'Props', False),
    'barrel_03': ('BarrelBlue', 'Props', False),
    'concrete_road_barrier': ('Barrier', 'Props', False),
    'plastic_crate_02': ('Crate', 'Props', False),
    'cardboard_box_01': ('Box', 'Props', False),
}
# name -> (mean linear reflectance to aim for, desaturation 0..1)
TARGET_ALBEDO = {'ConcreteFloor': (0.27, 0.0), 'ConcreteWall': (0.34, 0.0), 'Asphalt': (0.125, 0.0), 'Ground': (0.15, 0.25)}
def find(id, key):
    c = [f for f in glob.glob(os.path.join(RAW, id, '**', '*.jpg'), recursive=True) if key in os.path.basename(f)]
    assert len(c) == 1, (id, key, c)
    return c[0]
for id, (name, sub, neutral) in SETS.items():
    out = os.path.join(OUT, sub); os.makedirs(out, exist_ok=True)
    diff = find(id, '_diff_') if not glob.glob(os.path.join(RAW, id, '*albedo*')) else find(id, '_albedo_')
    nor, arm = find(id, '_nor_gl_'), find(id, '_arm_')
    if neutral:
        im = np.asarray(Image.open(diff).convert('RGB')).astype(np.float32) / 255
        g = im @ np.array([0.299, 0.587, 0.114], dtype=np.float32)
        g = g / g.mean() * 0.78                      # light neutral cloth, keeps the weave contrast
        g = np.clip(g, 0, 1)
        Image.fromarray((np.repeat(g[..., None], 3, 2) * 255).astype(np.uint8)).save(os.path.join(out, name + '_albedo.jpg'), quality=93)
    elif name in TARGET_ALBEDO:
        # scans are often darker than the real material; rescale in linear light to a plausible reflectance
        im = (np.asarray(Image.open(diff).convert('RGB')).astype(np.float32) / 255) ** 2.2
        lum = im @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32)
        target, desat = TARGET_ALBEDO[name]
        im = im * (target / lum.mean())
        if desat > 0:
            g = (im @ np.array([0.2126, 0.7152, 0.0722], dtype=np.float32))[..., None]
            im = im * (1 - desat) + g * desat
        im = np.clip(im, 0, 1) ** (1 / 2.2)
        Image.fromarray((im * 255 + 0.5).astype(np.uint8)).save(os.path.join(out, name + '_albedo.jpg'), quality=93)
    else:
        shutil.copyfile(diff, os.path.join(out, name + '_albedo.jpg'))
    shutil.copyfile(nor, os.path.join(out, name + '_normal.jpg'))
    a = Image.open(arm).convert('RGB')
    if max(a.size) > 1024: a = a.resize((1024, 1024), Image.LANCZOS)
    a = np.asarray(a)
    ao, rough, metal = a[..., 0], a[..., 1], a[..., 2]
    mask = np.stack([metal, ao, np.zeros_like(ao), 255 - rough], -1)
    Image.fromarray(mask, 'RGBA').save(os.path.join(out, name + '_mask.png'), optimize=True)
    alb = ((np.asarray(Image.open(os.path.join(out, name + '_albedo.jpg')).convert('RGB')).reshape(-1, 3) / 255) ** 2.2).mean(0)
    print(f'{name:14s} albedo linear mean {np.round(alb,3)}  rough {rough.mean()/255:.2f}  metal {metal.mean()/255:.2f}  ao {ao.mean()/255:.2f}')
