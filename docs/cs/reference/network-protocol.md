# Reference: síťový protokol

Dvě zařízení, jedno spojení. Zařízení, které hru založilo, je **hostitel**,
to druhé je **připojené zařízení**.

## Přenos

| | Wi-Fi | Bluetooth |
|---|---|---|
| Spojení | TCP, port **47800**, hostitel naslouchá | sériová linka RFCOMM, hostitel naslouchá |
| Nalezení hostitele | UDP broadcast na port **47801** jednou za sekundu: text `PRDZ1\|device model`; nebo kód hry (poslední číslo IPv4 adresy hostitele) | seznam spárovaných zařízení na připojeném zařízení (telefony, tablety, počítače) |
| Identifikátor služby | – | UUID `5f0d7c1e-6b3a-4e52-9c47-a1d2e8f04780`, název `PrdivyZavod` |
| Oprávnění na Androidu | `INTERNET` | `BLUETOOTH_CONNECT` od Androidu 12; dříve `BLUETOOTH`, `BLUETOOTH_ADMIN` |
| Dostupné na | všech platformách | jen na Androidu |

Hostitel čeká na obou zároveň (na Bluetooth jen tam, kde existuje a je
povolený) a jakmile se jedno spojí, na druhém čekat přestane. Přijme se jen
jedno připojené zařízení. Pokusy o připojení vyprší po 6 s (Wi-Fi) nebo 20 s
(Bluetooth).

## Rámcování

Každá zpráva má tvar

```
u16  length of what follows (little-endian)
u8   message type
...  payload
```

Čísla jsou little-endian; `f32` je číslo s jednoduchou přesností podle
IEEE 754. Zprávy pro jiný zápas než aktuální (`match id`) se ignorují.

## Zprávy

| Typ | Název | Od | Obsah |
|-----|-------|----|-------|
| 2 | Start | hostitel | `u8` id zápasu, `u8` počet prdů, pak pro každý `f32 x, y, z` |
| 3 | Ready | připojené zařízení | `u8` id zápasu |
| 5 | State | hostitel, 20krát za sekundu | viz níže |
| 6 | Input | připojené zařízení, 20krát za sekundu | `u8` id zápasu, `f32 x, y, z` jeho hráče, `f32` yaw ve stupních |
| 7 | Action | připojené zařízení | `u8` id zápasu, `u8` druh, `u8` argument |

Druhy akcí: 1 vyprdnout jeden prd (hostitel ověří, že hráč stojí v cíli),
2 prdnout po závodníkovi s indexem v soupisce *argument*, 3 prdnout po
příšeře, 4 spustit nový zápas (vyhoví se jen po skončení zápasu).

### State

```
u8   match id
f32  match time in seconds
u8   flags: 1 = waiting for both devices, 2 = match over
i8   winner's roster index, or -1
u8   number of competitors, then for each:
       f32 x, y, z;  f32 yaw
       u8  farts carried;  u8 farts delivered
       u8  flags: 1 knocked out, 2 in the goal, 4 knocked out by the monster, 8 has left
       f32 seconds of knock-out left
       u8  blast counter;  u8 last blast's target (roster index, or 100 = the monster)
u32  bit i set = fart i is there to be taken
u8   1 if a monster follows, then:
       u8  phase: 0 dormant, 1 omen, 2 emerging, 3 hunting, 4 feeding, 5 dissolving, 6 lurking
       f32 seconds in that phase
       f32 x, y, z;  f32 yaw
       i8  prey's roster index or -1;  i8 last victim's index or -1
       u8  number of appearances;  u8 counter of times it was blasted away
       f32 speed
```

Pořadí v soupisce je na obou zařízeních stejné: hráč hostitele, hráč
připojeného zařízení, Pšouk, Bzdina.

## Průběh

1. Připojené zařízení se připojí.
2. Hostitel postaví zápas, pozastaví ho a pošle **Start**.
3. Připojené zařízení postaví podle zprávy **Start** stejný zápas a odpoví
   **Ready**.
4. Hostitel zápas znovu rozběhne (i po 20 s bez odpovědi) a průběžně posílá
   **State**; připojené zařízení průběžně posílá **Input** a také **Action**,
   když jeho hráč něco udělá.
5. Nový zápas: hostitel ho postaví a pošle nový **Start** s dalším id zápasu;
   pokračuje se bodem 3.

Když se spojení přeruší, hostitel označí hráče připojeného zařízení za
odešlého a hraje dál; připojené zařízení se vrátí do menu.

Verze se nedomlouvají a nic se nešifruje; na obou zařízeních musí běžet stejné
sestavení, a to v síti nebo ve spárování, kterým důvěřuješ.
