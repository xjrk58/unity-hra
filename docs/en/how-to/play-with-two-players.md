# How to play with two players

Two people, two devices, the same match; both bots and the monster take part
as well. The devices can be any mix of Android phones or tablets and computers.

## Over Wi-Fi

Both devices must be on the **same Wi-Fi network**.

1. On the first device choose **Hra pro dva: založit** ("set up"). It shows
   **Kód hry** – a number, the last part of that device's network address –
   and waits.
2. On the second device choose **Hra pro dva: připojit se** ("join"), then either
   - tap the game in the list **Nalezené hry** on the left, or
   - tap the code in on the number pad and press **OK**.
3. The match starts when both devices have built it. Until then they show
   **Čekám na druhého hráče…**.

## Over Bluetooth (two Android devices)

No Wi-Fi is needed, but the two devices must be **paired** first:
*Settings ▸ Bluetooth* on both, pair them as you would pair any two phones.

1. Switch Bluetooth on on both.
2. First device: **Hra pro dva: založit**. If Android asks whether the game
   may find, connect to and determine the position of *nearby devices*, allow
   it. The screen then also says under which name the phone is waiting on
   Bluetooth.
3. Second device: **Hra pro dva: připojit se**, allow the same question, and in
   the list tap **Bluetooth: _name of the first device_**. Connecting takes a
   few seconds.

A hosting phone waits on Wi-Fi and on Bluetooth at the same time and takes
whichever connection arrives first. Computers cannot use Bluetooth for the
game.

## During and after the match

- Each player sees themselves as **Ty** and the other as **Hráč 1** (the host)
  or **Hráč 2** (the one who joined).
- After the match either player can start the next one: `R` on a computer, a
  tap on a touch screen.
- If the player who joined leaves, the host plays on, and the host's next match
  is an ordinary solo one. If the host leaves, the other device goes back to
  the title screen with **Spojení s hostitelem se přerušilo.**

## If it does not connect

| What you see | What to do |
|--------------|------------|
| The game is not in the list | Type the code instead. Some networks and phones do not pass on the announcements the list is built from. |
| **Na této adrese žádná hra nečeká** | The code is wrong, or the other device is not on the *set up* screen any more. |
| **Hostitel neodpovídá** | The two devices are not on the same network (guest Wi-Fi networks often keep devices apart), or a firewall is in the way. |
| The host is a computer | Its firewall must allow incoming TCP port 47800 – and UDP 47801 if the game should show up in the list. On macOS answer *Allow* to the two questions about the local network and incoming connections. If you cannot change the firewall, let the phone host and the computer join. |
| **Přes Bluetooth se nepodařilo připojit** | The other phone must be on the *set up* screen, paired with this one, with Bluetooth on and the permission granted. |
| No Bluetooth entries in the list | Bluetooth is off, the permission was refused (*Settings ▸ Apps ▸ Prdivý závod ▸ Permissions ▸ Nearby devices*), or the phones are not paired. |

How it works underneath is described in
[How two-player works](../explanation/two-player-design.md); the exact
messages in [Network protocol](../reference/network-protocol.md). What has and
has not been tried on real devices is in
[Verification status](../reference/verification-status.md).
