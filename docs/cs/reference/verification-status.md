# Reference: stav ověření

Co se doopravdy spustilo a kde. Přečti si to, než se spolehneš na sestavení
pro platformu uvedenou v části „Nespuštěno“.

Vývoj a testování probíhaly na jednom notebooku s Linuxem (integrovaná grafika
AMD Radeon 780M). Naposledy aktualizováno podle stavu repozitáře při jeho
prvním zveřejnění.

## Ověřeno na vývojovém počítači

| Oblast | Jak | Výsledek |
|--------|-----|----------|
| Logika sólového zápasu | `PlaytestRunner` – boti sbírají ve všech patrech, používají rampy, odevzdávají, nikdy se nezastaví; restart | prochází |
| Dotykové ovládání | `TouchTestRunner` – 16 kontrol s naskriptovanými prsty | prochází |
| Příšera | `MonsterTestRunner` – 22 kontrol | prochází |
| Vykreslování, tři grafické úrovně | snímky obrazovky z `ShotRunner`, prohlédnuté | podle záměru |
| Menu, HUD se čtyřmi závodníky, konečné pořadí | snímky obrazovky ze sestavené hry | podle záměru |
| Sestavení pro Linux | spustí se, vykresluje, načte všech 63 nahrávek | funguje |
| Dva hráči přes TCP | hostitel a připojený hráč, který hraje sám, jako dva procesy na jednom počítači: sbírání, skórování, prdnutí oběma směry, příšera u připojeného hráče (včetně chycení a prdnutí po ní), konec zápasu, nový zápas na žádost kterékoli strany, zmizení kterékoli strany | funguje |
| APK pro Android (standardní a pro staré telefony) | sestavení; prozkoumán balíček, oprávnění, typy procesorů a třída pro Bluetooth | podle záměru |

## Spuštěno na skutečném zařízení

| Co | Zařízení | Výsledek |
|----|----------|----------|
| **První** sestavení pro Android – ještě než existovalo menu, příšera, hra ve dvou a Bluetooth | jeden současný špičkový telefon, Android 17 | nainstalovalo se, vykreslovalo správně, dalo se hrát dotykovým ovládáním, v logu žádné chyby |

## Nespuštěno

| Oblast | Stav |
|--------|------|
| Jakékoli sestavení pro Android od toho prvního | sestaveno, na zařízení nikdy nespuštěno. Jedno průběžné testovací sestavení zamrzlo na černé obrazovce hned po instalaci a spuštění z počítače; příčina se nezjistila (log ukazuje spíš na to, že aplikace byla spuštěna dvakrát najednou, než na kód hry). |
| APK pro staré telefony na starém nebo 32bitovém zařízení | nikdy nespuštěno |
| Hra přes Bluetooth | přeloží se a je zabalená; nikdy nespuštěna – potřebuje dvě spárovaná zařízení s Androidem |
| Dva hráči na dvou samostatných zařízeních přes Wi-Fi | nikdy nespuštěno; jen dva procesy na jednom počítači |
| Nalezení hry v seznamu (oznámení přes UDP) mezi zařízeními | nikdy nepozorováno; firewall vývojového počítače blokuje příchozí provoz |
| Sestavení pro macOS | nikdy nesestaveno ani nespuštěno; návod vychází ze sestavovacího skriptu a obvyklého postupu Unity |
| Sestavení pro Windows | nikdy nesestaveno ani nespuštěno |
| Snímková frekvence na jakémkoli telefonu | neměřena |
| Systémové tlačítko Zpět na Androidu | nevyzkoušeno |
| Odeslání hry na pozadí během zápasu pro dva | nevyzkoušeno |

## Ověřeno jen čísly

Zvuky příšery jsou syntetizované. Změřila se jejich hlasitost a frekvenční
obsah (dost energie nad 150 Hz, aby prošly reproduktorem telefonu), ale nikdo
je neposoudil sluchem. Nahrávky prdů poslechnuté byly.

## Známá slabá místa

- Samotní boti dohrají zápas asi za 45–80 sekund, takže příšera (45 s) může
  dorazit, když je zápas skoro u konce.
- Únik přes starou, nízkou okrajovou zídku se nikdy nepodařilo zopakovat;
  zídka se přesto zvýšila.
- APK jsou podepsaná ladicím klíčem: v pořádku pro instalaci mimo obchod, ne
  pro obchod.
