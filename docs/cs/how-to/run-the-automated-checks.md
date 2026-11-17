# Jak spustit automatické kontroly

Projekt se dá přeložit, vyfotit a otestovat hrou, aniž by se otevřelo okno
editoru a aniž by někdo hrál. Použij to po změně kódu nebo assetů.

## Než začneš

- Zavři editor Unity: projekt může mít otevřený jen jeden editor a v jednu
  chvíli může běžet jen jeden z těchhle příkazů.
- Musí běžet Unity Hub s přihlášeným účtem (potřebuje ho kontrola licence).
- Nadefinuj si zkratku; zbytek stránky ji používá:

```sh
# Linux (on a Mac or Windows use that editor's path and -buildTarget StandaloneOSX / StandaloneWindows64)
UNITY="$HOME/Unity/Hub/Editor/6000.4.11f1/Editor/Unity -batchmode -projectPath . -buildTarget StandaloneLinux64"
```

Každému běhu dej vlastní `-logFile` v `Logs/` (tu složku git ignoruje) a po
každém běhu zkontroluj, jestli v logu nejsou chyby překladače:

```sh
grep -c "error CS" Logs/that-run.log      # must print 0
```

## Kontroly

| Co | Příkaz | Prošlo, když je v logu |
|----|--------|------------------------|
| Zápas botů: boti sbírají, používají rampy, odevzdávají, nikdy se nezaseknou; zvířata prdí; potom restart | `$UNITY -executeMethod PlaytestRunner.Run -playtestSeconds 240 -logFile Logs/playtest.log` | `[Playtest] PASS` |
| Totéž s prdy jen v horních patrech, nebo jen v nejvyšším | přidej `-playtestMinFloor 2`, nebo `3` | `[Playtest] PASS` |
| Dotykové ovládání řízené naskriptovanými prsty | `$UNITY -executeMethod TouchTestRunner.Run -touchHud -logFile Logs/touch.log` | `[TouchTest] PASS (16 checks)` |
| Příšera: kdy přijde, lov, chycení, zahnání, restart | `$UNITY -executeMethod MonsterTestRunner.Run -logFile Logs/monster.log` | `[MonsterTest] PASS (22 checks)` |
| Koně a jednorožci: kudy chodí, kam dopadají jejich prdy a jakou mají hodnotu, uhýbání, restart | `$UNITY -executeMethod AnimalTestRunner.Run -logFile Logs/animals.log` | `[AnimalTest] PASS (26 checks)` |

K těmhle příkazům **nepřidávej** `-quit`: samy vstoupí do režimu Play a samy
skončí. Každý trvá jednu až dvě minuty; návratový kód je 0 jen při úspěchu.

## Snímky obrazovky

```sh
$UNITY -executeMethod ShotRunner.Run -shotDir Shots/desktop -logFile Logs/shots.log
$UNITY -executeMethod ShotRunner.Run -shotDir Shots/mobile  -touchHud -mobileQuality -logFile Logs/shots-mobile.log
$UNITY -executeMethod ShotRunner.Run -shotDir Shots/lite    -touchHud -liteQuality   -logFile Logs/shots-lite.log
```

Každý zapíše skoro třicet souborů PNG: pohled hráče, HUD, arénu z několika
úhlů, prdy, příšeru zdálky, zblízka a přímo před obličejem
(`20_`–`25_`), koně a jednorožce uprostřed duhového prdu (`30_`–`33_`)
a konečné pořadí (`13_ranking`). Druhý a třetí příkaz ukazují,
co vykresluje telefon a starý telefon
([Grafické úrovně](../reference/graphics-tiers.md)). V logu je i hrubý čas
snímku: `ShotRunner benchmark: … ms per 1080p frame`.

Prohlédni si obrázky. To, že snímek obrazovky existuje, ještě neznamená, že je
správně.

## Dva hráči na jednom počítači

Nejdřív potřebuješ sestavení pro počítač (Linux).

```sh
Tools/dev/nettest.sh 80 62 -netWinAt 36 -monsterAt 14
grep -E "\[Net\]|\[NetLog\] (match|host|joiner)" Logs/net-host.log Logs/net-client.log
```

spustí hostitele a připojeného hráče, který hraje sám, oba bez okna, na 80 a
62 sekund. V logech má být vidět, jak se připojený hráč připojuje, jak oba
staví zápas 1, skóre, které se ve stejném čase zápasu na obou stranách
shoduje, prdnutí oběma směry, žádost připojeného hráče o nový zápas a
– protože se připojený hráč zastaví první – `host: the joiner left`.

Každou sekundu navíc obě strany zalogují zvířata – kolikrát které prdlo a kde
visí jeho prd (`grep "animals=" Logs/net-host.log Logs/net-client.log`). Co
řádek připojeného hráče říká o každém zvířeti, se musí shodovat s tím, co říká
řádek hostitele těsně před ním nebo těsně po něm. Jediná výjimka: prd, který
zvíře pustilo a někdo sebral mezi dvěma řádky hostitele, může být jako `there`
vidět jen na straně připojeného hráče.

```sh
Tools/dev/nettest.sh 24 44
grep "the host is gone" Logs/net-client.log
```

zastaví nejdřív hostitele; připojený hráč se musí vrátit do menu.

Na Macu nebo ve Windows spusť obě kopie ručně se stejnými přepínači
(`-batchmode -nographics -host -netBot` a
`-batchmode -nographics -join 127.0.0.1 -netBot`, každou s vlastním
`-logFile`).

## Sestavená hra

```sh
Builds/PrdivyZavod/PrdivyZavod.x86_64 -batchmode -captureShot /tmp/solo.png
Builds/PrdivyZavod/PrdivyZavod.x86_64 -batchmode -menu -captureMenu -captureShot /tmp/menu.png
```

První příkaz dokazuje, že sestavení spustí zápas a vykresluje; jeho řádek
v logu `DevCapture wrote …` taky říká, kolik nahrávek prdů se načetlo (63).

## Co ti tyhle kontroly neřeknou

Neslyší zvuk, necítí ovládání a neběží na telefonu. Všechno, co se týká
skutečných zařízení, se musí vyzkoušet na skutečném zařízení; viz
[Stav ověření](../reference/verification-status.md).

Všechny přepínače jsou vypsané na stránce
[Příkazová řádka, nabídka editoru a vývojářské nástroje](../reference/command-line-and-editor-tools.md).
