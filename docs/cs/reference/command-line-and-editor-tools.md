# Reference: příkazová řádka, nabídka editoru a vývojářské nástroje

## Nabídka editoru: Tools ▸ Prdivý závod

| Položka | Metoda | Výsledek |
|---------|--------|----------|
| Build player | `BuildGame.Build` | sestavení pro počítač, pro systém, na kterém editor běží, v `Builds/PrdivyZavod/` |
| Build Android APK | `BuildGame.BuildAndroid` | `Builds/Android/PrdivyZavod.apk` |
| Build Android APK for old phones | `BuildGame.BuildAndroidOld` | `Builds/Android/PrdivyZavod-old.apk` |
| Rebuild graphics settings and materials | `GraphicsSetup.Apply` | pipeline v `Assets/Settings`, materiály v `Assets/Resources/Materials` |
| Export synthesised fart sounds | `AudioDump.Run` | soubory `.wav` ve `FartSounds/` |

## Metody editoru pro `-executeMethod`

Spouštějí se jako `Unity -batchmode -projectPath . -buildTarget TARGET -executeMethod METHOD [options] -logFile FILE`.
Vždycky předej `-buildTarget` (`StandaloneLinux64`, `StandaloneWindows64`,
`StandaloneOSX`, nebo `Android` pro obě sestavení pro Android).

| Metoda | Volby | `-quit`? | Řádek v logu, který hledat | Návratový kód |
|--------|-------|----------|----------------------------|---------------|
| `BuildGame.Build` | `-buildDir DIR` | ano | `[Setup] build Succeeded` | 0, 3 = sestavení selhalo |
| `BuildGame.BuildAndroid` | `-buildDir DIR` | ano | `[Setup] build Succeeded` | 0, 3, 4 = chybí modul pro Android |
| `BuildGame.BuildAndroidOld` | `-buildDir DIR` | ano | `[Setup] build Succeeded` | 0, 3, 4 |
| `GraphicsSetup.Apply` | – | ano | `[Setup] done` | 0 |
| `AudioDump.Run` | `-audioDir DIR` | ano | – | 0 |
| `ShotRunner.Run` | `-shotDir DIR`, `-touchHud`, `-mobileQuality`, `-liteQuality` | **ne** | `ShotRunner wrote …`, `ShotRunner benchmark: …` | 0, 1 |
| `PlaytestRunner.Run` | `-playtestSeconds N` (240), `-playtestMinFloor F` (0) | **ne** | `[Playtest] PASS` / `FAIL` | 0, 2 |
| `TouchTestRunner.Run` | potřebuje `-touchHud` | **ne** | `[TouchTest] PASS` / `FAIL` | 0, 1, 2 = spuštěno bez `-touchHud` |
| `MonsterTestRunner.Run` | – | **ne** | `[MonsterTest] PASS` / `FAIL` | 0, 1 |

Čtyři runnery označené **ne** vstoupí do režimu Play a editor ukončí samy.

## Volby samotné hry

Fungují v sestavené hře a tam, kde to dává smysl, i v editoru.

| Volba | Účinek |
|-------|--------|
| `-batchmode` | (Unity) bez okna. Hra pak přeskočí menu a spustí sólový zápas. |
| `-nographics` | (Unity) úplně bez vykreslování; pro testy spojení |
| `-menu` | ukáže menu i s `-batchmode` |
| `-host` | rovnou hostuje zápas pro dva |
| `-join ADDRESS` | rovnou se připojí k hostiteli na té adrese; opakuje pokus každé 2 s, dokud tam hostitel není |
| `-touchHud` | dotykové ovládání a rozložení na počítači |
| `-monsterAt SECONDS` | první objevení příšery (výchozí 45) |
| `-captureShot FILE` | po několika sekundách vykreslí jeden obrázek, zapíše ho a skončí |
| `-captureDelay SECONDS` | jak dlouho `-captureShot` čeká (výchozí 4) |
| `-captureMenu` | nechá `-captureShot` vyfotit menu (s `-menu`, `-host` nebo `-menuJoin`) |
| `-menuJoin` | otevře menu na stránce pro připojení |
| `-netBot` | hráč hraje sám a do logu se každou sekundu zapíše řádek `[NetLog]` (testy spojení) |
| `-netBlastAt SECONDS` | s `-netBot`: kdy hostitel prdne po připojeném hráči (výchozí 10) |
| `-netWinAt SECONDS` | s `-netBot` na hostiteli: v tom čase ukončí první zápas |
| `-logFile FILE` | (Unity) kam se zapisuje log |

## Předpony v logu

| Předpona | Zapisuje | O čem |
|----------|----------|-------|
| `[Setup]` | `GraphicsSetup`, `BuildGame` | nastavení, materiály, sestavení |
| `[Boot]` | `GameBootstrap` | jak dlouho trvalo postavení světa |
| `[Net]` | `NetLink`, `NetGame` | spojení a úvodní domluva zápasu |
| `[Bt]` | `BluetoothLink` | problémy s Bluetooth |
| `[NetLog]` | `NetBot` | jeden stavový řádek za sekundu v testech spojení |
| `[Monster]` | `FartMonster` | hráč tohohle zařízení byl chycen |
| `[Playtest]`, `[TouchTest]`, `[MonsterTest]` | runnery | kontroly a verdikty |
| `ShotRunner`, `DevCapture` | nástroje pro snímky obrazovky | zapsané soubory, benchmark |

## Skripty mimo Unity

| Skript | Účel |
|--------|------|
| `Tools/dev/nettest.sh HOST_SECONDS JOINER_SECONDS [host options]` | test hry ve dvou na jednom počítači; logy v `Logs/net-host.log`, `Logs/net-client.log` |
| `Tools/blender/build_character.py`, `build_kit.py`, `build_monster.py` | sestaví modely (spouštěj přes `blender -b --factory-startup -P`) |
| `Tools/blender/verify_character.py` | kontroluje kosti modelu postavy |
| `Tools/assets/fetch.py`, `process_textures.py`, `process_sky.py`, `hdr.py`, `convert_props.py` | stáhnou a připraví textury, oblohu a rekvizity z Poly Haven |
| `Tools/assets/slice_farts.py` | rozřeže archivní nahrávku na klipy |
| `Tools/assets/make_icon.py` | nakreslí ikonu aplikace |
