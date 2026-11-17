# Reference: verification status

What has actually been run, and where. Read this before relying on a build for
a platform listed under "not run".

Development and testing happened on one Linux laptop (integrated AMD Radeon
780M graphics). Last updated when the horses and unicorns were added.

## Verified on the development computer

| Area | How | Result |
|------|-----|--------|
| Solo match logic | `PlaytestRunner`, three runs: farts everywhere, only on the two upper decks, only on the top deck – bots collect on all decks, use ramps, deliver, never stall; the animals fart; restart | passes |
| Touch controls | `TouchTestRunner` – 16 checks with scripted fingers | passes |
| Monster | `MonsterTestRunner` – 22 checks | passes |
| Horses and unicorns | `AnimalTestRunner` – 26 checks: two minutes of wandering without entering a hole, a column or a ramp end; where farts land; a horse's fart worth one and a rainbow fart three; shying away; a new herd after a restart | passes |
| Rendering, three graphics tiers | `ShotRunner` screenshots, looked at – including the animals, the rainbow and the gas effects on each tier | as intended |
| Menu, HUD with four competitors, final ranking | screenshots from the built game | as intended |
| Linux build | starts, renders, loads all 63 recordings | works |
| Two players over TCP | host and self-playing joiner as two processes on one computer: collecting, scoring, blasts both ways, the monster on the joiner (including being caught and blasting it), end of match, new match asked for by either side, either side disappearing; the animals, how often they farted and where their farts hang agree on both sides | works |
| Android APKs (standard and old-phone) | build; package, permissions, processor types and the Bluetooth class inspected; the horse model and the animals' code are in the package | as intended |

## Run on a real device

| What | Device | Result |
|------|--------|--------|
| The standard Android build: menu, solo matches, brighter farts, the monster | one current high-end phone, Android 17 | installed and played; a match is built in 0.02–0.14 s; the monster appeared and caught the player (the scare ran); no errors in its log |
| Two players over Wi-Fi | that phone hosting, the Linux computer joining by address with a self-playing player | four matches in a row: both devices built each match, scores and blasts in step, a new match asked for by the joiner; no errors on either side |

Both were done with the build from before the horses and unicorns.

## Not run

| Area | State |
|------|-------|
| Horses and unicorns on a phone, and in a match between two real devices | built into both APKs; not played on a device yet |
| The gas effects on a phone | the fix for the phone tiers (below) was only seen in pictures rendered on the computer; nobody has looked at a phone's screen since |
| The old-phone APK on an old or 32-bit device | never run |
| Bluetooth play | compiles and is packaged; never run – it needs two paired Android devices |
| Two phones playing each other; a phone *joining* a game (only hosting was tried) | never run |
| Finding a game in the list (UDP announcements) between devices | never observed; the development computer's firewall blocks incoming traffic, so it joined by address |
| The GitHub Actions workflow | written, never run: it needs a Unity licence in the repository's secrets |
| macOS build | never built or run; the guide follows the build script and Unity's usual workflow |
| Windows build | never built or run |
| Frame rate on any phone | not measured |
| The system Back button on Android | not tried |
| The game being sent to the background during a two-player match | not tried |

## Checked only by numbers

The monster's sounds are synthesised. Their loudness and frequency content
were measured (enough energy above 150 Hz to come through a phone speaker), but
nobody has judged them by ear. The fart recordings were listened to.

The animals' sounds – a recording played slowly for a horse, one played fast
with synthesised bells on top for a unicorn – have not been heard by anybody.

## Known weak spots

- Starting the app from a computer (`adb … am start`) at the same moment as
  somebody taps its icon launches it twice, and it stays on a black screen
  until it is force-stopped. This happened twice during testing and looked like
  a frozen build. Start it one way, once.

- Until the horses and unicorns were added, the two phone tiers drew none of
  the soft-particle gas when rendered on the computer – a fart there had no
  swirl and no beam of light above it. If phones behaved the same way, farts
  were harder to spot in every Android build made before that. The renderers
  were changed ([Graphics tiers](graphics-tiers.md)) and the rendered pictures
  now show the gas; it has not been confirmed on a phone.
- Bots alone finish a match in about 45–100 seconds, so the monster (45 s) can
  arrive when the match is nearly over.
- The escape over the old, low edge wall was never reproduced; the wall was
  raised anyway.
- The APKs are signed with the debug key: fine for side-loading, not for a
  store.
