"""Minimal Radiance .hdr (RGBE, new-style RLE) reader -> float32 numpy array (H, W, 3)."""
import numpy as np

def read_hdr(path):
    with open(path, 'rb') as f:
        data = f.read()
    pos = 0
    # header ends with blank line
    end = data.index(b'\n\n')
    pos = end + 2
    nl = data.index(b'\n', pos)
    res = data[pos:nl].decode('ascii').split()
    pos = nl + 1
    assert res[0] == '-Y' and res[2] == '+X', res
    h, w = int(res[1]), int(res[3])
    buf = np.frombuffer(data, dtype=np.uint8)
    out = np.empty((h, w, 4), dtype=np.uint8)
    for y in range(h):
        assert buf[pos] == 2 and buf[pos + 1] == 2, 'not new RLE'
        assert (int(buf[pos + 2]) << 8 | int(buf[pos + 3])) == w
        pos += 4
        for c in range(4):
            x = 0
            row = out[y, :, c]
            while x < w:
                n = int(buf[pos]); pos += 1
                if n > 128:
                    n -= 128
                    row[x:x + n] = buf[pos]; pos += 1
                else:
                    row[x:x + n] = buf[pos:pos + n]; pos += n
                x += n
    e = out[..., 3].astype(np.int32)
    scale = np.where(e > 0, np.ldexp(1.0, e - 136), 0.0).astype(np.float32)
    return out[..., :3].astype(np.float32) * scale[..., None]
