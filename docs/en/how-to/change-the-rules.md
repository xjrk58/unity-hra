# How to change the rules

The numbers that define the game are plain fields and constants in the
scripts. Change one, then run the [automated checks](run-the-automated-checks.md)
– several of them assume the current values.

| To change | Edit | In |
|-----------|------|----|
| Farts needed to win (15) | `WinTarget` | `Assets/Scripts/GameManager.cs` |
| Backpack size (8) | `Capacity` | `Assets/Scripts/Competitor.cs` |
| Number of farts lying about (20) | `pickupCount` | `Assets/Scripts/GameBootstrap.cs` |
| Time until a taken fart is back (4 s) | `respawnDelay` | `Assets/Scripts/FartPickup.cs` |
| Player's walking / sprinting speed (6 / 8.6 m/s) | `moveSpeed`, `sprintSpeed` | `Assets/Scripts/PlayerController.cs` |
| Player's blast: price, knock-out time, range (2, 10 s, 40 m) | `blastCost`, `stunDuration`, `blastRange` | `Assets/Scripts/PlayerController.cs` |
| Bots: speed, blast (4.7 m/s; 2, 5 s, 13 m) | `moveSpeed`, `blastCost`, `blastStun`, `blastRange` | `Assets/Scripts/AICompetitor.cs` |
| When the monster first appears (45 s) | `FirstAppearance` | `Assets/Scripts/FartMonster.cs` |
| Monster: speed, catch distance, knock-out, how long it hunts | constants at the top of the class | `Assets/Scripts/FartMonster.cs` |
| No monster at all | set `FartMonster.Enabled = false` before the scene loads, or return early from `FartMonster.Build` | `Assets/Scripts/FartMonster.cs` |
| Size and shape of the arena | constants | `Assets/Scripts/Arena.cs` |
| Names and colours of the competitors | `GameBootstrap.Awake` | `Assets/Scripts/GameBootstrap.cs` |

Things to keep in mind:

- **The arena.** `Arena.cs` is shared by the builder, the collisions and the
  bots' navigation, so they stay in step; but look at the screenshots after
  changing it, and run the bot match with `-playtestMinFloor 2`.
- **The monster's speed** sits between the bots (4.7), the player's walk (6)
  and sprint (8.6) on purpose; see [The monster](../explanation/the-monster.md).
- **Match length.** Bots alone finish a match in roughly 45 to 80 seconds, so
  with the default target the monster often arrives near the end. A higher
  `WinTarget` gives it more of the match.
- **Two players.** Both devices must run the same version of the game; the
  rules are not negotiated between them.
- New on-screen texts are Czech, like the rest of the game.

To try a different monster time without editing code, start the built game
with `-monsterAt SECONDS`.
