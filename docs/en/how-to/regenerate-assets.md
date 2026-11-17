# How to regenerate the assets

Everything in `Assets/Resources` can be rebuilt from scripts in `Tools/`. You
only need this after changing those scripts or their inputs; a normal build
uses the files that are already in the repository.

## Materials and graphics settings

After adding or changing a texture, a material or a render setting:

**Tools ▸ Prdivý závod ▸ Rebuild graphics settings and materials**, or

```sh
$UNITY -executeMethod GraphicsSetup.Apply -quit -logFile Logs/setup.log
```

(`$UNITY` as in [Run the automated checks](run-the-automated-checks.md)). It is
safe to run at any time. It recreates the three render pipelines in
`Assets/Settings`, fixes texture import settings and writes every material in
`Assets/Resources/Materials`. A material that game code asks for by name must
be created here, otherwise its shader variants are missing from builds.

## Models (Blender)

Needs Blender 4.

```sh
blender -b --factory-startup -P Tools/blender/build_character.py     # the competitors
blender -b --factory-startup -P Tools/blender/build_kit.py           # railing, column, lamp, goal
blender -b --factory-startup -P Tools/blender/build_monster.py -- --preview Shots/monster
blender -b --factory-startup -P Tools/blender/build_horse.py -- --preview Shots/horse     # horses and unicorns: one model
```

Each writes `.fbx` files into `Assets/Resources/Models`. `--preview DIR` also
renders pictures of the monster, or of the horse and the unicorn. The game
finds parts of a model by the names of objects and material slots, so keep
those when you edit a script. A horse and a unicorn are the same model: the
game shows the horn on unicorns only and gives every animal its own coat.

One trap: the exporter only places **direct children of the root object**
correctly. A part nested deeper loses its position; hang it on the root in the
file and re-parent it in game code (the monster's jaw and the unicorn's horn
do this).

## Textures, sky and props (Poly Haven, CC0)

Needs Python 3 with Pillow and NumPy. `Tools/assets/README.md` lists the exact
commands: `fetch.py` downloads the source files, `process_textures.py` turns
them into albedo / normal / mask sets with plausible brightness,
`process_sky.py` prepares the HDRI sky, `convert_props.py` converts the scanned
props.

## Fart recordings

Needs NumPy and SciPy.

```sh
curl -L -o /tmp/yle_farts.wav "https://upload.wikimedia.org/wikipedia/commons/b/b7/425594_ylearkisto_farts-pieruja.wav"
python3 Tools/assets/slice_farts.py /tmp/yle_farts.wav Assets/Resources/Audio/Farts --list
```

cuts the archive recording into individual clips named `short_NN`,
`medium_NN`, `long_NN` and `fanfare_NN`. To drop a clip you do not like, delete
its `.wav` and `.wav.meta`. The recording is CC BY 3.0: keep
`ATTRIBUTION.txt` in that folder and the credit in the game
([Assets and licences](../reference/assets-and-licences.md)).

The monster's sounds are not files; they are computed when the game starts.
To listen to them, **Tools ▸ Prdivý závod ▸ Export synthesised fart sounds**
writes them as `.wav` into `FartSounds/`.

## App icon

Needs Pillow.

```sh
python3 Tools/assets/make_icon.py Assets/Icon.png
```

## Afterwards

Run the material rebuild above, then the screenshots and checks from
[Run the automated checks](run-the-automated-checks.md), and look at the
pictures.
