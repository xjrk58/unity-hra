# How to build the Android APK

This produces `Builds/Android/PrdivyZavod.apk`: a 64-bit (ARM64) build for
current phones and tablets. For devices that are old, weak or 32-bit, see
[Build for old Android phones](build-for-old-android-phones.md).

## Before you start

In Unity Hub, **Installs ▸ (6000.4.11f1) ▸ Add modules** and add
**Android Build Support** with its sub-modules **OpenJDK** and
**Android SDK & NDK Tools** (about 4.5 GB to download, 10 GB installed). From
a terminal the same is:

```sh
unityhub -- --headless install-modules --version 6000.4.11f1 -m android --cm
```

Nothing else needs installing: the SDK, the NDK and Java come with the module
and the project does not use Android Studio.

## Build

**Tools ▸ Prdivý závod ▸ Build Android APK**, or with the editor closed:

```sh
~/Unity/Hub/Editor/6000.4.11f1/Editor/Unity -batchmode -projectPath . \
  -buildTarget Android -executeMethod BuildGame.BuildAndroid -quit -logFile Logs/android.log
```

The first Android build converts every asset and compiles the game to native
code; expect ten minutes or more. Later builds take one to two minutes.

The log ends with `[Setup] build Succeeded: … -> …/PrdivyZavod.apk`.

## What the build sets

The build method fixes these every time, whatever the project settings say:

| Setting | Value |
|---------|-------|
| Package name | `com.prdivyzavod.hra` |
| App name | Prdivý závod |
| Scripting backend, architecture | IL2CPP, ARM64 |
| Minimum Android | 7.1 (API 25) |
| Orientation | landscape, both ways |
| Graphics level on start | *Medium* ([Graphics tiers](../reference/graphics-tiers.md)) |
| Signing | debug key |
| Format | `.apk` (not an app bundle) |

Because it is signed with the debug key, the APK can be installed by hand
(side-loaded) but not published in a store.

## Check the result

```sh
SDK=~/Unity/Hub/Editor/6000.4.11f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK
$SDK/build-tools/*/aapt2 dump badging Builds/Android/PrdivyZavod.apk | grep -E "^package|minSdk|native-code|uses-permission"
```

should show the package name, `minSdkVersion:'25'`, `native-code: 'arm64-v8a'`
and the permissions `INTERNET`, `VIBRATE` and the Bluetooth ones.

Then [install it on a phone](install-on-an-android-phone.md).

## Troubleshooting

- **`Android Build Support is not installed`** (exit code 4): add the module as
  described above.
- **After an Android build the desktop tools reimport everything:** always pass
  `-buildTarget` explicitly, as the commands in this documentation do;
  otherwise the editor stays switched to Android.
- **`adb` stops seeing the phone after a build:** the build restarts the adb
  server. Run `adb devices` again; for a wireless connection see
  [Install the game on an Android phone](install-on-an-android-phone.md).
