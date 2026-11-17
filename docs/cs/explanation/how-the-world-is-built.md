# Vysvětlení: jak se staví svět

Otevři scénu v editoru a najdeš jediný objekt, `Bootstrap`. Žádný level
k prohlížení tam není. Všechno – parkovací dům, oblohu, světla, hráče, prdy,
menu – vytvoří kód v prvním snímku. Tahle stránka vysvětluje proč a co z toho
plyne.

## Jeden popis arény

Parkovací dům určuje hrstka konstant v `Arena`: kolik pater, jak daleko od
sebe, kde rampy začínají a končí, kde jsou otvory. Čtou je tři velmi odlišné
části kódu:

- `ArenaBuilder` vyrábí desky, rampy, zábradlí a collidery;
- `GameBootstrap` rozhoduje, kde smějí ležet prdy;
- boti (`AICompetitor`, `NavPlanner`) rozhodují, kudy můžou chodit.

Kdyby se level modeloval ručně, představa botů o něm by byla druhá kopie,
která se s každou úpravou vzdaluje od té první. Když se staví ze sdílených
čísel, svět, ve kterém boti plánují, *je* ten svět, který se postaví. Změň
`RampWidth` a rampa, její obrubníky, její otvor v patře nad ní, zábradlí kolem
toho otvoru i cesta botů se posunou společně.

Díky stejné vlastnosti je jednoduchá i hra ve dvou: obě zařízení postaví
totožnou arénu, aniž by si vyměnila jediný trojúhelník, takže síť musí
přenášet jen to, co se hýbe.

## Velké ploché věci se generují, podrobné věci jsou modely

Desky, rampy, obrubníky a trámy jsou kvádry z `MeshFactory` s texturovými
souřadnicemi ve skutečných metrech, takže betonová textura má stejné měřítko
na 26m patře i na 30cm obrubníku. Věci, které mají tvar – díl zábradlí, sloup,
lampa, cíl, závodníci, příšera, koně – jsou modely FBX, samy vygenerované skripty
pro Blender, a rozmisťují se podle mřížky.

Kód hledá části modelu podle jména („GoalFan“, „MonsterJaw“) a každý slot
materiálu vymění za některý z vlastních materiálů projektu. Modely proto
nenesou žádné textury ani vlastní vzhled; vzhled žije na jednom místě,
v `GraphicsSetup`.

## Materiály se generují taky

`GraphicsSetup` zapisuje každý materiál, který hra načítá. Není to
pořádkumilovnost pro ni samu: Unity dá do sestavení jen ty varianty shaderů,
které nějaký asset potřebuje. Materiál vymyšlený až za běhu by v editoru
fungoval a v sestavení by vyšel růžový. Když se materiály generují jako assety
z jednoho skriptu, zůstávají editor a sestavení totožné a z otázky „jak hra
vypadá“ se stává soubor, který se dá číst a porovnávat.

## Světlo

Záměrem byla scéna, která působí jako fotografie skutečného místa, ne jako
herní level:

- Obloha je fotografie (HDRI). Slunce je směrové světlo umístěné přesně tam,
  kde je slunce na té fotografii, takže stíny souhlasí s oblohou.
- Skoro celá hra se odehrává pod betonovou deskou. Světlo tam přichází ze
  stran otevřenými okraji a odráží se od osluněné podlahy, takže okolní světlo
  je ručně vyladěný tříbarevný přechod, ne světlo otevřené oblohy.
- Každé patro má jednu reflexní sondu, zachycenou jednou, když svět existuje,
  takže lesklé podlahy odrážejí místnost, ve které jsou, a ne oblohu.
- Tone mapping, bloom a trocha barevných korekcí se aplikují jen na scénu.
  HUD se kreslí až potom, nedotčený.

`LightingRig.SetMood` umí tohle všechno – slunce, oblohu, okolní světlo, mlhu,
odrazy, barvu – plynule posunout ze slunečného dne do zeleného šera. Právě
tímhle jedním knoflíkem otáčí příšera.

## Vstup na jednom místě

`GameInput` je jediný kód, který ví, jestli se používá klávesnice, nebo
dotyková obrazovka. Kód hráče se ptá „kterým směrem?“, „skok?“, „prdnout?“ a
v obou případech dostane stejný druh odpovědi. Na dotykových obrazovkách si
každý ovládací prvek drží prst, který na něm začal, a díky tomu můžeš zároveň
jít, rozhlížet se a mačkat tlačítko; a protože prsty přicházejí přes jedno
malé rozhraní, může test dodat naskriptované.

## Co tenhle přístup stojí

- Nikdo nemůže rekvizitu v editoru jen tak pošoupnout. Každá změna je změna
  kódu a kontroluje se na snímcích obrazovky.
- První snímek udělá všechnu práci. Na počítači to je třetina sekundy; loguje
  se to (`[Boot]`), protože na pomalém telefonu to tak být nemusí.
- Soubor scény nováčkovi nic neřekne. Začni číst u `GameBootstrap.Awake`.

Pro tuhle malou hru je to dobrý obchod: jeden člověk udrží celý svět
v několika konstantách a stejné sestavení se dá prověřit bez obrazovky
([Testování bez obrazovky](testing-without-a-screen.md)).
