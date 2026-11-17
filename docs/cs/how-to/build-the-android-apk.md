# Jak sestavit APK pro Android

Výsledkem je `Builds/Android/PrdivyZavod.apk`: 64bitové (ARM64) sestavení pro
současné telefony a tablety. Pro zařízení, která jsou stará, slabá nebo
32bitová, viz [Sestavení pro staré telefony s Androidem](build-for-old-android-phones.md).

## Než začneš

V Unity Hubu zvol **Installs ▸ (6000.4.11f1) ▸ Add modules** a přidej
**Android Build Support** s podmoduly **OpenJDK** a
**Android SDK & NDK Tools** (asi 4,5 GB ke stažení, 10 GB po instalaci).
Z terminálu se totéž udělá takhle:

```sh
unityhub -- --headless install-modules --version 6000.4.11f1 -m android --cm
```

Nic dalšího instalovat nemusíš: SDK, NDK i Java jsou součástí modulu a projekt
nepoužívá Android Studio.

## Sestavení

**Tools ▸ Prdivý závod ▸ Build Android APK**, nebo se zavřeným editorem:

```sh
~/Unity/Hub/Editor/6000.4.11f1/Editor/Unity -batchmode -projectPath . \
  -buildTarget Android -executeMethod BuildGame.BuildAndroid -quit -logFile Logs/android.log
```

První sestavení pro Android převede všechny assety a přeloží hru do nativního
kódu; počítej s deseti minutami nebo víc. Další sestavení trvají jednu až dvě
minuty.

Log končí řádkem `[Setup] build Succeeded: … -> …/PrdivyZavod.apk`.

## Co sestavení nastavuje

Sestavovací metoda tyhle hodnoty nastaví pokaždé, ať je v nastavení projektu
cokoli:

| Nastavení | Hodnota |
|-----------|---------|
| Název balíčku | `com.prdivyzavod.hra` |
| Název aplikace | Prdivý závod |
| Skriptovací backend, architektura | IL2CPP, ARM64 |
| Minimální Android | 7.1 (API 25) |
| Orientace | na šířku, oběma směry |
| Grafická úroveň po spuštění | *Medium* ([Grafické úrovně](../reference/graphics-tiers.md)) |
| Podpis | ladicí klíč |
| Formát | `.apk` (ne app bundle) |

Protože je APK podepsané ladicím klíčem, dá se nainstalovat ručně (instalace
mimo obchod), ale nedá se zveřejnit v obchodě.

## Zkontroluj výsledek

```sh
SDK=~/Unity/Hub/Editor/6000.4.11f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK
$SDK/build-tools/*/aapt2 dump badging Builds/Android/PrdivyZavod.apk | grep -E "^package|minSdk|native-code|uses-permission"
```

by mělo ukázat název balíčku, `minSdkVersion:'25'`, `native-code: 'arm64-v8a'`
a oprávnění `INTERNET`, `VIBRATE` a ta pro Bluetooth.

Potom ho [nainstaluj na telefon](install-on-an-android-phone.md).

## Řešení potíží

- **`Android Build Support is not installed`** (návratový kód 4): přidej modul
  podle postupu výše.
- **Po sestavení pro Android nástroje pro počítač všechno importují znovu:**
  vždycky předávej `-buildTarget` výslovně, jak to dělají příkazy v téhle
  dokumentaci; jinak zůstane editor přepnutý na Android.
- **`adb` po sestavení přestane telefon vidět:** sestavení restartuje server
  adb. Spusť znovu `adb devices`; pro bezdrátové připojení viz
  [Instalace hry na telefon s Androidem](install-on-an-android-phone.md).
