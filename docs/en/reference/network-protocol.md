# Reference: network protocol

Two devices, one connection. The device that set the game up is the **host**,
the other the **joiner**.

## Transport

| | Wi-Fi | Bluetooth |
|---|---|---|
| Connection | TCP, port **47800**, host listens | RFCOMM serial line, host listens |
| Finding the host | UDP broadcast to port **47801** once a second: the text `PRDZ1\|device model`; or the game code (last number of the host's IPv4 address) | the joiner's list of paired devices (phones, tablets, computers) |
| Service identifier | – | UUID `5f0d7c1e-6b3a-4e52-9c47-a1d2e8f04780`, name `PrdivyZavod` |
| Permissions on Android | `INTERNET` | `BLUETOOTH_CONNECT` from Android 12; `BLUETOOTH`, `BLUETOOTH_ADMIN` before |
| Available on | every platform | Android only |

A host waits on both at once (Bluetooth only where it exists and is allowed)
and stops waiting on the other as soon as one connects. Only one joiner is
accepted. Connection attempts time out after 6 s (Wi-Fi) or 20 s (Bluetooth).

## Framing

Every message is

```
u16  length of what follows (little-endian)
u8   message type
...  payload
```

Numbers are little-endian; `f32` is an IEEE 754 single. Messages for another
match than the current one (`match id`) are ignored.

## Messages

| Type | Name | From | Payload |
|------|------|------|---------|
| 2 | Start | host | `u8` match id, `u8` number of scattered farts, then for each `f32 x, y, z` |
| 3 | Ready | joiner | `u8` match id |
| 5 | State | host, 20 times a second | see below |
| 6 | Input | joiner, 20 times a second | `u8` match id, `f32 x, y, z` of its player, `f32` yaw in degrees |
| 7 | Action | joiner | `u8` match id, `u8` kind, `u8` argument |

Action kinds: 1 expel one fart (the host checks that the player stands in the
goal), 2 blast the competitor with roster index *argument*, 3 blast the
monster, 4 start a new match (only honoured after the match is over).

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
u8   number of animals, then for each:
       f32 x, y, z;  f32 yaw
       u8  fart counter (a change means: it has just farted)
       u8  speed in units of 0.05 m/s;  u8 1 = grazing
       f32 x, y, z of the place where its fart hangs
```

The roster order is the same on both devices: host's player, joiner's player,
Pšouk, Bzdina.

Farts are numbered the same way on both devices: first the scattered ones in
the order of **Start**, then one for each animal. The animals and their farts
are not in **Start**; both devices create the same herd in the same order
(horse, unicorn, horse, unicorn, horse), and the joiner places them from
**State**. Whether an animal's fart is there to be taken is its bit in the
`u32`, like any other fart's; a match has 25 farts, and the field has room for
32.

## Sequence

1. The joiner connects.
2. The host builds the match, pauses it, sends **Start**.
3. The joiner builds the same match from **Start** and answers **Ready**.
4. The host unpauses (also after 20 s without an answer) and sends **State**
   continuously; the joiner sends **Input** continuously and **Action** when
   its player does something.
5. A new match: the host builds it and sends a new **Start** with the next
   match id; continue at 3.

If the connection breaks, the host marks the joiner's player as having left
and plays on; the joiner returns to the menu.

There is no version negotiation and no encryption; both devices must run the
same build, on a network or pairing you trust.
