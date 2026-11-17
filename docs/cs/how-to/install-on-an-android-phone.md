# Jak nainstalovat hru na telefon s Androidem

Tři způsoby, od nejmenší přípravy po největší. Všechny instalují mimo obchod
APK podepsané ladicím klíčem ([jak ho sestavit](build-the-android-apk.md)).

`adb` níže je ten, který je součástí modulu Unity pro Android:

```sh
ADB=~/Unity/Hub/Editor/6000.4.11f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb   # Linux
```

## A. Zkopíruj soubor a klepni na něj

1. Dostaň `PrdivyZavod.apk` do telefonu, jak chceš: USB kabelem v režimu
   *File transfer* (Přenos souborů), na paměťové kartě, zprávou v chatu sám
   sobě.
2. V telefonu otevři správce souborů, soubor najdi a klepni na něj.
3. Napoprvé Android požádá o povolení, aby tahle aplikace směla instalovat
   neznámé aplikace: povol to, vrať se zpět a klepni na **Install**.
   Na Androidu 7 se místo toho přepíná *Settings ▸ Security ▸ Unknown sources*
   (Neznámé zdroje).
4. Pokud Play Protect varuje před neznámou aplikací, zvol **Install anyway**.

Hra se objeví jako **Prdivý závod**.

## B. Přes USB pomocí adb

1. V telefonu: *Settings ▸ About phone ▸ Software information*, sedmkrát
   klepni na **Build number** (Číslo sestavení). Potom
   *Settings ▸ Developer options ▸ USB debugging* (Pro vývojáře ▸ Ladění USB).
2. Připoj telefon kabelem, který přenáší data, a na telefonu potvrď
   **Allow USB debugging?**
3. Na počítači:

```sh
$ADB devices                                  # the phone must be listed as "device"
$ADB install -r Builds/Android/PrdivyZavod.apk
```

Pokud `adb devices` nic nevypíše, kabel nebo port jen nabíjí, nebo je USB
v telefonu v režimu *Charging only*: stáhni oznámení, klepni na položku USB a
zvol *File transfer*.

## C. Přes Wi-Fi pomocí adb (bez kabelu)

Funguje od Androidu 11. Telefon a počítač musí být na stejné Wi-Fi.

1. V telefonu: *Developer options ▸ Wireless debugging* (Pro vývojáře ▸
   Bezdrátové ladění) – zapni ho, otevři a klepni na
   **Pair device with pairing code**. To okno nech otevřené.
2. Na počítači, s adresou a šestimístným kódem z toho okna:

```sh
$ADB pair 192.168.1.23:37199 123456
$ADB devices                                  # usually connects by itself now
$ADB install -r Builds/Android/PrdivyZavod.apk
```

Pokud se telefon po spárování neobjeví, připoj se na adresu uvedenou na hlavní
obrazovce *Wireless debugging* (její port se liší od párovacího):
`$ADB connect 192.168.1.23:41311`, nebo nech adb, ať ho najde:
`$ADB mdns services`.

Spárování se pamatuje. Bezdrátové ladění se samo vypne, když telefon opustí
Wi-Fi; zapni ho znovu a počítač se zase připojí. Až skončíš, vypni ho.

## Spusť hru a sleduj, co dělá

```sh
$ADB shell am force-stop com.prdivyzavod.hra
$ADB shell am start -n com.prdivyzavod.hra/com.unity3d.player.UnityPlayerGameActivity
$ADB logcat -v brief Unity:V AndroidRuntime:E '*:S'
```

Užitečné řádky: `[Boot] world built in … ms` při startu zápasu, `[Net] …`
u spojení pro hru ve dvou, `[Bt] …` u Bluetooth. Snímek obrazovky telefonu:

```sh
$ADB exec-out screencap -p > phone.png
```

Než aplikaci spustíš z počítače, zastav ji, a spusť ji jen jednou. Když se
spustila ve chvíli, kdy ji po aktualizaci znovu spouštěl sám Android, zůstala
zamrzlá na černé obrazovce.

## Odinstaluj ji

```sh
$ADB uninstall com.prdivyzavod.hra
```

nebo na telefonu podrž prst na ikoně a odinstaluj ji.
