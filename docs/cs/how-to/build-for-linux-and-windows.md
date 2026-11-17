# Jak sestavit hru pro Linux a Windows

Sestavovací skript sestaví hru pro ten systém počítače, na kterém editor
zrovna běží.

**Než začneš:** Unity 6000.4.11f1 nainstalované přes Unity Hub a projekt
jednou otevřený, aby se naimportovaly jeho assety
([tutoriál](../tutorials/02-build-and-run-from-source.md)).

## Z editoru

**Tools ▸ Prdivý závod ▸ Build player.** Výsledek je v `Builds/PrdivyZavod/`:

| Systém | Spusť tohle |
|--------|-------------|
| Linux | `Builds/PrdivyZavod/PrdivyZavod.x86_64` |
| Windows | `Builds\PrdivyZavod\PrdivyZavod.exe` |

Celou složku nech pohromadě; složka `PrdivyZavod_Data` vedle programu je
součástí hry.

## Bez otevření editoru

Nejdřív editor zavři (dva editory nemůžou mít projekt otevřený zároveň), potom:

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

Log končí řádkem `[Setup] build Succeeded: …`; návratový kód je 0 při úspěchu
a 3, pokud sestavení selhalo. Přidáním `-buildDir SOMEWHERE` uložíš výsledek
jinam.

## Zkontroluj sestavení

```sh
Builds/PrdivyZavod/PrdivyZavod.x86_64 -batchmode -captureShot /tmp/shot.png
```

spustí bez okna několik sekund sólového zápasu, uloží jeden snímek obrazovky a
skončí. Víc najdeš v návodu [Spuštění automatických kontrol](run-the-automated-checks.md).

## Řešení potíží

- **`No valid Unity Editor license found`** v logu: když editor běží bez okna,
  musí být spuštěný Unity Hub s přihlášeným účtem.
- **Okno hry je obrovské nebo pomalé:** hra se spouští na celou obrazovku
  v rozlišení plochy. Sniž MSAA nebo rozlišení stínů
  v `Assets/Settings/GamePipeline.asset`, viz [Grafické úrovně](../reference/graphics-tiers.md).
- **Druhý hráč se nemůže připojit ke hře hostované na tomhle počítači:**
  firewall musí povolit příchozí TCP port 47800 (a UDP 47801, aby se hra
  objevila v seznamu). Viz [Hra ve dvou](play-with-two-players.md).

Na sestavení pro Linux se tenhle projekt vyvíjí a testuje. Sestavení pro
Windows používá stejný skript, ale autoři ho nespustili.
