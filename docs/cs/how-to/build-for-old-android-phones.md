# Jak sestavit hru pro staré telefony s Androidem

Standardní APK ([jak ho sestavit](build-the-android-apk.md)) potřebuje
64bitové zařízení a vykresluje hru v kvalitě určené pro telefony z posledních
několika let. Podle tohohle návodu sestavíš druhé APK, `PrdivyZavod-old.apk`,
pro zařízení, která jsou stará, pomalá nebo 32bitová, a dozvíš se, jak zjistit,
jestli na zařízení hra vůbec poběží.

> **Stav:** sestavení bylo vytvořeno a prozkoumáno a jeho grafická úroveň byla
> vykreslena na počítači. Na skutečném starém zařízení spuštěno **nebylo**.
> Viz [Stav ověření](../reference/verification-status.md).

## Co staré zařízení potřebuje

| Požadavek | Proč | Jak to ověřit (pomocí `adb`, viz níže) |
|-----------|------|----------------------------------------|
| Android **7.1** (API 25) nebo novější | nejnižší verze, pro kterou Unity 6 umí sestavovat | `adb shell getprop ro.build.version.sdk` → 25 nebo víc |
| Procesor ARM, 32bitový nebo 64bitový | APK obsahuje kód pro `armeabi-v7a` a `arm64-v8a`; zařízení s x86 pokrytá nejsou | `adb shell getprop ro.product.cpu.abilist` |
| **OpenGL ES 3.0** nebo Vulkan | Unity 6 nemá renderer pro OpenGL ES 2 | `adb shell dumpsys SurfaceFlinger \| grep -i "GLES"` → `OpenGL ES 3.x` |
| Asi 2 GB RAM, 300 MB volného úložiště | textury, zvuky a 100MB APK | *Settings ▸ About phone* |

Na zařízeních s Androidem 7.0 nebo starším nebo s grafickým čipem, který umí
jen OpenGL ES 2.0 (hodně telefonů zhruba z doby před rokem 2014), hra nepoběží
v žádném sestavení z tohohle projektu.

## Čím se sestavení pro staré telefony liší

| | Standardní APK | APK pro staré telefony |
|---|---|---|
| Soubor | `PrdivyZavod.apk` (asi 78 MB) | `PrdivyZavod-old.apk` (asi 100 MB) |
| Procesory | ARM64 | ARM64 **a 32bitový ARMv7** |
| Grafická úroveň po spuštění | *Medium* | *Low*, ta nejlehčí |
| Vykreslované rozlišení | 80 % | 60 % |
| Vyhlazování hran | 2× | vypnuté |
| Stíny slunce | 2048 px, 2 kaskády, měkké | 1024 px, 1 kaskáda, ostré, 28 m |
| Bloom | zapnutý | vypnutý |
| Rozsvícené stropní lampy | všechny | jen v patře, kde právě jsi |
| Minimální Android | 7.1 | 7.1 |

Všechno ostatní – pravidla, příšera, hra ve dvou, zvuky – je stejné a obě
sestavení můžou hrát proti sobě. Obě mají stejný název balíčku, takže instalace
jednoho nahradí druhé.

## Sestav ho

Potřebuješ modul pro Android, jak popisuje návod
[Sestavení APK pro Android](build-the-android-apk.md).

**Tools ▸ Prdivý závod ▸ Build Android APK for old phones**, nebo se zavřeným
editorem:

```sh
~/Unity/Hub/Editor/6000.4.11f1/Editor/Unity -batchmode -projectPath . \
  -buildTarget Android -executeMethod BuildGame.BuildAndroidOld -quit -logFile Logs/android-old.log
grep "\[Setup\]" Logs/android-old.log
```

V logu by mělo být

```
[Setup] Android build: old phones, architectures ARMv7, ARM64, min API 25, starts in quality 'Low'
[Setup] build Succeeded: … -> …/Builds/Android/PrdivyZavod-old.apk
```

Kód hry se překládá dvakrát, jednou pro každý typ procesoru, takže tohle
sestavení trvá o pár minut déle než standardní. Po dokončení se nastavení
projektu vrátí k hodnotám standardního sestavení.

## Zkontroluj APK

```sh
SDK=~/Unity/Hub/Editor/6000.4.11f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK
$SDK/build-tools/*/aapt2 dump badging Builds/Android/PrdivyZavod-old.apk | grep -E "minSdk|native-code"
# minSdkVersion:'25'
# native-code: 'arm64-v8a' 'armeabi-v7a'
```

## Nainstaluj ho na staré zařízení

Postupuj podle návodu [Instalace hry na telefon s Androidem](install-on-an-android-phone.md),
jen se souborem `PrdivyZavod-old.apk`. Na starých verzích Androidu se liší dvě
věci:

- **Android 7:** instalace mimo obchod se povoluje v *Settings ▸ Security ▸
  Unknown sources*, ne pro každou aplikaci zvlášť.
- **Android 7 až 10:** bezdrátové ladění tu není; použij USB kabel, nebo soubor
  zkopíruj a klepni na něj.

## Podívej se, jak bude sestavení vypadat

Než to zkusíš na zařízení, můžeš si nejlehčí grafickou úroveň vykreslit na
počítači:

```sh
$UNITY -executeMethod ShotRunner.Run -shotDir Shots/lite -touchHud -liteQuality -logFile Logs/shots-lite.log
```

(`$UNITY` jako v návodu [Spuštění automatických kontrol](run-the-automated-checks.md)).

## Když je to pořád moc pomalé

Nejlehčí úroveň je definovaná v `GraphicsSetup.cs` (větve `Tier.Lite`
v `CreatePipeline`) a v `LightingRig.cs` (co `Lite` vypíná). Seřazeno podle
účinku:

1. **Měřítko vykreslování** – `m_RenderScale`, 0.6. Zkus 0.5. Obraz zhrubne,
   snímková frekvence roste skoro úměrně ušetřeným pixelům.
2. **Stíny** – nastav pro tuhle úroveň `m_MainLightShadowsSupported` na false.
   Hra vypadá plošší, ale ušetří se celý průchod scénou.
3. **Strop snímkové frekvence** – `Application.targetFrameRate`
   v `GameBootstrap.Awake` je na telefonech 60; se 30 se slabý telefon míň
   zahřívá a běží rovnoměrněji.
4. **Míň prdů a lamp** – `pickupCount` v `GameBootstrap`; každý prd je pět
   malých koulí, paprsek a dva částicové systémy.

Po úpravě `GraphicsSetup.cs` spusť **Tools ▸ Prdivý závod ▸ Rebuild graphics
settings and materials** a pak sestav znovu.

## Přečti si, co hlásí samo zařízení

S připojeným zařízením ([jak na to](install-on-an-android-phone.md)):

```sh
adb logcat -v brief Unity:V AndroidRuntime:E '*:S'
```

`[Boot] world built in … ms` ti řekne, jak dlouho trval start zápasu. Když se
hra zavře, hledej řádek s `Exception` nebo hlášení o pádu od `AndroidRuntime`.
Grafické problémy na starých čipech se obvykle projeví chybějícími světly nebo
růžovými povrchy, ne pádem; když takový problém hlásíš, pošli spolu s logem
i snímek obrazovky (`adb exec-out screencap -p > phone.png`).

## Bluetooth na starých zařízeních

Před Androidem 12 se hra kvůli Bluetooth na žádné oprávnění neptá; stačí obě
zařízení spárovat v nastavení systému
([Hra ve dvou](play-with-two-players.md)).
