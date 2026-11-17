# Tutoriál: sestavení a spuštění ze zdrojových kódů

V tomhle tutoriálu otevřeš projekt v Unity, spustíš hru v editoru a vytvoříš
samostatnou hru. Počítej s půlhodinou; většinu z ní zabere čekání na stahování
a první import.

Potřebuješ 64bitový počítač s Linuxem, Windows nebo macOS, asi 15 GB volného
místa na disku a bezplatný účet Unity.

## 1. Stáhni si zdrojové kódy

```sh
git clone git@github.com:xjrk58/unity-hra.git
cd unity-hra
```

## 2. Nainstaluj Unity

1. Nainstaluj **Unity Hub** z <https://unity.com/download> a přihlas se.
2. V Hubu otevři **Installs ▸ Install Editor** a nainstaluj verzi
   **6000.4.11f1** (Unity 6). Pokud se nenabízí, použij záložku *Archive*.
   Pro tenhle tutoriál nejsou potřeba žádné další moduly.

Hra používá Universal Render Pipeline, která je součástí editoru; nic dalšího
se nestahuje.

## 3. Otevři projekt

V Hubu zvol **Projects ▸ Add ▸ Add project from disk**, vyber naklonovanou
složku a projekt otevři.

Při prvním otevření se importují všechny textury a modely. Trvá to několik
minut; další otevření už jen pár sekund.

## 4. Zahraj si v editoru

1. V okně *Project* otevři `Assets/Scenes/Main.unity`.
2. Stiskni nahoře tlačítko **Play**.

V pohledu *Game* se objeví úvodní obrazovka. Klikni na **Hrát sám** a hraj:
nejdřív klikni do pohledu Game, aby dostal klávesnici a myš. Klávesou `Esc`
uvolníš myš a dalším stiskem Play hru zastavíš.

Všimni si, že před stiskem Play byl v okně *Hierarchy* jediný objekt,
`Bootstrap`: celý svět postaví kód při startu scény.

## 5. Vytvoř sestavení

V nabídce editoru zvol **Tools ▸ Prdivý závod ▸ Build player**.

Po minutě nebo dvou konzole vypíše
`[Setup] build Succeeded: …` a hra je v `Builds/PrdivyZavod/`. Spusť ji
odtamtud.

## Co máš hotovo

Projekt ti běží ze zdrojových kódů a máš z něj samostatnou hru. Kam dál:

- další platformy: [Mac s Apple silicon](../how-to/build-on-a-mac-with-apple-silicon.md),
  [Android](../how-to/build-the-android-apk.md), [staré telefony s Androidem](../how-to/build-for-old-android-phones.md);
- jak je kód uspořádaný: [Uspořádání projektu a skripty](../reference/project-layout.md);
- proč je ve scéně jen jeden objekt: [Jak se staví svět](../explanation/how-the-world-is-built.md).
