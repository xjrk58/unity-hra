# Jak znovu vygenerovat assety

Všechno v `Assets/Resources` se dá znovu vytvořit skripty v `Tools/`.
Potřebuješ to jen po změně těch skriptů nebo jejich vstupů; běžné sestavení
používá soubory, které už v repozitáři jsou.

## Materiály a nastavení grafiky

Po přidání nebo změně textury, materiálu nebo nastavení vykreslování:

**Tools ▸ Prdivý závod ▸ Rebuild graphics settings and materials**, nebo

```sh
$UNITY -executeMethod GraphicsSetup.Apply -quit -logFile Logs/setup.log
```

(`$UNITY` jako v návodu [Spuštění automatických kontrol](run-the-automated-checks.md)).
Dá se bezpečně spustit kdykoli. Znovu vytvoří tři render pipeline
v `Assets/Settings`, opraví nastavení importu textur a zapíše všechny materiály
do `Assets/Resources/Materials`. Materiál, o který si kód hry říká jménem, musí
vzniknout tady, jinak v sestaveních chybějí jeho varianty shaderů.

## Modely (Blender)

Potřebuješ Blender 4.

```sh
blender -b --factory-startup -P Tools/blender/build_character.py     # the competitors
blender -b --factory-startup -P Tools/blender/build_kit.py           # railing, column, lamp, goal
blender -b --factory-startup -P Tools/blender/build_monster.py -- --preview Shots/monster
```

Každý zapíše soubory `.fbx` do `Assets/Resources/Models`. `--preview DIR`
navíc vykreslí obrázky příšery. Hra hledá části modelu podle názvů objektů a
slotů materiálů, takže je při úpravě skriptu zachovej.

Jedna past: exportér umístí správně jen **přímé potomky kořenového objektu**.
Část vnořená hlouběji přijde o svou pozici; v souboru ji zavěs na kořen a
v kódu hry jí změň rodiče (takhle to dělá čelist příšery).

## Textury, obloha a rekvizity (Poly Haven, CC0)

Potřebuješ Python 3 s knihovnami Pillow a NumPy. Přesné příkazy uvádí
`Tools/assets/README.md`: `fetch.py` stáhne zdrojové soubory,
`process_textures.py` z nich udělá sady albedo / normal / mask s věrohodným
jasem, `process_sky.py` připraví HDRI oblohu a `convert_props.py` převede
naskenované rekvizity.

## Nahrávky prdů

Potřebuješ NumPy a SciPy.

```sh
curl -L -o /tmp/yle_farts.wav "https://upload.wikimedia.org/wikipedia/commons/b/b7/425594_ylearkisto_farts-pieruja.wav"
python3 Tools/assets/slice_farts.py /tmp/yle_farts.wav Assets/Resources/Audio/Farts --list
```

rozřeže archivní nahrávku na jednotlivé klipy pojmenované `short_NN`,
`medium_NN`, `long_NN` a `fanfare_NN`. Klip, který se ti nelíbí, vyřadíš
smazáním jeho souborů `.wav` a `.wav.meta`. Nahrávka má licenci CC BY 3.0:
zachovej `ATTRIBUTION.txt` v té složce a uvedení autora ve hře
([Assety a licence](../reference/assets-and-licences.md)).

Zvuky příšery nejsou soubory; počítají se při startu hry.
Chceš-li si je poslechnout, **Tools ▸ Prdivý závod ▸ Export synthesised fart sounds**
je zapíše jako `.wav` do `FartSounds/`.

## Ikona aplikace

Potřebuješ Pillow.

```sh
python3 Tools/assets/make_icon.py Assets/Icon.png
```

## Potom

Spusť výše popsané nové vytvoření materiálů, potom snímky obrazovky a kontroly
z návodu [Spuštění automatických kontrol](run-the-automated-checks.md) a
prohlédni si obrázky.
