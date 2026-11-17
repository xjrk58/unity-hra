"""Prepare the HDRI for realtime use:
   - clamp the sun disc (the directional light supplies the sun; an unclamped sun double-lights
     the scene through the ambient probe and causes fireflies in reflection probes)
   - replace the mirrored lower hemisphere with a plausible sun-lit ground colour so ambient
     light from below (ceilings!) is a warm ground bounce instead of sky blue
   Writes Radiance .hdr with RLE."""
import sys, numpy as np
sys.path.insert(0, sys.argv[3])
from hdr import read_hdr
SRC, DST = sys.argv[1], sys.argv[2]
SUN_CLAMP = 24.0
GROUND = np.array([0.40, 0.37, 0.28], np.float32)

im = read_hdr(SRC); h, w, _ = im.shape
mx = im.max(-1, keepdims=True)
im = im * np.minimum(1.0, SUN_CLAMP / np.maximum(mx, 1e-6))

elev = 90 - (np.arange(h) + 0.5) / h * 180            # degrees, per row
horizon_row = im[h // 2 - 2: h // 2].mean(0)           # (w, 3) colour right at the horizon
for y in range(h // 2, h):
    t = np.clip((-elev[y] - 1.0) / 11.0, 0, 1)         # 0 at the horizon haze, 1 from -12 deg down
    t = t * t * (3 - 2 * t)
    im[y] = horizon_row * (1 - t) + GROUND * t

def encode_rgbe(img):
    m = img.max(-1)
    e = np.zeros(m.shape, np.int32); f = np.zeros_like(m)
    nz = m > 1e-32
    f[nz], e[nz] = np.frexp(m[nz])
    scale = np.where(nz, f * 256.0 / np.maximum(m, 1e-32), 0)
    out = np.zeros(img.shape[:2] + (4,), np.uint8)
    out[..., :3] = np.clip(img * scale[..., None], 0, 255).astype(np.uint8)
    out[..., 3] = np.where(nz, e + 128, 0).astype(np.uint8)
    return out

def rle_channel(row):
    n = len(row); out = bytearray()
    change = np.flatnonzero(np.diff(row)) + 1
    starts = np.concatenate(([0], change)); lens = np.diff(np.concatenate((starts, [n])))
    long_idx = np.flatnonzero(lens >= 4)
    pos = 0
    def literals(a, b):
        while a < b:
            c = min(128, b - a); out.append(c); out.extend(row[a:a + c].tobytes()); a += c
    for i in long_idx:
        s, l = int(starts[i]), int(lens[i])
        literals(pos, s)
        v = int(row[s])
        while l > 0:
            c = min(127, l); out.append(128 + c); out.append(v); l -= c
        pos = s + int(lens[i])
    literals(pos, n)
    return out

rgbe = encode_rgbe(im)
with open(DST, 'wb') as f:
    f.write(b'#?RADIANCE\nFORMAT=32-bit_rle_rgbe\n\n')
    f.write(f'-Y {h} +X {w}\n'.encode())
    for y in range(h):
        f.write(bytes([2, 2, w >> 8, w & 255]))
        for c in range(4):
            f.write(rle_channel(rgbe[y, :, c]))

back = read_hdr(DST)
err = np.abs(back - im).max() / max(im.max(), 1)
lum = back @ np.array([0.2126, 0.7152, 0.0722], np.float32)
print('written', DST, 'max', back.max(), 'roundtrip rel err', err)
theta = (np.arange(h) + .5) / h * np.pi; sa = np.sin(theta); cosw = np.cos(theta)
up = slice(0, h // 2); dn = slice(h // 2, h)
E_up = (back[up] * (sa[up] * cosw[up])[:, None, None]).sum((0, 1)) * (np.pi / h) * (2 * np.pi / w)
E_dn = (back[dn] * (sa[dn] * -cosw[dn])[:, None, None]).sum((0, 1)) * (np.pi / h) * (2 * np.pi / w)
print('irradiance/pi on up-facing surface', np.round(E_up / np.pi, 3), ' down-facing', np.round(E_dn / np.pi, 3))
from PIL import Image
tm = back / (1 + back); # Image.fromarray((np.clip(tm, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8)).resize((1000, 500)).save('sky_preview.jpg')
