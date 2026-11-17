# Reference: verification status

What has actually been run, and where. Read this before relying on a build for
a platform listed under "not run".

Development and testing happened on one Linux laptop (integrated AMD Radeon
780M graphics). Last updated with the state of the repository at its first
publication.

## Verified on the development computer

| Area | How | Result |
|------|-----|--------|
| Solo match logic | `PlaytestRunner` – bots collect on all decks, use ramps, deliver, never stall; restart | passes |
| Touch controls | `TouchTestRunner` – 16 checks with scripted fingers | passes |
| Monster | `MonsterTestRunner` – 22 checks | passes |
| Rendering, three graphics tiers | `ShotRunner` screenshots, looked at | as intended |
| Menu, HUD with four competitors, final ranking | screenshots from the built game | as intended |
| Linux build | starts, renders, loads all 63 recordings | works |
| Two players over TCP | host and self-playing joiner as two processes on one computer: collecting, scoring, blasts both ways, the monster on the joiner (including being caught and blasting it), end of match, new match asked for by either side, either side disappearing | works |
| Android APKs (standard and old-phone) | build; package, permissions, processor types and the Bluetooth class inspected | as intended |

## Run on a real device

| What | Device | Result |
|------|--------|--------|
| The standard Android build: menu, solo matches, brighter farts, the monster | one current high-end phone, Android 17 | installed and played; a match is built in 0.02–0.14 s; the monster appeared and caught the player (the scare ran); no errors in its log |
| Two players over Wi-Fi | that phone hosting, the Linux computer joining by address with a self-playing player | four matches in a row: both devices built each match, scores and blasts in step, a new match asked for by the joiner; no errors on either side |

## Not run

| Area | State |
|------|-------|
| The old-phone APK on an old or 32-bit device | never run |
| Bluetooth play | compiles and is packaged; never run – it needs two paired Android devices |
| Two phones playing each other; a phone *joining* a game (only hosting was tried) | never run |
| Finding a game in the list (UDP announcements) between devices | never observed; the development computer's firewall blocks incoming traffic, so it joined by address |
| macOS build | never built or run; the guide follows the build script and Unity's usual workflow |
| Windows build | never built or run |
| Frame rate on any phone | not measured |
| The system Back button on Android | not tried |
| The game being sent to the background during a two-player match | not tried |

## Checked only by numbers

The monster's sounds are synthesised. Their loudness and frequency content
were measured (enough energy above 150 Hz to come through a phone speaker), but
nobody has judged them by ear. The fart recordings were listened to.

## Known weak spots

- Starting the app from a computer (`adb … am start`) at the same moment as
  somebody taps its icon launches it twice, and it stays on a black screen
  until it is force-stopped. This happened twice during testing and looked like
  a frozen build. Start it one way, once.

- Bots alone finish a match in about 45–80 seconds, so the monster (45 s) can
  arrive when the match is nearly over.
- The escape over the old, low edge wall was never reproduced; the wall was
  raised anyway.
- The APKs are signed with the debug key: fine for side-loading, not for a
  store.
