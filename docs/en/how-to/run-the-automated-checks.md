# How to run the automated checks

The project can be compiled, photographed and play-tested without opening the
editor's window and without anybody playing. Use this after changing code or
assets.

## Before you start

- Close the Unity editor: only one editor can have the project open, and only
  one of these commands can run at a time.
- Unity Hub must be running and signed in (the licence check needs it).
- Define a shortcut; the rest of this page uses it:

```sh
# Linux (on a Mac or Windows use that editor's path and -buildTarget StandaloneOSX / StandaloneWindows64)
UNITY="$HOME/Unity/Hub/Editor/6000.4.11f1/Editor/Unity -batchmode -projectPath . -buildTarget StandaloneLinux64"
```

Give every run its own `-logFile` in `Logs/` (the folder is ignored by git) and
after every run check the log for compiler errors:

```sh
grep -c "error CS" Logs/that-run.log      # must print 0
```

## The checks

| What | Command | Passed when the log has |
|------|---------|-------------------------|
| A bot match: bots collect, use ramps, deliver, never get stuck; then a restart | `$UNITY -executeMethod PlaytestRunner.Run -playtestSeconds 240 -logFile Logs/playtest.log` | `[Playtest] PASS` |
| The same with farts only on the upper decks | add `-playtestMinFloor 2` | `[Playtest] PASS` |
| Touch controls, driven by scripted fingers | `$UNITY -executeMethod TouchTestRunner.Run -touchHud -logFile Logs/touch.log` | `[TouchTest] PASS (16 checks)` |
| The monster: when it comes, hunting, catching, being driven off, restart | `$UNITY -executeMethod MonsterTestRunner.Run -logFile Logs/monster.log` | `[MonsterTest] PASS (22 checks)` |

Do **not** add `-quit` to these: they enter Play mode and exit by themselves.
Each takes one to two minutes; the exit code is 0 only on a pass.

## Screenshots

```sh
$UNITY -executeMethod ShotRunner.Run -shotDir Shots/desktop -logFile Logs/shots.log
$UNITY -executeMethod ShotRunner.Run -shotDir Shots/mobile  -touchHud -mobileQuality -logFile Logs/shots-mobile.log
$UNITY -executeMethod ShotRunner.Run -shotDir Shots/lite    -touchHud -liteQuality   -logFile Logs/shots-lite.log
```

Each writes about two dozen PNG files: the player's view, the HUD, the arena
from several angles, the farts, the monster far away, close and in your face
(`20_`–`25_`), and the final ranking (`13_ranking`). The second and third
commands show what a phone and an old phone render
([Graphics tiers](../reference/graphics-tiers.md)). The log also carries a
rough frame time: `ShotRunner benchmark: … ms per 1080p frame`.

Look at the pictures. A screenshot that exists is not a screenshot that is right.

## Two players on one computer

Needs the desktop build (Linux) first.

```sh
Tools/dev/nettest.sh 80 62 -netWinAt 36 -monsterAt 14
grep -E "\[Net\]|\[NetLog\] (match|host|joiner)" Logs/net-host.log Logs/net-client.log
```

starts a host and a self-playing joiner, both without a window, for 80 and 62
seconds. In the logs you should find the joiner connecting, both building
match 1, scores that agree on both sides at the same match time, blasts in both
directions, the joiner asking for a new match, and – because the joiner is
stopped first – `host: the joiner left`.

```sh
Tools/dev/nettest.sh 24 44
grep "the host is gone" Logs/net-client.log
```

stops the host first; the joiner must go back to the menu.

On a Mac or Windows start the two copies by hand with the same flags
(`-batchmode -nographics -host -netBot` and
`-batchmode -nographics -join 127.0.0.1 -netBot`, each with its own
`-logFile`).

## The built game

```sh
Builds/PrdivyZavod/PrdivyZavod.x86_64 -batchmode -captureShot /tmp/solo.png
Builds/PrdivyZavod/PrdivyZavod.x86_64 -batchmode -menu -captureMenu -captureShot /tmp/menu.png
```

The first proves that the build starts a match and renders; its log line
`DevCapture wrote …` also says how many fart recordings were loaded (63).

## What these checks cannot tell you

They do not hear sound, feel the controls or run on a phone. Anything about
real devices has to be tried on one; see
[Verification status](../reference/verification-status.md).

All flags are listed in
[Command line, editor menu and dev tools](../reference/command-line-and-editor-tools.md).
