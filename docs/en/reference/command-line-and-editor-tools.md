# Reference: command line, editor menu and dev tools

## Editor menu: Tools ▸ Prdivý závod

| Item | Method | Result |
|------|--------|--------|
| Build player | `BuildGame.Build` | desktop build for the editor's own system in `Builds/PrdivyZavod/` |
| Build Android APK | `BuildGame.BuildAndroid` | `Builds/Android/PrdivyZavod.apk` |
| Build Android APK for old phones | `BuildGame.BuildAndroidOld` | `Builds/Android/PrdivyZavod-old.apk` |
| Rebuild graphics settings and materials | `GraphicsSetup.Apply` | pipelines in `Assets/Settings`, materials in `Assets/Resources/Materials` |
| Export synthesised fart sounds | `AudioDump.Run` | `.wav` files in `FartSounds/` |

## Editor methods for `-executeMethod`

Run as `Unity -batchmode -projectPath . -buildTarget TARGET -executeMethod METHOD [options] -logFile FILE`.
Always pass `-buildTarget` (`StandaloneLinux64`, `StandaloneWindows64`,
`StandaloneOSX`, or `Android` for the two Android builds).

| Method | Options | `-quit`? | Log line to look for | Exit code |
|--------|---------|----------|----------------------|-----------|
| `BuildGame.Build` | `-buildDir DIR` | yes | `[Setup] build Succeeded` | 0, 3 = build failed |
| `BuildGame.BuildAndroid` | `-buildDir DIR` | yes | `[Setup] build Succeeded` | 0, 3, 4 = Android module missing |
| `BuildGame.BuildAndroidOld` | `-buildDir DIR` | yes | `[Setup] build Succeeded` | 0, 3, 4 |
| `GraphicsSetup.Apply` | – | yes | `[Setup] done` | 0 |
| `AudioDump.Run` | `-audioDir DIR` | yes | – | 0 |
| `ShotRunner.Run` | `-shotDir DIR`, `-touchHud`, `-mobileQuality`, `-liteQuality` | **no** | `ShotRunner wrote …`, `ShotRunner benchmark: …` | 0, 1 |
| `PlaytestRunner.Run` | `-playtestSeconds N` (240), `-playtestMinFloor F` (0) | **no** | `[Playtest] PASS` / `FAIL` | 0, 2 |
| `TouchTestRunner.Run` | needs `-touchHud` | **no** | `[TouchTest] PASS` / `FAIL` | 0, 1, 2 = started without `-touchHud` |
| `MonsterTestRunner.Run` | – | **no** | `[MonsterTest] PASS` / `FAIL` | 0, 1 |
| `AnimalTestRunner.Run` | – | **no** | `[AnimalTest] PASS` / `FAIL` | 0, 1 |

The five runners marked **no** enter Play mode and quit the editor themselves.

## Options of the game itself

These work in a built game and, where it makes sense, in the editor.

| Option | Effect |
|--------|--------|
| `-batchmode` | (Unity) no window. The game then skips the menu and starts a solo match. |
| `-nographics` | (Unity) no rendering at all; for the link tests |
| `-menu` | show the menu even with `-batchmode` |
| `-host` | go straight to hosting a two-player match |
| `-join ADDRESS` | go straight to joining the host at that address; retries every 2 s until it is there |
| `-touchHud` | touch controls and layout on a computer |
| `-monsterAt SECONDS` | first appearance of the monster (default 45) |
| `-captureShot FILE` | render one picture after a few seconds, write it, quit |
| `-captureDelay SECONDS` | how long `-captureShot` waits (default 4) |
| `-captureMenu` | let `-captureShot` photograph the menu (with `-menu`, `-host` or `-menuJoin`) |
| `-menuJoin` | open the menu on the join page |
| `-netBot` | the player plays by itself and the log gets `[NetLog]` lines every second (link tests) |
| `-netBlastAt SECONDS` | with `-netBot`: when the host blasts the joiner (default 10) |
| `-netWinAt SECONDS` | with `-netBot` on the host: end the first match at that time |
| `-logFile FILE` | (Unity) where the log goes |

## Log prefixes

| Prefix | Written by | About |
|--------|-----------|-------|
| `[Setup]` | `GraphicsSetup`, `BuildGame` | settings, materials, builds |
| `[Boot]` | `GameBootstrap` | how long building the world took |
| `[Net]` | `NetLink`, `NetGame` | the connection and the match hand-shake |
| `[Bt]` | `BluetoothLink` | Bluetooth problems |
| `[NetLog]` | `NetBot` | two status lines per second in link tests: the match, and the animals with their farts |
| `[Monster]` | `FartMonster` | this device's player was caught |
| `[Playtest]`, `[TouchTest]`, `[MonsterTest]`, `[AnimalTest]` | the runners | checks and verdicts |
| `ShotRunner`, `DevCapture` | screenshot tools | files written, benchmark |

## Scripts outside Unity

| Script | Purpose |
|--------|---------|
| `Tools/dev/nettest.sh HOST_SECONDS JOINER_SECONDS [host options]` | two-player test on one computer; logs in `Logs/net-host.log`, `Logs/net-client.log` |
| `Tools/blender/build_character.py`, `build_kit.py`, `build_monster.py`, `build_horse.py` | build the models (run with `blender -b --factory-startup -P`) |
| `Tools/blender/verify_character.py` | checks the character model's bones |
| `Tools/assets/fetch.py`, `process_textures.py`, `process_sky.py`, `hdr.py`, `convert_props.py` | download and prepare the Poly Haven textures, sky and props |
| `Tools/assets/slice_farts.py` | cut the archive recording into clips |
| `Tools/assets/make_icon.py` | draw the app icon |
