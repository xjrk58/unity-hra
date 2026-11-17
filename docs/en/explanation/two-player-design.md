# Explanation: how two-player works

Two devices show the same match. This page explains how they agree, and why it
was built this way rather than another.

## One device decides

The device that set the game up – the host – runs the whole world exactly as
in a solo game: bots, farts, the monster, scores, who has won. Twenty times a
second it sends the other device a complete description of that world. The
joining device runs none of that. It shows what it is told.

The alternative – both devices simulating and exchanging events – is faster to
react but can disagree: two players touch the same fart in the same instant,
and each device gives it to its own player. With one device deciding, the
question never arises. For a game where a twentieth of a second does not
matter, agreement is worth more than immediacy.

## Each device moves its own player

The exception is your own movement. If the joiner's steps had to travel to the
host and back before the view moved, the game would feel like wading. So each
device moves its own player at once and merely reports the position; the other
device shows that player as a puppet gliding after the reports.

This means the host believes the joiner about where it is. Between two people
on a sofa that is the right amount of trust.

## Asking instead of doing

Anything the joining player does that changes the match is a request to the
host: *expel one*, *blast number 2*, *blast the monster*, *new match*. The host
checks what it can (are you in the goal? can you pay?) and does it, and the
result comes back in the next description of the world.

In the code this is the rule to keep: a device that has only joined must not
change scores, backpacks, farts, knock-outs or the monster on its own. The
places that would – picking up a fart, expelling, blasting, deciding the
winner, the monster's whole mind, the bots – all check which kind of device
they are on.

## Effects are deduced, not sent

The host does not send "play a fart sound here". The joining device compares
each description with the last one: a score went up, so somebody expelled –
puff and sound at that competitor; a blast counter changed, so draw the jet
from them to their victim; the monster's phase changed to *feeding* with this
device's player as victim, so start the scare.

Fewer kinds of message means fewer ways for the two sides to fall out of step,
and a lost or late message cannot leave an effect half-played: the next
description puts everything right.

## Starting together

Devices load at different speeds. If the host started the clock while a slow
phone was still building the car park, the host would have a head start and
the monster's 45 seconds would mean different things on each screen. So the
host builds the match, freezes it, tells the joiner where the farts are, and
waits for "ready" (twenty seconds at most). The match has its own clock that
only runs while it is not frozen.

Every match has a number. A message from the previous match that arrives
after a restart is recognised and dropped.

## Finding each other

On a home network nobody should have to type an address. The host shouts a
short announcement to the whole network once a second, and the joining device
lists what it hears. Some networks and phones swallow such announcements, so
there is a second way that always works: the host shows the last number of its
address as a "game code", and since both devices are on the same network the
joiner can rebuild the full address from its own.

## Bluetooth is just another wire

Everything above travels as small length-prefixed messages over one
connection. `NetLink` does not care what the connection is. Over Wi-Fi it is a
TCP socket; between two paired Android phones it can be a Bluetooth serial
line, handled by a small Java class because Unity has no Bluetooth of its own.
A hosting phone waits on both and takes whichever arrives first. The game
above the wire is unchanged.

Bluetooth play uses paired devices only. That avoids scanning, which on
Android drags in location permissions and a discoverability dialog – too much
ceremony for a game about farts.

## When the wire breaks

If the joiner disappears, the host marks that player as gone and the match
goes on; the next match is an ordinary solo one. If the host disappears, there
is no world left to show, so the joining device returns to the menu and says
so.

## What it is not

It is for two players on one network or one Bluetooth pairing. There are no
servers, accounts, lobbies or internet play; no encryption and no protection
against a modified game; and both devices must run the same build, because
they do not compare versions. Those are deliberate limits for a game played by
people in the same room.

The exact bytes are in [Network protocol](../reference/network-protocol.md).
What has been tried on real devices is in
[Verification status](../reference/verification-status.md).
