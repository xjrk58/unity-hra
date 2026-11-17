# How to build for Linux and Windows

The build script builds for whatever desktop system the editor is running on.

**Before you start:** Unity 6000.4.11f1 installed through Unity Hub, the
project opened once so that its assets are imported
([tutorial](../tutorials/02-build-and-run-from-source.md)).

## From the editor

**Tools ▸ Prdivý závod ▸ Build player.** The result is in `Builds/PrdivyZavod/`:

| System | Start this |
|--------|------------|
| Linux | `Builds/PrdivyZavod/PrdivyZavod.x86_64` |
| Windows | `Builds\PrdivyZavod\PrdivyZavod.exe` |

Keep the whole folder together; the `PrdivyZavod_Data` folder next to the
program is part of the game.

## Without opening the editor

Close the editor first (two editors cannot have the project open), then:

```sh
# Linux
~/Unity/Hub/Editor/6000.4.11f1/Editor/Unity -batchmode -projectPath . \
  -buildTarget StandaloneLinux64 -executeMethod BuildGame.Build -quit -logFile Logs/build.log
```

```bat
:: Windows
"C:\Program Files\Unity\Hub\Editor\6000.4.11f1\Editor\Unity.exe" -batchmode -projectPath . ^
  -buildTarget StandaloneWindows64 -executeMethod BuildGame.Build -quit -logFile Logs\build.log
```

The log ends with `[Setup] build Succeeded: …`; the exit code is 0 on success
and 3 if the build failed. Add `-buildDir SOMEWHERE` to put the result
elsewhere.

## Check the build

```sh
Builds/PrdivyZavod/PrdivyZavod.x86_64 -batchmode -captureShot /tmp/shot.png
```

runs a few seconds of a solo match without a window, writes one screenshot and
quits. See [Run the automated checks](run-the-automated-checks.md) for more.

## Troubleshooting

- **`No valid Unity Editor license found`** in the log: Unity Hub must be
  running and signed in while the editor runs without a window.
- **The game window is huge or slow:** it starts full-screen at the desktop
  resolution. Lower MSAA or the shadow resolution in
  `Assets/Settings/GamePipeline.asset`, see [Graphics tiers](../reference/graphics-tiers.md).
- **A second player cannot join a game hosted on this computer:** the firewall
  must allow incoming TCP port 47800 (and UDP 47801 for the game to show up in
  the list). See [Play with two players](play-with-two-players.md).

The Linux build is the one this project is developed and tested with. The
Windows build uses the same script but has not been run by the authors.
