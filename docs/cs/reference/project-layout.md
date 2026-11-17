# Reference: uspořádání projektu a skripty

Unity 6 (6000.4.11f1), Universal Render Pipeline 17.4, uGUI, vestavěný input
manager. Žádné další balíčky a žádný obsah z Asset Store.

## Složky

| Cesta | Obsah |
|-------|-------|
| `Assets/Scenes/Main.unity` | jediná scéna: jeden objekt, `Bootstrap` |
| `Assets/Scripts/` | hra |
| `Assets/Editor/` | sestavovací skript, nastavení grafiky, runnery bez okna |
| `Assets/Resources/Models` | modely FBX: postava, příšera, zábradlí, sloup, lampa, cíl, naskenované rekvizity |
| `Assets/Resources/Textures`, `Sky` | sady PBR textur, sprite částic, HDRI obloha |
| `Assets/Resources/Materials` | generuje je `GraphicsSetup`; za běhu se načítají podle jména |
| `Assets/Resources/Audio/Farts` | 63 nahrávek prdů a uvedení jejich autora |
| `Assets/Settings` | tři assety render pipeline se svými renderery |
| `Assets/Plugins/Android/BtLink.androidlib` | pomocná třída v Javě a manifest pro hru přes Bluetooth |
| `Assets/Icon.png` | ikona aplikace |
| `Tools/assets`, `Tools/blender`, `Tools/dev` | skripty, které vytvářejí assety; test spojení |
| `docs/` | tahle dokumentace, anglicky a česky |
| `Builds/`, `Logs/`, `Shots/`, `FartSounds/`, `Library/` | výstupy; nejsou v gitu |

## Skripty hry (`Assets/Scripts`)

| Skript | Role |
|--------|------|
| `GameBootstrap` | vstupní bod: podle režimu relace postaví pozadí menu, nebo svět, cíl, závodníky, prdy a příšeru |
| `Session` | co tenhle běh hraje (menu / sólo / hostitel / připojený), drží se přes načítání scén; čte příkazovou řádku |
| `MainMenu` | úvodní obrazovka: sólo, hostování, připojení ze seznamu nebo kódem; `MenuOrbit` pohybuje její kamerou |
| `Arena` | rozměry arény, sdílené stavbou, kolizemi a navigací |
| `ArenaBuilder` | desky, rampy, zábradlí, sloupy, trámy, lampy, rekvizity, značení |
| `MeshFactory` | procedurální kvádry s UV v metrech |
| `Mats` | knihovna materiálů (`Resources/Materials`) |
| `LightingRig` | HDRI obloha, slunce, okolní světlo, mlha, reflexní sondy, post-processing, nálada od dne po šero |
| `GameManager` | soupiska, prdy, skóre, vítěz, čas zápasu, restart |
| `Competitor` | co mají hráči a boti společné: batůžek, skóre, prdnutí, vyřazení |
| `PlayerController` | člověk na tomhle zařízení: pohyb, pohled, vyprdnutí, prdnutí, lekačka |
| `GameInput` | ovládání na jednom místě: klávesnice a myš, nebo dotyk |
| `AICompetitor` | boti: sbírají, odevzdávají, prdnou po soupeři, používají rampy |
| `NavPlanner` | plánování cesty v jednom patře (graf viditelnosti kolem překážek) |
| `CharacterVisual` | model postavy, procedurální animace běhu, nádrž na plyn |
| `FartPickup` | prd, který se dá sebrat |
| `GoalZone` | trigger cíle |
| `GasFx` | částicové efekty |
| `FartAudio` | zvuky prdů: nahrávky, syntéza jako záloha |
| `FartMonster` | chování příšery a šero, které přináší |
| `MonsterVisual` | model příšery a procedurální animace |
| `MonsterAudio` | zvuky příšery, všechny syntetizované |
| `Hud` | HUD ve hře, grafika dotykového ovládání, hlášení, konečné pořadí |
| `NetLink` | spojení mezi dvěma zařízeními: TCP s UDP majákem, nebo Bluetooth |
| `BluetoothLink` | C# strana pomocné třídy pro Bluetooth (jen Android) |
| `NetGame` | udržuje zápas pro dva ve shodě: stav hostitele ven, poloha a žádosti připojeného hráče dovnitř |
| `NetPuppet` | závodník řízený z druhého zařízení |
| `NetBot` | vývojářský nástroj: sám odehraje zápas pro dva a zaloguje ho |
| `DevCapture` | vývojářský nástroj: jeden snímek obrazovky ze sestavené hry |
| `Spinner` | otáčí větrákem cíle |

## Skripty editoru (`Assets/Editor`)

| Skript | Role |
|--------|------|
| `BuildGame` | sestavení pro počítače a Android |
| `GraphicsSetup` | render pipeline, úrovně kvality, nastavení importu textur, materiály |
| `ShotRunner` | snímky obrazovky a benchmark času snímku |
| `PlaytestRunner` | zrychlený zápas botů |
| `TouchTestRunner` | dotykové ovládání s naskriptovanými prsty |
| `MonsterTestRunner` | chování příšery |
| `AudioDump` | exportuje syntetizované zvuky jako WAV |

## Konvence

- Svět se staví z kódu; soubor scény zůstává prázdný. Nové věci se vytvářejí
  v `GameBootstrap`, neumisťují se v editoru.
- Materiály se načítají podle jména a musí vznikat v `GraphicsSetup`, aby se
  jejich varianty shaderů dostaly do sestavení.
- Modely se instancují pod obalovým objektem; části a sloty materiálů se
  hledají podle jména.
- Všechno, co mění stav zápasu, musí fungovat i tehdy, když se tohle zařízení
  jen připojilo k cizímu zápasu: viz
  [Jak funguje hra ve dvou](../explanation/two-player-design.md).
- Texty na obrazovce jsou česky. Kód, komentáře a logy anglicky.
