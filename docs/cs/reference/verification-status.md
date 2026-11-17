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
| Standardní sestavení pro Android: menu, sólové zápasy, výraznější prdy, příšera | jeden současný špičkový telefon, Android 17 | nainstalováno a hráno; zápas se postaví za 0,02–0,14 s; příšera se objevila a hráče chytila (lekačka proběhla); v logu žádné chyby |
| Dva hráči přes Wi-Fi | tento telefon jako hostitel, linuxový počítač připojený přes adresu s hráčem, který hraje sám | čtyři zápasy za sebou: obě zařízení postavila každý zápas, skóre i prdnutí seděly, o nový zápas požádal připojený hráč; žádné chyby na žádné straně |

## Nespuštěno

| Oblast | Stav |
|--------|------|
| APK pro staré telefony na starém nebo 32bitovém zařízení | nikdy nespuštěno |
| Hra přes Bluetooth | přeloží se a je zabalená; nikdy nespuštěna – potřebuje dvě spárovaná zařízení s Androidem |
| Dva telefony proti sobě; telefon, který se ke hře *připojuje* (vyzkoušeno bylo jen hostování) | nikdy nespuštěno |
| Nalezení hry v seznamu (oznámení přes UDP) mezi zařízeními | nikdy nepozorováno; firewall vývojového počítače blokuje příchozí provoz, takže se připojoval přes adresu |
| Workflow pro GitHub Actions | napsané, nikdy nespuštěné: potřebuje licenci Unity v tajných údajích repozitáře |
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

- Když se aplikace spustí z počítače (`adb … am start`) ve stejnou chvíli, kdy
  někdo ťukne na její ikonu, spustí se dvakrát a zůstane na černé obrazovce,
  dokud ji nevynutíš ukončit. Při testování se to stalo dvakrát a vypadalo to
  jako zamrzlé sestavení. Spouštěj ji jedním způsobem a jednou.

- Samotní boti dohrají zápas asi za 45–80 sekund, takže příšera (45 s) může
  dorazit, když je zápas skoro u konce.
- Únik přes starou, nízkou okrajovou zídku se nikdy nepodařilo zopakovat;
  zídka se přesto zvýšila.
- APK jsou podepsaná ladicím klíčem: v pořádku pro instalaci mimo obchod, ne
  pro obchod.
