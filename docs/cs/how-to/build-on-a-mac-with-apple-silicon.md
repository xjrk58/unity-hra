# Jak sestavit hru na Macu s Apple silicon (M1 a novější)

Tenhle návod tě na Macu s čipem M1, M2, M3 nebo M4 dovede od nuly k běžící
`PrdivyZavod.app` a volitelně i k APK pro Android sestavenému na Macu.

> **Stav:** tyhle kroky vycházejí ze sestavovacího skriptu projektu a ze
> standardního postupu Unity pro macOS, ale autoři je na Macu nespustili. Pokud
> se některý krok chová jinak, směrodatné jsou pokyny pro Linux a log
> sestavení. Viz [Stav ověření](../reference/verification-status.md).

## Co potřebuješ

- macOS 12 nebo novější na Apple silicon, asi 20 GB volného místa na disku.
- Účet Unity (stačí bezplatná licence Personal).
- Git. Pokud `git --version` nabídne instalaci nástrojů příkazové řádky, nech
  je nainstalovat.

Rosetta potřeba **není**: Unity Hub i editor Unity 6 existují jako nativní
programy pro Apple silicon.

## 1. Nainstaluj Unity Hub a editor

1. Stáhni **Unity Hub** pro macOS z <https://unity.com/download>, otevři
   `.dmg` a přetáhni ho do *Applications*. Spusť ho a přihlas se.
2. **Installs ▸ Install Editor**. Najdi **6000.4.11f1** (pod *Archive*, pokud
   v seznamu není) a tam, kde Hub nabízí výběr, zvol editor
   **Apple silicon**, ne *Intel*.
3. Na stránce s moduly zaškrtni:
   - **Mac Build Support (IL2CPP)** – volitelné; výchozí backend Mono, který
     sestavovací skript používá, už je součástí editoru;
   - **Android Build Support** spolu s **OpenJDK** a
     **Android SDK & NDK Tools** – jen pokud chceš sestavovat i APK.
4. Nainstaluj. Editor skončí v
   `/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app`.

Jak ověřit, že máš nativní editor: v aplikaci *Finder* vyber `Unity.app`,
*File ▸ Get Info* – u *Kind* má být **Application (Apple silicon)**.

## 2. Stáhni si projekt a otevři ho

```sh
git clone git@github.com:xjrk58/unity-hra.git
cd unity-hra
```

V Unity Hubu: **Projects ▸ Add ▸ Add project from disk**, vyber složku a
otevři ji. První import textur a modelů trvá několik minut. macOS se možná
zeptá, jestli Unity smí ke složce přistupovat – povol to.

Otevři `Assets/Scenes/Main.unity` a jednou stiskni **Play**, ať máš jistotu,
že hra běží (klikni na **Hrát sám**).

## 3. Zvol architekturu procesoru

**File ▸ Build Profiles ▸ macOS ▸ Architecture** určuje, na čem aplikace
poběží:

| Nastavení | Běží na | Použij, když |
|-----------|---------|--------------|
| **Apple silicon** | jen Macy řady M | je sestavení pro tvůj vlastní Mac (nejmenší, nejrychlejší) |
| **Intel 64-bit + Apple silicon** | každý Mac | chceš aplikaci dát ostatním |
| Intel 64-bit | Macy s Intelem; na řadě M jen přes Rosettu | k tomu máš důvod |

Pokud macOS v tom okně ještě není aktivní platformou, nemusíš na něj
přepínat; udělá to sestavovací skript.

## 4. Sestav

**Tools ▸ Prdivý závod ▸ Build player.** Na Macu skript sestaví hru pro macOS
a zapíše `Builds/PrdivyZavod/PrdivyZavod.app`.

Totéž z Terminálu, se zavřeným editorem:

```sh
/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -buildTarget StandaloneOSX \
  -executeMethod BuildGame.Build -quit -logFile Logs/build.log
tail -5 Logs/build.log        # look for "[Setup] build Succeeded"
```

Během toho musí běžet Unity Hub s přihlášeným účtem, jinak bude v logu
`No valid Unity Editor license found`.

## 5. Spusť ji

```sh
open Builds/PrdivyZavod/PrdivyZavod.app
```

Aplikace sestavená na tvém vlastním Macu se spustí bez řečí. Když poprvé
založíš hru pro dva nebo se k ní připojíš, macOS se zeptá na dvě věci; na obě
odpověz **Allow**:

- *"…would like to find and connect to devices on your local network"*
  (macOS 15 a novější);
- *"Do you want the application … to accept incoming network connections?"*
  (firewall, jen při hostování).

Jak zjistit, pro které procesory byla aplikace sestavena:

```sh
lipo -archs Builds/PrdivyZavod/PrdivyZavod.app/Contents/MacOS/*
# arm64            -> Apple silicon only
# x86_64 arm64     -> universal
```

## 6. Dej aplikaci někomu dalšímu

Kopii, která putovala přes internet nebo chat, dá macOS do karantény, a
protože aplikace není podepsaná vývojářským certifikátem Apple, odmítne ji
s hlášením *"is damaged"* nebo *"cannot be opened"*. Na Macu příjemce:

```sh
xattr -dr com.apple.quarantine /path/to/PrdivyZavod.app
```

nebo na aplikaci klikni pravým tlačítkem, zvol **Open** a potvrď (v macOS 15:
*System Settings ▸ Privacy & Security ▸ Open Anyway*). Před odesláním aplikaci
zabal do zipu
(`ditto -c -k --keepParent PrdivyZavod.app PrdivyZavod.zip`), aby se zachovala
její struktura.

Řádná distribuce vyžaduje účet Apple Developer, podepisování a notarizaci; to
je mimo rozsah tohohle návodu.

## 7. Sestav APK pro Android na Macu (volitelné)

S nainstalovanými moduly pro Android z kroku 1:

**Tools ▸ Prdivý závod ▸ Build Android APK**, nebo

```sh
/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -projectPath . -buildTarget Android \
  -executeMethod BuildGame.BuildAndroid -quit -logFile Logs/android.log
```

APK je `Builds/Android/PrdivyZavod.apk`. Nástroj `adb` pro jeho instalaci na
telefon je uvnitř editoru:

```sh
ADB=/Applications/Unity/Hub/Editor/6000.4.11f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb
$ADB install -r Builds/Android/PrdivyZavod.apk
```

Pokračuj návodem [Instalace hry na telefon s Androidem](install-on-an-android-phone.md);
variantu pro stará zařízení popisuje [Sestavení pro staré telefony s Androidem](build-for-old-android-phones.md).

## 8. Vývojářské nástroje na Macu (volitelné)

Kontroly bez okna fungují stejně jako na Linuxu, jen s cestou k editoru na
Macu a s `-buildTarget StandaloneOSX`:

```sh
UNITY="/Applications/Unity/Hub/Editor/6000.4.11f1/Unity.app/Contents/MacOS/Unity -batchmode -projectPath . -buildTarget StandaloneOSX"
$UNITY -executeMethod MonsterTestRunner.Run -logFile Logs/monster.log
$UNITY -executeMethod AnimalTestRunner.Run -logFile Logs/animals.log
```

Nástroje pro assety potřebují Python 3 s knihovnami Pillow, NumPy a SciPy
(`python3 -m pip install pillow numpy scipy`) a Blender
(`brew install --cask blender`; jeho příkaz je
`/Applications/Blender.app/Contents/MacOS/Blender`). `Tools/dev/nettest.sh`
počítá s názvem souboru sestavení pro Linux; na Macu spusť obě kopie ručně,
jak popisuje návod [Spuštění automatických kontrol](run-the-automated-checks.md).

## Řešení potíží

| Příznak | Příčina a řešení |
|---------|------------------|
| Hub nabízí jen editor pro Intel | Aktualizuj Unity Hub; staré verze Hubu editory pro Apple silicon neznaly. |
| `No valid Unity Editor license found` | Spusť Unity Hub, přihlas se a příkaz spusť znovu. |
| Sestavení selže s *Mac Build Support not installed* | Sestavení pro macOS na Macu s backendem Mono žádný modul nepotřebuje; pokud byl projekt přepnut na IL2CPP, přidej v Hubu **Mac Build Support (IL2CPP)**. |
| Aplikace se na jiném Macu otevře a hned zavře | Byla sestavena jako *Apple silicon* a ten druhý Mac má Intel: sestav *Intel 64-bit + Apple silicon*. |
| *"PrdivyZavod is damaged and can't be opened"* | Karanténa, viz krok 6. |
| Druhý hráč nemůže hru najít nebo se k ní připojit | Oprávnění pro místní síť nebo firewall, viz krok 5 a [Hra ve dvou](play-with-two-players.md). |
| Růžové (purpurové) povrchy v editoru | Import byl přerušen: **Tools ▸ Prdivý závod ▸ Rebuild graphics settings and materials**. |
