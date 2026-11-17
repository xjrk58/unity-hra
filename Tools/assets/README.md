# Asset pipeline

Everything under `Assets/Resources/{Textures,Sky,Models/Props}` is derived from
CC0 assets on [Poly Haven](https://polyhaven.com) by these scripts (Python 3 with
numpy + Pillow; Blender 4.x for the props). Run from the repository root:

```sh
python3 Tools/assets/fetch.py            /tmp/polyhaven          # download the originals
python3 Tools/assets/process_textures.py /tmp/polyhaven Assets/Resources/Textures
python3 Tools/assets/process_sky.py      /tmp/polyhaven/autumn_field_puresky/autumn_field_puresky_4k.hdr \
                                         Assets/Resources/Sky/Sky.hdr Tools/assets
blender -b -P Tools/assets/convert_props.py -- /tmp/polyhaven Assets/Resources/Models/Props
```

* `process_textures.py` — writes `<Name>_albedo.jpg`, `<Name>_normal.jpg` (OpenGL convention) and
  `<Name>_mask.png` (R = metallic, G = ambient occlusion, A = smoothness, the layout URP/Lit expects).
  Dark scans are rescaled in linear light to a plausible real-world reflectance.
* `process_sky.py` — clamps the sun disc (the game's directional light supplies the sun) and replaces the
  mirrored lower half of the "pure sky" HDRI with a sun-lit ground colour.
* `convert_props.py` — glTF → single-mesh FBX, origin at bottom centre, decimated to ≤ 9k triangles.

## Fart recordings

`Assets/Resources/Audio/Farts` is cut from one archive recording, *"Farts - pieruja"* by Yle Arkisto
(CC BY 3.0 — attribution required, see the `ATTRIBUTION.txt` in that folder). Needs numpy + scipy:

```sh
curl -L -o /tmp/yle_farts.wav "https://upload.wikimedia.org/wikipedia/commons/b/b7/425594_ylearkisto_farts-pieruja.wav"
python3 Tools/assets/slice_farts.py /tmp/yle_farts.wav Assets/Resources/Audio/Farts --list
```

It finds every loud event, drops handling noise and repeats, and writes `short_NN` / `medium_NN` /
`long_NN` / `fanfare_NN` WAVs (NN = position in the recording). To remove a clip you don't like from
the game, just delete its `.wav` (and `.meta`).

## App icon

`Assets/Icon.png` (the icon of the built game, on Android the one on the home screen) is original
artwork drawn by a script; needs Pillow:

```sh
python3 Tools/assets/make_icon.py Assets/Icon.png
```

## Models

The Blender scripts in `Tools/blender/` build the original models (character, fart monster, railing,
column, lamp, goal) from nothing: `blender -b -P Tools/blender/build_character.py` / `build_kit.py` /
`build_monster.py` (add `-- --preview DIR` to the last one for renders of the monster).

After changing assets, run **Tools ▸ Prdivý závod ▸ Rebuild graphics settings and materials** in Unity.
