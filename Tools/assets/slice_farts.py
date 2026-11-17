"""Cut the Yle archive recording "Farts - pieruja" (many real farts in one file) into
individual clips for the game.

    python3 slice_farts.py <yle_farts.wav> <out_dir> [--list]

Source: https://commons.wikimedia.org/wiki/File:425594_ylearkisto_farts-pieruja.wav
        (Yle Arkisto / Freesound #425594, recorded 1986, CC BY 3.0)

Each detected event becomes one mono 16-bit WAV: short_NN (<= 0.6 s), medium_NN (<= 1.1 s),
long_NN (the rest; the longest one is saved as fanfare_NN). NN is the event's index in the
recording, so names are stable. Changes made to the original: cut, mixed to mono, 40 Hz
high-pass, short fades, loudness-normalised.
"""
import sys, os, json, warnings
import numpy as np
from scipy.io import wavfile
from scipy import signal
warnings.simplefilter('ignore')

SRC, OUT = sys.argv[1], sys.argv[2]
sr, x = wavfile.read(SRC)
x = x.astype(np.float64)
x /= np.abs(x).max()
mono = x.mean(1) if x.ndim > 1 else x
b, a = signal.butter(2, 40 / (sr / 2), 'high')
mono = signal.lfilter(b, a, mono)

# loudness envelope: RMS over 20 ms every 5 ms
HOP = 0.005
hop, win = int(HOP * sr), int(0.02 * sr)
n = (len(mono) - win) // hop
env = np.sqrt(np.array([np.mean(mono[i * hop:i * hop + win] ** 2) for i in range(n)]))
floor = np.percentile(env, 20)
thr = max(floor * 6, env.max() * 0.012)
active = env > thr

# an event = loud stretch; pauses under 150 ms stay inside it (sputtering farts are one event)
gap = int(0.15 / HOP)
events, i = [], 0
while i < n:
    if not active[i]: i += 1; continue
    j = i
    while j < n and (active[j] or active[j:j + gap].any()): j += 1
    while j > i and not active[j - 1]: j -= 1
    if (j - i) * HOP >= 0.07: events.append((i, j))
    i = j + 1

def bands(seg):
    fr, P = signal.welch(seg, sr, nperseg=min(4096, len(seg)))
    return [float(P[(fr >= lo) & (fr < hi)].sum() / P.sum()) for lo, hi in ((0, 200), (200, 800), (800, 3000), (3000, sr / 2))]

clips, kept = [], []
for idx, (s, e) in enumerate(events):
    a0 = max(0, s * hop - int(0.025 * sr))
    a1 = min(len(mono), e * hop + win + int(0.14 * sr))       # keep the natural tail / room decay
    seg = mono[a0:a1].copy()
    peak = float(np.abs(seg).max())
    if peak < 0.12: continue                                   # handling noise, not a fart
    feat = [len(seg) / sr] + bands(seg)
    # the tape repeats a few events: skip near-identical ones
    if any(abs(k[0] - feat[0]) < 0.04 * max(k[0], feat[0]) + 0.02 and all(abs(p - q) < 0.035 for p, q in zip(k[1:], feat[1:])) for k in kept):
        continue
    kept.append(feat)
    fi, fo = int(0.004 * sr), int(0.10 * sr)
    seg[:fi] *= np.linspace(0, 1, fi)
    seg[-fo:] *= np.linspace(1, 0, fo) ** 2
    rms = np.sqrt(np.mean(seg ** 2))
    seg *= min(0.92 / peak, 0.16 / rms)                        # even loudness, never clipping
    clips.append((idx, a0 / sr, seg, feat))

longest = max(clips, key=lambda c: len(c[2]))[0]
os.makedirs(OUT, exist_ok=True)
for f in os.listdir(OUT):
    if f.endswith('.wav'): os.remove(os.path.join(OUT, f))
index = []
for idx, t0, seg, feat in clips:
    dur = len(seg) / sr
    kind = 'fanfare' if idx == longest else 'short' if dur <= 0.6 else 'medium' if dur <= 1.1 else 'long'
    name = f'{kind}_{idx:02d}.wav'
    wavfile.write(os.path.join(OUT, name), sr, (np.clip(seg, -1, 1) * 32767).astype(np.int16))
    index.append(dict(file=name, source_time=round(t0, 2), duration=round(dur, 2), bands=[round(v, 2) for v in feat[1:]]))
    if '--list' in sys.argv: print(f'{name:16s} from {t0:6.1f}s  {dur:4.2f}s  <200:{feat[1]:.2f} 200-800:{feat[2]:.2f} 800-3k:{feat[3]:.2f} >3k:{feat[4]:.2f}')
json.dump(index, open(os.path.join(OUT, 'index.json'), 'w'), indent=1)
kinds = {}
for c in index: kinds[c['file'].split('_')[0]] = kinds.get(c['file'].split('_')[0], 0) + 1
print(f'{len(index)} clips -> {OUT}: {kinds}')
