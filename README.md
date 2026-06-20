# Prdivý závod 💨 (Fart Race)

A small first-person Unity game. **Tři postavy se snaží sezbírat batůžek prdů a
pak je vyprdět v cíli** — three characters try to fill a backpack with farts and
then fart them out at the goal.

## Pravidla / Rules

- Hraješ z first-person pohledu proti dvěma botům: **Pšouk** (červený) a
  **Bzdina** (modrý).
- Po aréně jsou rozházené **zelené prdy**. Posbírej je do svého **batůžku**
  (kapacita 8).
- Plný (nebo částečně plný) batůžek dones do **oranžového cíle** a tam je
  **vyprdni**.
- Kdo první donese **15 prdů** do cíle, vyhrává.

## Ovládání / Controls

| Akce | Klávesa |
|------|---------|
| Pohyb | `W A S D` |
| Rozhlížení | myš |
| Skok | `Space` |
| Vyprdnout (v cíli) | drž levé tlačítko myši |
| Nová hra | `R` (po výhře) |

## Jak to spustit / How to run

1. Otevři **Unity Hub** → *Add project from disk* → vyber tuto složku.
2. Projekt cílí na **Unity 6 (6000.0 LTS)**. Pokud máš jinou verzi 6.x, Hub
   nabídne otevření v ní.
3. Otevři scénu `Assets/Scenes/Main.unity` a stiskni **Play**.

## Jak je to postavené / How it's built

Celý svět se generuje **proceduálně z primitiv** za běhu. Scéna obsahuje jediný
objekt `Bootstrap` se skriptem `GameBootstrap`, který vytvoří arénu, světlo,
cíl, hráče, boty i prdy. Díky tomu nejsou potřeba žádné ručně vytvořené assety.

| Skript | Role |
|--------|------|
| `GameBootstrap` | Postaví celý svět při startu |
| `GameManager`   | Skóre, výherce, HUD, restart |
| `Competitor`    | Sdílená logika batůžku (hráč i boti) |
| `PlayerController` | FPS ovládání (pohyb, myš, vyprdávání) |
| `AICompetitor`  | AI soupeři – sbírají a nosí do cíle |
| `FartPickup`    | Sběratelný prd (respawnuje se) |
| `GoalZone`      | Spouštěč cíle |
| `FartPuff`      | Vizuální obláček při vyprdnutí |
| `MaterialUtil`  | Materiály za běhu (Standard shader) |
