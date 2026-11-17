# Jak hrát ve dvou

Dva lidé, dvě zařízení, stejný zápas; účastní se i oba boti a příšera.
Zařízení můžou být libovolnou kombinací telefonů nebo tabletů s Androidem a
počítačů.

## Přes Wi-Fi

Obě zařízení musí být ve **stejné síti Wi-Fi**.

1. Na prvním zařízení zvol **Hra pro dva: založit**. Ukáže
   **Kód hry** – číslo, poslední část síťové adresy toho zařízení –
   a čeká.
2. Na druhém zařízení zvol **Hra pro dva: připojit se** a pak buď
   - klepni na hru v seznamu **Nalezené hry** vlevo, nebo
   - naťukej kód na číselné klávesnici a stiskni **OK**.
3. Zápas začne, až ho obě zařízení postaví. Do té doby ukazují
   **Čekám na druhého hráče…**.

## Přes Bluetooth (dvě zařízení s Androidem)

Wi-Fi není potřeba, ale obě zařízení musí být nejdřív **spárovaná**:
na obou *Settings ▸ Bluetooth*, spáruj je stejně jako kterékoli jiné dva
telefony.

1. Na obou zapni Bluetooth.
2. První zařízení: **Hra pro dva: založit**. Pokud se Android zeptá, jestli
   hra smí vyhledávat *nearby devices* (zařízení v okolí), připojovat se k nim
   a zjišťovat jejich polohu, povol to. Na obrazovce pak je i to, pod jakým
   jménem telefon na Bluetooth čeká.
3. Druhé zařízení: **Hra pro dva: připojit se**, povol stejný dotaz a
   v seznamu klepni na **Bluetooth: _název prvního zařízení_**. Připojení trvá
   pár sekund.

Hostující telefon čeká na Wi-Fi i na Bluetooth zároveň a vezme to spojení,
které přijde první. Počítače Bluetooth pro hru použít neumějí.

## Během zápasu a po něm

- Každý hráč vidí sebe jako **Ty** a toho druhého jako **Hráč 1** (hostitel)
  nebo **Hráč 2** (ten, kdo se připojil).
- Po zápase může další spustit kterýkoli hráč: `R` na počítači, klepnutí na
  dotykové obrazovce.
- Když připojený hráč odejde, hostitel hraje dál a jeho další zápas je
  obyčejný sólový. Když odejde hostitel, druhé zařízení se vrátí na úvodní
  obrazovku s hlášením **Spojení s hostitelem se přerušilo.**

## Když se to nepřipojí

| Co vidíš | Co dělat |
|----------|----------|
| Hra není v seznamu | Zadej místo toho kód. Některé sítě a telefony nepředávají oznámení, ze kterých se seznam sestavuje. |
| **Na této adrese žádná hra nečeká** | Kód je špatně, nebo druhé zařízení už není na obrazovce *založit*. |
| **Hostitel neodpovídá** | Zařízení nejsou ve stejné síti (sítě Wi-Fi pro hosty často zařízení od sebe oddělují), nebo stojí v cestě firewall. |
| Hostitelem je počítač | Jeho firewall musí povolit příchozí TCP port 47800 – a UDP 47801, pokud se má hra objevit v seznamu. V macOS odpověz *Allow* na oba dotazy ohledně místní sítě a příchozích spojení. Pokud firewall změnit nemůžeš, ať hostuje telefon a počítač se připojí. |
| **Přes Bluetooth se nepodařilo připojit** | Druhý telefon musí být na obrazovce *založit*, spárovaný s tímhle, se zapnutým Bluetooth a uděleným oprávněním. |
| V seznamu nejsou žádné položky Bluetooth | Bluetooth je vypnutý, oprávnění bylo zamítnuto (*Settings ▸ Apps ▸ Prdivý závod ▸ Permissions ▸ Nearby devices*), nebo telefony nejsou spárované. |

Jak to funguje uvnitř, popisuje stránka
[Jak funguje hra ve dvou](../explanation/two-player-design.md); přesné
zprávy jsou na stránce [Síťový protokol](../reference/network-protocol.md). Co
se na skutečných zařízeních vyzkoušelo a co ne, je na stránce
[Stav ověření](../reference/verification-status.md).
