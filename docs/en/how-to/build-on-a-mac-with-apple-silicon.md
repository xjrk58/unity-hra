# How to build on a Mac with Apple silicon (M1 and later)

This guide takes a Mac with an M1, M2, M3 or M4 chip from nothing to a running
`PrdivyZavod.app`, and optionally to an Android APK built on the Mac.

> **Status:** these steps follow the project's build script and Unity's
> standard macOS workflow, but the authors have not run them on a Mac. If a
> step behaves differently, the Linux instructions and the build log are the
> reference. See [Verification status](../reference/verification-status.md).

## What you need

- macOS 12 or later on Apple silicon, about 20 GB of free disk space.
- A Unity account (the free Personal licence is enough).
- Git. If `git --version` asks to install the command line tools, let it.

Rosetta is **not** needed: Unity Hub and the Unity 6 editor both exist as
native Apple silicon programs.

## 1. Install Unity Hub and the editor

1. Download **Unity Hub** for macOS from <https://unity.com/download>, open the
   `.dmg` and drag it to *Applications*. Start it and sign in.
2. **Installs ▸ Install Editor**. Find **6000.4.11f1** (under *Archive* if it
   is not listed) and, where the Hub offers a choice, pick the
   **Apple silicon** editor, not *Intel*.
3. On the module page tick:
   - **Mac Build Support (IL2CPP)** – optional; the default Mono backend that
     the build script uses is part of the editor already;
   - **Android Build Support** together with **OpenJDK** and
     **Android SDK & NDK Tools** – only if you also want to build the APK.
4. Install. The editor ends up in
   `/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app`.

To check that you got the native editor: in *Finder* select `Unity.app`,
*File ▸ Get Info* – *Kind* should say **Application (Apple silicon)**.

## 2. Get the project and open it

```sh
git clone git@github.com:xjrk58/unity-hra.git
cd unity-hra
```

In Unity Hub: **Projects ▸ Add ▸ Add project from disk**, pick the folder,
open it. The first import of textures and models takes several minutes. macOS
may ask whether Unity may access the folder – allow it.

Open `Assets/Scenes/Main.unity` and press **Play** once to make sure the game
runs (click **Hrát sám**).

## 3. Choose the processor architecture

**File ▸ Build Profiles ▸ macOS ▸ Architecture** decides what the app runs on:

| Setting | Runs on | Use it when |
|---------|---------|-------------|
| **Apple silicon** | M-series Macs only | the build is for your own Mac (smallest, fastest) |
| **Intel 64-bit + Apple silicon** | every Mac | you want to give the app to others |
| Intel 64-bit | Intel Macs; on M-series only through Rosetta | you have a reason |

If macOS is not the active platform in that window yet, you do not have to
switch to it; the build script does.

## 4. Build

**Tools ▸ Prdivý závod ▸ Build player.** On a Mac the script builds for macOS
and writes `Builds/PrdivyZavod/PrdivyZavod.app`.

The same from Terminal, with the editor closed:

```sh
/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -buildTarget StandaloneOSX \
  -executeMethod BuildGame.Build -quit -logFile Logs/build.log
tail -5 Logs/build.log        # look for "[Setup] build Succeeded"
```

Unity Hub must be running and signed in while this runs, otherwise the log
says `No valid Unity Editor license found`.

## 5. Run it

```sh
open Builds/PrdivyZavod/PrdivyZavod.app
```

An app built on your own Mac starts without complaints. The first time you
host or join a two-player game macOS asks two things; answer **Allow** to both:

- *"…would like to find and connect to devices on your local network"*
  (macOS 15 and later);
- *"Do you want the application … to accept incoming network connections?"*
  (the firewall, only when hosting).

To check which processors the app was built for:

```sh
lipo -archs Builds/PrdivyZavod/PrdivyZavod.app/Contents/MacOS/*
# arm64            -> Apple silicon only
# x86_64 arm64     -> universal
```

## 6. Give the app to somebody else

A copy that travelled through the internet or a chat is quarantined by macOS,
and because the app is not signed with an Apple developer certificate it is
refused with *"is damaged"* or *"cannot be opened"*. On the receiving Mac:

```sh
xattr -dr com.apple.quarantine /path/to/PrdivyZavod.app
```

or right-click the app, **Open**, and confirm (on macOS 15: *System Settings ▸
Privacy & Security ▸ Open Anyway*). Zip the app before sending it
(`ditto -c -k --keepParent PrdivyZavod.app PrdivyZavod.zip`) so that its
structure survives.

Proper distribution needs an Apple Developer account, signing and notarisation;
that is outside this guide.

## 7. Build the Android APK on the Mac (optional)

With the Android modules from step 1 installed:

**Tools ▸ Prdivý závod ▸ Build Android APK**, or

```sh
/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -buildTarget Android \
  -executeMethod BuildGame.BuildAndroid -quit -logFile Logs/android.log
```

The APK is `Builds/Android/PrdivyZavod.apk`. `adb`, for installing it on a
phone, is inside the editor:

```sh
ADB=/Applications/Unity/Hub/Editor/6000.4.11f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB install -r Builds/Android/PrdivyZavod.apk
```

Continue with [Install the game on an Android phone](install-on-an-android-phone.md);
for the variant for old devices see [Build for old Android phones](build-for-old-android-phones.md).

## 8. The developer tools on a Mac (optional)

The headless checks work the same as on Linux with the Mac editor path and
`-buildTarget StandaloneOSX`:

```sh
UNITY="/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -buildTarget StandaloneOSX"
$UNITY -executeMethod MonsterTestRunner.Run -logFile Logs/monster.log
$UNITY -executeMethod AnimalTestRunner.Run -logFile Logs/animals.log
```

The asset tools need Python 3 with Pillow, NumPy and SciPy
(`python3 -m pip install pillow numpy scipy`) and Blender
(`brew install --cask blender`; its command is
`/Applications/Blender.app/Contents/MacOS/Blender`). `Tools/dev/nettest.sh`
expects the Linux build's file name; on a Mac start the two copies by hand as
described in [Run the automated checks](run-the-automated-checks.md).

## Troubleshooting

| Symptom | Cause and fix |
|---------|---------------|
| The Hub only offers an Intel editor | Update Unity Hub; old Hub versions did not know the Apple silicon editors. |
| `No valid Unity Editor license found` | Start Unity Hub and sign in, then run the command again. |
| Build fails with *Mac Build Support not installed* | Building for macOS on a Mac needs no module with the Mono backend; if the project was switched to IL2CPP, add **Mac Build Support (IL2CPP)** in the Hub. |
| The app opens and closes at once on another Mac | It was built *Apple silicon* and the other Mac is Intel: build *Intel 64-bit + Apple silicon*. |
| *"PrdivyZavod is damaged and can't be opened"* | Quarantine, see step 6. |
| The other player cannot find or join the game | Local-network permission or firewall, see step 5 and [Play with two players](play-with-two-players.md). |
| Pink (magenta) surfaces in the editor | The import was interrupted: **Tools ▸ Prdivý závod ▸ Rebuild graphics settings and materials**. |
