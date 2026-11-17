"""Cuts a prepared timeline (make_slides.py) into an MP4 with Blender's video
sequence editor, which has the encoder built in - nothing else is needed.

    blender -b --factory-startup -P Tools/video/assemble.py -- WORKDIR/timeline.json OUT.mp4

Each picture drifts slowly (zoom and pan), cross-fades into the next, and
carries its caption on a layer of its own that fades in and out without moving.
A timeline with a "voice" (a finished recording) is as long as that recording;
with "loop" its pictures repeat until the recording ends.
"""
import json
import sys

import bpy

timeline_path, out = sys.argv[sys.argv.index("--") + 1:][:2]
t = json.load(open(timeline_path))

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.resolution_x, scene.render.resolution_y = t["width"], t["height"]
scene.render.resolution_percentage = 100
scene.render.fps = t["fps"]
scene.frame_start, scene.frame_end = 1, t["frames"]
scene.sequence_editor_create()
seq = scene.sequence_editor.sequences
fade = t["fade"]

slides = t["slides"]
voice = t.get("voice")
if voice:
    v = seq.new_sound("voice", voice, 30, 1)
    total = v.frame_final_duration + t["fps"]            # the recording, and a second of quiet
    if t.get("loop"):
        base, slides, start, k = slides, [], 1, 0
        while start < total:
            s = dict(base[k % len(base)])
            s["start"] = start
            slides.append(s)
            start += s["frames"] - fade
            k += 1
    scene.frame_end = total

previous = None
for i, s in enumerate(slides):
    first, frames = s["start"], s["frames"]
    last = first + frames
    pic = seq.new_image(f"pic{i}", s["picture"], 1 + (i % 2), first)
    pic.frame_final_duration = frames
    tr = pic.transform                                   # the slow drift
    for frame, k in ((first, 0.0), (last, 1.0)):
        z = 1.0 + (s["zoom"] - 1.0) * k
        tr.scale_x = tr.scale_y = z
        tr.offset_x, tr.offset_y = s["pan"][0] * k, s["pan"][1] * k
        for prop in ("scale_x", "scale_y", "offset_x", "offset_y"):
            tr.keyframe_insert(prop, frame=frame)
    if previous is not None:
        seq.new_effect(f"cross{i}", 'CROSS', 3, first, frame_end=first + fade, seq1=previous, seq2=pic)
    previous = pic

    cap = seq.new_image(f"cap{i}", s["caption"], 5 + (i % 2), first)
    cap.frame_final_duration = frames
    cap.blend_type = 'ALPHA_OVER'
    for frame, a in ((first, 0.0), (first + fade, 1.0), (last - fade, 1.0), (last, 0.0)):
        cap.blend_alpha = a
        cap.keyframe_insert("blend_alpha", frame=frame)

for i, s in enumerate(t["sounds"]):
    snd = seq.new_sound(f"snd{i}", s["file"], 8 + i, s["frame"])
    snd.volume = s["volume"]

r = scene.render
r.image_settings.file_format = 'FFMPEG'
r.ffmpeg.format = 'MPEG4'
r.ffmpeg.codec = 'H264'
r.ffmpeg.constant_rate_factor = 'MEDIUM'
r.ffmpeg.gopsize = 60
r.ffmpeg.audio_codec = 'AAC'
r.ffmpeg.audio_bitrate = 192
r.use_file_extension = False
r.filepath = out
bpy.ops.render.render(animation=True)
print("WROTE", out)
