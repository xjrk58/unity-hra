# Reference: assety a licence

## Projekt

Kód hry, skripty v `Tools/`, modely vygenerované těmi skripty (postava,
příšera, kůň, zábradlí, sloup, lampa, cíl), ikona aplikace, syntetizované zvuky a
tahle dokumentace jsou vydané pod **licencí MIT** – viz
[`LICENSE`](../../../LICENSE).

Soubory uvedené níže pocházejí od jiných lidí a zůstávají pod svými vlastními
licencemi.

## Assety třetích stran

| Co | Kde v projektu | Zdroj | Licence |
|----|----------------|-------|---------|
| Sady PBR textur: beton, asfalt, zemina, slzičkový plech, kůže, látka | `Assets/Resources/Textures` | [Poly Haven](https://polyhaven.com) | CC0 1.0 (volné dílo) |
| Obloha: HDRI *Autumn Field (Pure Sky)* | `Assets/Resources/Sky/Sky.hdr` | Poly Haven | CC0 1.0 |
| Naskenované rekvizity: sudy, zábrana, bedna, krabice | `Assets/Resources/Models/Props`, `Assets/Resources/Textures/Props` | Poly Haven | CC0 1.0 |
| Nahrávky prdů (63 klipů) | `Assets/Resources/Audio/Farts` | *„Farts - pieruja“* od **Yle Arkisto** (zvukový archiv Yleisradio), 1986 – [Freesound #425594](https://freesound.org/people/YleArkisto/sounds/425594/), kopie na [Wikimedia Commons](https://commons.wikimedia.org/wiki/File:425594_ylearkisto_farts-pieruja.wav) | [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) |

### Co vyžaduje CC BY 3.0

Nahrávky se smějí používat, upravovat a šířit dál, i komerčně, pokud je uveden
autor a vyznačeny změny. Projekt to dělá na třech místech; zachovej je, když
hru nebo klipy šíříš dál:

- `Assets/Resources/Audio/Farts/ATTRIBUTION.txt` – zdroj, licence, co se změnilo;
- banner na konci zápasu ve hře – jeden řádek s uvedením autora;
- tahle stránka a README.

Změny provedené v nahrávce: rozřezání na jednotlivé klipy, smíchání do mona,
filtr typu horní propust, náběhy a doběhy hlasitosti, normalizace hlasitosti
(`Tools/assets/slice_farts.py`).

Pokud uvádět autora nechceš, klipy smaž: hra pak zvuky prdů syntetizuje.

## Co v repozitáři není

- Samotné **Unity** a balíček Universal Render Pipeline stahuje Unity Hub a
  vztahují se na ně vlastní podmínky Unity. Sestavená hra obsahuje runtime
  Unity.
- **Zdrojové soubory**, ze kterých vznikly textury, obloha a rekvizity,
  stahuje `Tools/assets/fetch.py`; v repozitáři jsou jen zpracované výsledky.

## Písma

Hra používá vestavěné písmo Unity a na telefonech systémové písmo. V repozitáři
žádné soubory písem nejsou.
