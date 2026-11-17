#!/usr/bin/env python3
"""Prepares everything a video is assembled from: full-frame pictures, caption
overlays, a music track and a timeline.

    python3 Tools/video/make_slides.py STORYBOARD.json WORKDIR [--voice SOUNDFILE]

With --voice the given recording (a podcast, say) becomes the sound track; a
storyboard with "loop": true then repeats its pictures for as long as it lasts.

A storyboard is a list of slides (see Tools/video/*.json): which screenshot,
how to fit it, a title, a text, how long, and sounds to play. The result is
WORKDIR/timeline.json, read by assemble.py (run inside Blender), which cuts it
all into an MP4. Needs Pillow and NumPy. The music is computed here - an
oom-pah tuba vamp - so there is nothing to license.
"""
import json
import math
import os
import sys
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H, FPS = 1920, 1080, 30
FADE = 12                                   # frames of cross-fade between slides
BOLD = "/usr/share/fonts/truetype/noto/NotoSans-Bold.ttf"
REGULAR = "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf"
if not os.path.exists(REGULAR):
    BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
    REGULAR = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
GOLD, WHITE = (255, 217, 51), (255, 255, 255)


def cover(im):
    s = max(W / im.width, H / im.height)
    im = im.resize((math.ceil(im.width * s), math.ceil(im.height * s)), Image.LANCZOS)
    x, y = (im.width - W) // 2, (im.height - H) // 2
    return im.crop((x, y, x + W, y + H))


def frame(path, fit):
    """The screenshot as a 1920x1080 picture: cropped to fill, or shown whole on a blurred copy of itself."""
    im = Image.open(path).convert("RGB")
    if fit == "cover":
        return cover(im)
    back = cover(im).filter(ImageFilter.GaussianBlur(28)).point(lambda v: int(v * 0.45))
    s = min(W / im.width, (H - 40) / im.height)
    small = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
    back.paste(small, ((W - small.width) // 2, (H - small.height) // 2))
    return back


def wrap(draw, text, font, width):
    lines = []
    for para in text.split("\n"):
        line = ""
        for word in para.split():
            trial = (line + " " + word).strip()
            if draw.textlength(trial, font=font) <= width:
                line = trial
            else:
                lines.append(line)
                line = word
        lines.append(line)
    return lines


def caption(title, text, where, brand):
    """A transparent overlay with the caption box (bottom, top or centre)."""
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    if not title and not text:                      # a picture on its own: just the name in the corner
        if brand:
            d.text((112, 36), brand, font=ImageFont.truetype(BOLD, 30), fill=(255, 255, 255, 215),
                   stroke_width=3, stroke_fill=(0, 0, 0, 200))
        return im
    big = where == "centre"
    # (the stars of a rating are not in Noto Sans; DejaVu has them)
    title_font = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf" if "\u2605" in title else BOLD
    f_title = ImageFont.truetype(title_font, 120 if big else 60)
    f_text = ImageFont.truetype(REGULAR if not big else BOLD, 54 if big else 42)
    f_brand = ImageFont.truetype(BOLD, 30)
    lines = wrap(d, text, f_text, W - 360) if text else []
    line_h = int(f_text.size * 1.32)
    th = f_title.size + 26 if title else 0
    box_h = 44 + th + len(lines) * line_h + 36
    box_w = W - 200
    x0 = 100
    y0 = {"bottom": H - box_h - 70, "top": 70, "centre": (H - box_h) // 2}[where]
    d.rounded_rectangle([x0, y0, x0 + box_w, y0 + box_h], radius=30, fill=(0, 0, 0, 185))
    y = y0 + 36
    if title:
        tw = d.textlength(title, font=f_title)
        d.text(((W - tw) / 2, y), title, font=f_title, fill=GOLD)
        y += th
    for line in lines:
        lw = d.textlength(line, font=f_text)
        d.text(((W - lw) / 2, y), line, font=f_text, fill=WHITE)
        y += line_h
    if brand and not big:
        d.text((112, 36 if where != "top" else H - 76), brand, font=f_brand, fill=(255, 255, 255, 215),
               stroke_width=3, stroke_fill=(0, 0, 0, 200))
    return im


# ---------------------------------------------------------------- music

def tone(freq, seconds, sr, harmonics, attack=0.01, decay=0.25, bend=0.0):
    t = np.arange(int(seconds * sr)) / sr
    f = freq * (1 + bend * np.exp(-t / 0.03))
    phase = 2 * np.pi * np.cumsum(f) / sr
    x = sum(a * np.sin(k * phase) for k, a in harmonics)
    return x * np.minimum(1, t / attack) * np.exp(-t / decay)


def music(seconds, sr=44100, bpm=104):
    """An oom-pah vamp: tuba on one and three, a short chord on two and four, a little tune on top."""
    beat = 60 / bpm
    note = lambda n: 440 * 2 ** ((n - 69) / 12)                      # MIDI number -> Hz
    chords = [(48, (60, 64, 67)), (53, (60, 65, 69)), (55, (59, 62, 67)), (48, (60, 64, 67)),
              (48, (60, 64, 67)), (57, (60, 64, 69)), (53, (60, 65, 69)), (55, (59, 62, 67))]   # C F G C | C Am F G
    tune = [[76, 79, 84, 79], [77, 81, 84, 81], [79, 83, 86, 83], [84, 79, 76, 72],
            [76, 79, 84, 79], [76, 81, 84, 81], [77, 81, 84, 77], [79, 77, 74, 71]]
    n = int(seconds * sr)
    out = np.zeros(n + sr)
    tuba = [(1, 1.0), (2, 0.55), (3, 0.3), (4, 0.12)]
    reed = [(1, 1.0), (2, 0.25), (3, 0.35), (5, 0.08)]
    bars = int(seconds / (4 * beat)) + 1
    for b in range(bars):
        root, triad = chords[b % len(chords)]
        for q in range(4):
            at = int((b * 4 + q) * beat * sr)
            if at >= n:
                break
            if q % 2 == 0:                                            # oom
                bass = root - 12 + (7 if q == 2 else 0)
                x = 0.9 * tone(note(bass), beat * 0.9, sr, tuba, decay=0.22, bend=0.04)
            else:                                                     # pah
                x = sum(0.22 * tone(note(m), beat * 0.45, sr, reed, attack=0.005, decay=0.07) for m in triad)
            out[at:at + len(x)] += x
            m = tune[b % len(tune)][q]                                # the tune, an octave of whistling
            y = 0.16 * tone(note(m), beat * 0.8, sr, [(1, 1.0), (2, 0.1)], attack=0.02, decay=0.18)
            out[at:at + len(y)] += y
    out = out[:n]
    out *= 0.5 / max(1e-9, np.abs(out).max())
    fade = int(1.5 * sr)
    out[-fade:] *= np.linspace(1, 0, fade)
    return (out * 32767).astype("<i2"), sr


def repitch(path, pitch, out):
    """Writes a copy of a mono 16-bit WAV played `pitch` times as fast."""
    with wave.open(path, "rb") as w:
        sr, n = w.getframerate(), w.getnframes()
        x = np.frombuffer(w.readframes(n), dtype="<i2").astype(np.float64)
        if w.getnchannels() == 2:
            x = x.reshape(-1, 2).mean(axis=1)
    y = np.interp(np.arange(0, len(x) - 1, pitch), np.arange(len(x)), x)
    with wave.open(out, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes(y.astype("<i2").tobytes())
    return out


def main(storyboard_path, work, voice=None):
    board = json.load(open(storyboard_path, encoding="utf-8"))
    if voice:
        board["voice"] = os.path.abspath(voice)
    root = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
    os.makedirs(work, exist_ok=True)
    slides, sounds = [], []
    start = 1
    for i, s in enumerate(board["slides"]):
        pic = os.path.join(work, f"slide{i:02d}.png")
        cap = os.path.join(work, f"caption{i:02d}.png")
        frame(os.path.join(root, s["image"]), s.get("fit", "cover")).save(pic)
        caption(s.get("title", ""), s.get("text", ""), s.get("where", "bottom"), board.get("brand", "")).save(cap)
        frames = int(round(s["seconds"] * FPS))
        slides.append({"picture": pic, "caption": cap, "start": start, "frames": frames + FADE,
                       "zoom": s.get("zoom", 1.06), "pan": s.get("pan", [0, 0])})
        for k, snd in enumerate(s.get("sounds", [])):
            path = os.path.join(root, snd["file"])
            if snd.get("pitch", 1.0) != 1.0:                         # played faster = higher
                path = repitch(path, snd["pitch"], os.path.join(work, f"sound{i:02d}_{k}.wav"))
            sounds.append({"file": path, "frame": start + int(snd.get("at", 0.3) * FPS), "volume": snd.get("volume", 0.8)})
        start += frames
    total = start + FADE
    if board.get("music", True):
        data, sr = music(total / FPS)
        path = os.path.join(work, "music.wav")
        with wave.open(path, "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr); w.writeframes(data.tobytes())
        sounds.append({"file": path, "frame": 1, "volume": board.get("music_volume", 0.35)})
    json.dump({"width": W, "height": H, "fps": FPS, "fade": FADE, "frames": total, "slides": slides, "sounds": sounds,
               "voice": board.get("voice"), "loop": bool(board.get("loop"))},
              open(os.path.join(work, "timeline.json"), "w"), indent=1)
    print(f"{len(slides)} slides, {total / FPS:.1f} s -> {work}/timeline.json")


if __name__ == "__main__":
    args = sys.argv[1:]
    voice = None
    if "--voice" in args:
        i = args.index("--voice")
        voice = args[i + 1]
        del args[i:i + 2]
    main(args[0], args[1], voice)
