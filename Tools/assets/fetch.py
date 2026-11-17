"""Download selected CC0 Poly Haven assets into dl/raw/<id>/ (untrusted data dir)."""
import json, os, sys, urllib.request, urllib.parse
RAW = sys.argv[1]
UA = {'User-Agent': 'asset-fetch/1.0'}
def get(url, dest):
    if os.path.exists(dest) and os.path.getsize(dest) > 0: return
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    req = urllib.request.Request(url, headers=UA)
    with urllib.request.urlopen(req, timeout=180) as r, open(dest + '.part', 'wb') as f:
        while True:
            b = r.read(1 << 16)
            if not b: break
            f.write(b)
    os.replace(dest + '.part', dest)
def files(id):
    p = os.path.join(RAW, id, 'files.json')
    if not os.path.exists(p):
        os.makedirs(os.path.dirname(p), exist_ok=True)
        get('https://api.polyhaven.com/files/' + urllib.parse.quote(id), p)
    return json.load(open(p))
def safe(name):
    name = name.replace('\\', '/')
    assert not name.startswith('/') and '..' not in name.split('/'), name
    return name
TEX = {'concrete_floor_worn_001': '2k', 'concrete_layers_02': '2k', 'asphalt_02': '2k', 'aerial_grass_rock': '2k',
       'denmin_fabric_02': '1k', 'brown_leather': '1k', 'metal_plate': '1k'}
MODELS = ['Barrel_01', 'barrel_03', 'concrete_road_barrier', 'plastic_crate_02', 'cardboard_box_01']
for id, res in TEX.items():
    d = files(id)
    for kind in ('Diffuse', 'nor_gl', 'arm'):
        info = d[kind][res]['jpg']
        get(info['url'], os.path.join(RAW, id, safe(os.path.basename(info['url']))))
    print('tex', id, res)
for id in MODELS:
    d = files(id)
    g = d['gltf']['1k']['gltf']
    get(g['url'], os.path.join(RAW, id, safe(os.path.basename(g['url']))))
    for rel, info in g.get('include', {}).items():
        get(info['url'], os.path.join(RAW, id, safe(rel)))
    print('model', id, len(g.get('include', {})), 'includes')
h = files('autumn_field_puresky')['hdri']['4k']['hdr']
get(h['url'], os.path.join(RAW, 'autumn_field_puresky', 'autumn_field_puresky_4k.hdr'))
print('hdri ok')
