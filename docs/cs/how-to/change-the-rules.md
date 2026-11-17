# Jak změnit pravidla

Čísla, která hru určují, jsou obyčejná pole a konstanty ve skriptech. Některé
změň a pak spusť [automatické kontroly](run-the-automated-checks.md)
– několik z nich počítá se současnými hodnotami.

| Co změnit | Uprav | Kde |
|-----------|-------|-----|
| Počet prdů potřebných k výhře (15) | `WinTarget` | `Assets/Scripts/GameManager.cs` |
| Velikost batůžku (8) | `Capacity` | `Assets/Scripts/Competitor.cs` |
| Počet povalujících se prdů (20) | `pickupCount` | `Assets/Scripts/GameBootstrap.cs` |
| Doba, než se sebraný prd vrátí (4 s) | `respawnDelay` | `Assets/Scripts/FartPickup.cs` |
| Rychlost chůze / sprintu hráče (6 / 8,6 m/s) | `moveSpeed`, `sprintSpeed` | `Assets/Scripts/PlayerController.cs` |
| Prdnutí hráče: cena, doba vyřazení, dosah (2, 10 s, 40 m) | `blastCost`, `stunDuration`, `blastRange` | `Assets/Scripts/PlayerController.cs` |
| Boti: rychlost, prdnutí (4,7 m/s; 2; 5 s; 13 m) | `moveSpeed`, `blastCost`, `blastStun`, `blastRange` | `Assets/Scripts/AICompetitor.cs` |
| Kdy se příšera poprvé objeví (45 s) | `FirstAppearance` | `Assets/Scripts/FartMonster.cs` |
| Příšera: rychlost, vzdálenost chycení, vyřazení, jak dlouho loví | konstanty na začátku třídy | `Assets/Scripts/FartMonster.cs` |
| Úplně bez příšery | nastav `FartMonster.Enabled = false` před načtením scény, nebo se hned na začátku vrať z `FartMonster.Build` | `Assets/Scripts/FartMonster.cs` |
| Velikost a tvar arény | konstanty | `Assets/Scripts/Arena.cs` |
| Jména a barvy závodníků | `GameBootstrap.Awake` | `Assets/Scripts/GameBootstrap.cs` |

Na co nezapomenout:

- **Aréna.** `Arena.cs` sdílí stavitel, kolize i navigace botů, takže zůstávají
  ve shodě; přesto se po změně podívej na snímky obrazovky a spusť zápas botů
  s `-playtestMinFloor 2`.
- **Rychlost příšery** leží záměrně mezi boty (4,7), chůzí hráče (6) a
  sprintem (8,6); viz [Příšera](../explanation/the-monster.md).
- **Délka zápasu.** Samotní boti dohrají zápas zhruba za 45 až 80 sekund,
  takže s výchozí hodnotou příšera často dorazí až ke konci. Vyšší `WinTarget`
  jí dá větší část zápasu.
- **Hra ve dvou.** Na obou zařízeních musí běžet stejná verze hry; pravidla si
  mezi sebou nedomlouvají.
- Nové texty na obrazovce jsou česky, jako zbytek hry.

Jiný čas příšery můžeš vyzkoušet bez úpravy kódu: spusť sestavenou hru
s `-monsterAt SECONDS`.
