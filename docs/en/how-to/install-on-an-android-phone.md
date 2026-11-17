# How to install the game on an Android phone

Three ways, from the least to the most setup. All of them install a
side-loaded, debug-signed APK ([how to build it](build-the-android-apk.md)).

`adb` below is the one that comes with Unity's Android module:

```sh
ADB=~/Unity/Hub/Editor/6000.4.11f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb   # Linux
```

## A. Copy the file and tap it

1. Get `PrdivyZavod.apk` onto the phone any way you like: a USB cable in
   *File transfer* mode, a memory card, a chat message to yourself.
2. On the phone open the file manager, find the file and tap it.
3. The first time, Android asks to allow this app to install unknown apps:
   allow it, go back, tap **Install**.
   On Android 7 the switch is *Settings ▸ Security ▸ Unknown sources* instead.
4. If Play Protect warns about an unknown app, choose **Install anyway**.

The game appears as **Prdivý závod**.

## B. Over USB with adb

1. On the phone: *Settings ▸ About phone ▸ Software information*, tap
   **Build number** seven times. Then *Settings ▸ Developer options ▸
   USB debugging*.
2. Plug the phone in with a cable that carries data and accept
   **Allow USB debugging?** on the phone.
3. On the computer:

```sh
$ADB devices                                  # the phone must be listed as "device"
$ADB install -r Builds/Android/PrdivyZavod.apk
```

If `adb devices` lists nothing, the cable or port only charges, or the phone's
USB mode is *Charging only*: pull down the notifications, tap the USB entry and
choose *File transfer*.

## C. Over Wi-Fi with adb (no cable)

Works from Android 11. Phone and computer must be on the same Wi-Fi.

1. On the phone: *Developer options ▸ Wireless debugging* – switch it on, open
   it and tap **Pair device with pairing code**. Keep that window open.
2. On the computer, with the address and the six-digit code from that window:

```sh
$ADB pair 192.168.1.23:37199 123456
$ADB devices                                  # usually connects by itself now
$ADB install -r Builds/Android/PrdivyZavod.apk
```

If the phone does not show up after pairing, connect to the address shown on
the main *Wireless debugging* screen (its port differs from the pairing one):
`$ADB connect 192.168.1.23:41311`, or let adb find it: `$ADB mdns services`.

Pairing is remembered. Wireless debugging switches itself off when the phone
leaves the Wi-Fi; switch it on again and the computer reconnects. Switch it off
when you are done.

## Start it and watch what it does

```sh
$ADB shell am force-stop com.prdivyzavod.hra
$ADB shell am start -n com.prdivyzavod.hra/com.unity3d.player.UnityPlayerGameActivity
$ADB logcat -v brief Unity:V AndroidRuntime:E '*:S'
```

Useful lines: `[Boot] world built in … ms` when a match starts, `[Net] …` for
two-player connections, `[Bt] …` for Bluetooth. A screenshot of the phone:

```sh
$ADB exec-out screencap -p > phone.png
```

Stop the app before starting it from the computer, and start it once. Starting
it while Android is itself relaunching the app after an update has left it
frozen on a black screen.

## Remove it

```sh
$ADB uninstall com.prdivyzavod.hra
```

or long-press the icon on the phone and uninstall.
