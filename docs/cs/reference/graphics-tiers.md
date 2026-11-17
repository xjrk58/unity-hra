# Reference: grafické úrovně

Tři assety render pipeline v `Assets/Settings`, každý svázaný s jednou úrovní
kvality Unity. Všechny zapisuje `GraphicsSetup.Apply`; čísla upravuj tam, ne
v assetech.

| | Počítač | Telefon | Starý telefon |
|---|---|---|---|
| Asset pipeline | `GamePipeline` | `GameMobilePipeline` | `GameLitePipeline` |
| Úroveň kvality | výchozí (na počítači *Ultra*) | *Medium* | *Low* |
| Používá | sestavení pro počítače | `PrdivyZavod.apk` | `PrdivyZavod-old.apk` |
| Způsob vykreslování (rendering path) | Forward+ | Forward+ | Forward+ |
| Vyhlazování hran (MSAA) | 4× | 2× | vypnuté |
| Měřítko vykreslování | 100 % | 80 % | 60 % |
| Stíny slunce | 4096, 4 kaskády, 65 m, měkké | 2048, 2 kaskády, 50 m, měkké | 1024, 1 kaskáda, 28 m, ostré |
| Ambient occlusion (SSAO) | ano | ne | ne |
| Bloom | vysoká kvalita | standardní | vypnutý |
| Reflexní sondy | 128 px, prolínané | 128 px, prolínané | 64 px, neprolínané |
| Rozsvícené stropní lampy | všechny | všechny | jen v patře, kde je kamera |
| Textura oblohy | 4096 px | 2048 px (Android) | 2048 px (Android) |
| HDR, tonemapping ACES, barevné korekce, mlha | ano | ano | ano |

Společné pro všechny: lineární barevný prostor, jedno směrové slunce zarovnané
se sluncem na obrázku oblohy, tříbarevné okolní světlo, jedna reflexní sonda
s box projekcí na každé patro zachycená jednou na začátku, měkké částice.

Proč úroveň pro staré telefony rozsvěcí lampy jen v jednom patře: na
grafických čipech omezených na OpenGL ES 3.0 zvládne pipeline kromě slunce
16 světel a parkovací dům má až 27 lamp.

## Kterou úroveň zařízení dostane

Sestavení se spouští v úrovni kvality nastavené pro svou platformu:

- počítač: výchozí úroveň projektu, s pipeline pro počítače;
- `BuildGame.BuildAndroid`: nastaví výchozí úroveň pro Android na *Medium*;
- `BuildGame.BuildAndroidOld`: nastaví ji pro to sestavení na *Low* a pak
  vrátí zpět.

Ve hře žádný přepínač není. Jak si úroveň prohlédnout na počítači:
`ShotRunner.Run -mobileQuality` nebo `-liteQuality`.

## Naměřeno

Na AMD Radeon 780M (integrovaná grafika) při vykreslování jednoho pohledu
v 1080p uvnitř editoru: asi 12–29 ms na snímek na úrovni pro počítače, asi
8 ms na úrovni pro telefony, asi 7 ms na úrovni pro staré telefony. Tahle
čísla kolísají podle toho, co dalšího počítač zrovna dělá, a o telefonech
neříkají nic; na telefonu se nic neměřilo.
