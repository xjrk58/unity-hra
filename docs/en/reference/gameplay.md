# Reference: gameplay rules and numbers

All values are the defaults in the source; where to change them is listed in
[Change the rules](../how-to/change-the-rules.md).

## The match

| | |
|---|---|
| Competitors, solo | the player (**Ty**, green) and two bots: **Pšouk** (red), **Bzdina** (blue) |
| Competitors, two players | host player (green), joined player (yellow), both bots |
| Goal of the match | be the first to expel **15** farts at the goal |
| Farts in the arena | 20, scattered over all decks; a taken one is back in the same place after 4 s |
| Backpack | holds 8 |
| Ranking | by delivered farts; ties by farts carried, then by roster order |
| First monster | 45 s after the match starts (lights fail from 38 s) |

## The arena

A car park of 4 square decks, 26 × 26 m, 5 m apart, open at the sides behind a
railing. One ramp, 5 m wide, joins each pair of decks; ramps alternate between
two opposite sides, and each upper deck has a hole over the whole ramp below.
The goal – an orange extraction hood – stands in a corner of the ground deck;
its zone is 5 × 5 m. An invisible wall one storey high runs round every deck.

## Competitors

| | Player | Bot |
|---|---|---|
| Speed | 6 m/s, sprint 8.6 m/s (forward only) | 4.7 m/s |
| Jump | about 0.9 m high | – |
| Expelling at the goal | one fart every 0.12 s | one every 0.25 s |
| Blast: price | 2 farts | 2 farts |
| Blast: range | 40 m, needs a clear line | 13 m, needs a clear line |
| Blast: the victim is out for | 10 s | 5 s |
| Blast: other conditions | victim under the crosshair | only when carrying at least 4 farts; at most one blast every 20–34 s |

A fart is picked up by touching it with room in the backpack. Somebody who is
knocked out disappears in a cloud, cannot move, collect or deliver, and cannot
be blasted or caught again until they are back.

Bots take the nearest fart (another deck counts as 26 m further), go to the
goal when the backpack is full – or holds at least 6 and the next fart is far
– and use the ramps. They do not avoid the monster.

## The monster

| | |
|---|---|
| Warning | 7 s of failing lights and **Něco se blíží…** before its first appearance |
| Where it appears | on its prey's deck, 10–14 m away, on solid floor, preferably behind its back |
| Prey | anybody not knocked out; a person is three times as likely to be chosen as a bot; not the one it just caught, if there is anybody else |
| Speed | starts at 4.9 m/s and gains 0.13 m/s every second of the hunt, up to 6.6 m/s |
| Obstacles | none: it passes through columns, railings and props, and follows up and down ramps |
| Catch | within 1.5 m |
| Effect of a catch | the whole backpack is lost; knocked out for 5 s (a player first gets 1.35 s of its face) |
| If the prey changes deck | after 1.5 s it sinks into the floor and comes up near the prey 2.2 s later |
| A hunt lasts at most | 28 s |
| Away after a catch / an unsuccessful hunt / being blasted | 12–17 s / 9–14 s / 14–18 s |
| Being blasted | costs the blaster 2 farts; works while it is rising or hunting, not while it feeds |
| End of the match | it dissolves and the daylight returns |

While it is out it is dark; between its visits a dusk remains.

## Messages on screen

| Text | Meaning |
|------|---------|
| **Nasbírej zelené prdy do batůžku.** | collect farts |
| **Dones prdy do oranžového cíle a vyprdni je.** | carry them to the goal |
| **Batůžek plný! Utíkej do oranžového cíle!** | the backpack is full |
| **CÍL 12 m (o patro níž)** | where the goal is, how far, how many decks down |
| **… – prdni po něm!** | a rival is under the crosshair and you can afford a blast |
| **Dostal jsi prdem!** | you were blasted |
| **Něco se blíží…** / **PRDOVÁ PŘÍŠERA!** / **PŘÍŠERA JE ZPĚT!** | the monster is coming / here / back |
| **PŘÍŠERA TĚ LOVÍ! 12 m** | it is after you, this far away |
| **Příšera tě dostala!** | it caught you |
| **Příšera zahnána!** | somebody blasted it away |
| **Vyhrál jsi!** / **Pšouk vyhrál!**, **Konečné pořadí** | end of the match and final ranking |
| **Čekám na druhého hráče…** | two players: waiting until both devices are ready |
