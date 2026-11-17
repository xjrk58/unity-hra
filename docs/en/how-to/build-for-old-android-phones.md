# How to build for old Android phones

The standard APK ([how to build it](build-the-android-apk.md)) needs a 64-bit
device and draws the game in a quality meant for phones of the last few years.
This guide builds a second APK, `PrdivyZavod-old.apk`, for devices that are
old, slow or 32-bit, and tells you how to find out whether a device can run the
game at all.

> **Status:** the build has been produced and inspected, and its graphics level
> has been rendered on a computer. It has **not** been run on an actual old
> device. See [Verification status](../reference/verification-status.md).

## What an old device needs

| Requirement | Why | How to check (with `adb`, see below) |
|-------------|-----|--------------------------------------|
| Android **7.1** (API 25) or newer | the lowest version Unity 6 can build for | `adb shell getprop ro.build.version.sdk` → 25 or more |
| An ARM processor, 32- or 64-bit | the APK contains `armeabi-v7a` and `arm64-v8a` code; x86 devices are not covered | `adb shell getprop ro.product.cpu.abilist` |
| **OpenGL ES 3.0** or Vulkan | Unity 6 has no OpenGL ES 2 renderer | `adb shell dumpsys SurfaceFlinger \| grep -i "GLES"` → `OpenGL ES 3.x` |
| About 2 GB of RAM, 300 MB free storage | textures, sounds and the 100 MB APK | *Settings ▸ About phone* |

Devices on Android 7.0 or older, or with a graphics chip that only does
OpenGL ES 2.0 (many phones from before about 2014), cannot run the game in any
build made from this project.

## How the old-phone build differs

| | Standard APK | Old-phone APK |
|---|---|---|
| File | `PrdivyZavod.apk` (about 78 MB) | `PrdivyZavod-old.apk` (about 100 MB) |
| Processors | ARM64 | ARM64 **and 32-bit ARMv7** |
| Graphics level on start | *Medium* | *Low*, the lightest |
| Resolution rendered | 80 % | 60 % |
| Anti-aliasing | 2× | off |
| Sun shadows | 2048 px, 2 cascades, soft | 1024 px, 1 cascade, hard, 28 m |
| Bloom | on | off |
| Ceiling lamps lit | all | only on the deck you are on |
| Minimum Android | 7.1 | 7.1 |

Everything else – rules, monster, two players, sounds – is the same, and the
two builds can play each other. Both have the same package name, so installing
one replaces the other.

## Build it

You need the Android module as described in
[Build the Android APK](build-the-android-apk.md).

**Tools ▸ Prdivý závod ▸ Build Android APK for old phones**, or with the
editor closed:

```sh
~/Unity/Hub/Editor/6000.4.11f1/Editor/Unity -batchmode -projectPath . \
  -buildTarget Android -executeMethod BuildGame.BuildAndroidOld -quit -logFile Logs/android-old.log
grep "\[Setup\]" Logs/android-old.log
```

The log should contain

```
[Setup] Android build: old phones, architectures ARMv7, ARM64, min API 25, starts in quality 'Low'
[Setup] build Succeeded: … -> …/Builds/Android/PrdivyZavod-old.apk
```

The game's code is compiled twice, once per processor type, so this build
takes a few minutes longer than the standard one. When it has finished, the
project's settings are back to those of the standard build.

## Check the APK

```sh
SDK=~/Unity/Hub/Editor/6000.4.11f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK
$SDK/build-tools/*/aapt2 dump badging Builds/Android/PrdivyZavod-old.apk | grep -E "minSdk|native-code"
# minSdkVersion:'25'
# native-code: 'arm64-v8a' 'armeabi-v7a'
```

## Install it on the old device

Follow [Install the game on an Android phone](install-on-an-android-phone.md)
with `PrdivyZavod-old.apk`. Two things differ on old Android versions:

- **Android 7:** side-loading is allowed in *Settings ▸ Security ▸ Unknown
  sources*, not per app.
- **Android 7 to 10:** there is no wireless debugging; use a USB cable, or copy
  the file and tap it.

## See what the build will look like

Before trying a device you can render the lightest graphics level on the
computer:

```sh
$UNITY -executeMethod ShotRunner.Run -shotDir Shots/lite -touchHud -liteQuality -logFile Logs/shots-lite.log
```

(`$UNITY` as in [Run the automated checks](run-the-automated-checks.md)).

## If it is still too slow

The lightest level is defined in `GraphicsSetup.cs` (the `Tier.Lite` branches
of `CreatePipeline`) and in `LightingRig.cs` (what `Lite` switches off). In
order of effect:

1. **Render scale** – `m_RenderScale`, 0.6. Try 0.5. The picture gets coarser,
   the frame rate rises almost in proportion to the pixels saved.
2. **Shadows** – set `m_MainLightShadowsSupported` to false for the tier. The
   game looks flatter but saves a whole pass over the scene.
3. **Frame rate cap** – `Application.targetFrameRate` in `GameBootstrap.Awake`
   is 60 on phones; 30 keeps a weak phone cooler and steadier.
4. **Fewer farts and lamps** – `pickupCount` in `GameBootstrap`; each fart is
   five small spheres, a beam and two particle systems.

After editing `GraphicsSetup.cs` run **Tools ▸ Prdivý závod ▸ Rebuild graphics
settings and materials**, then build again.

## Read the device's own report

With the device connected ([how](install-on-an-android-phone.md)):

```sh
adb logcat -v brief Unity:V AndroidRuntime:E '*:S'
```

`[Boot] world built in … ms` tells you how long starting a match took. A line
with `Exception` or a crash report from `AndroidRuntime` is what to look for
if the game closes. Graphics problems on old chips usually show as missing
lights or pink surfaces rather than as crashes; send a screenshot
(`adb exec-out screencap -p > phone.png`) along with the log when reporting one.

## Bluetooth on old devices

Before Android 12 the game needs no permission question for Bluetooth; pairing
the two devices in the system settings is enough
([Play with two players](play-with-two-players.md)).
