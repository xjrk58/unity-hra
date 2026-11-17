# Explanation: the monster

The request was simple: something really scary that shows up about 45 seconds
into a match. This page explains the decisions behind what was built.

## A thing made of gas

The monster is a tall, hunched phantom of dense gas – horns, burning eyes with
slit pupils, a mouth full of needles, hands like rakes, and smoke where legs
would be. It suits a game about farts, and being gas solved two practical
problems at once.

**It needs no path-finding.** It drifts straight at its prey, through columns,
railings and props. The bots need a planner and careful ramp handling to get
round the car park; a second, larger creature threading the same gaps would
have been the most fragile code in the project. Instead its straight line is
part of the threat: nothing you put between you and it helps.

**It needs no collider.** A solid monster would shove bots off their paths and
wedge them against railings. So it is not solid at all: a blast "hits" it when
the aiming ray passes through its body before reaching anything else.

Stairs would have been the awkward case. When its prey leaves the deck the
monster does what gas does: sinks into the floor and boils up near them a
moment later. Running to another deck buys you two seconds, not safety.

## Fear needs warning and release

A monster that is simply there is furniture. The sequence is built around
what the player knows and when:

1. **Omen.** Seven seconds before it comes, the lamps start to stutter and a
   low hum fades in from nowhere in particular. Nothing can be done yet; that
   is the point.
2. **Arrival.** Every lamp dies for half a second, a dissonant chord hits, and
   it comes up out of the floor – on your deck, ten metres or more away, and
   if possible *behind* you. You hear it before you see it.
3. **The hunt.** Its hum now has a place, so it tells you where it is. The
   player's own heartbeat gets faster as it closes, and the edges of the
   screen darken. The HUD says how far it is but not in which direction: you
   have to look.
4. **The catch.** The view is wrenched round to its face, a metre away, jaws
   open, with a roar; then the lights go out for you.
5. **Release.** It leaves, the day does not quite come back, and you know it
   will return. From then on a flicker of the lamps means something.

The darkness is doing two jobs. It makes the scene frightening – glowing eyes
need dark around them – and it is the game's way of telling you the monster's
state without a single icon: night means it is out, dusk means it is away,
day means the match is over.

## Fast enough to matter, slow enough to escape

Bots walk at 4.7 m/s, the player at 6, and sprints at 8.6. The monster starts
at 4.9 and speeds up the longer it hunts, to 6.6.

- It always catches a bot eventually. Bots do not run from it; watching one
  get eaten is both a warning and a small gift to the player.
- A walking player is caught after a few seconds. A sprinting player always
  gets away – but sprinting away is not collecting farts.
- A hunt ends after 28 seconds at the latest, so it cannot pin one player
  for a whole match.

It prefers people three to one, because a monster that mostly chases bots is
not scary, and it never goes straight back for whoever it just caught.

## Something to do about it

Pure helplessness is frightening for ten seconds and annoying afterwards. The
blast – the same two farts that knock out a rival – drives the monster off
for a quarter of a minute. That gives a decision with a price: farts spent on
the monster are not delivered, and you have to turn and face it to aim.

Being caught costs the whole backpack and five seconds. It never costs
delivered points: the monster can swing a match but not undo it.

## Sound without a sound file

Its voice is computed when the game starts: the hum, the heartbeat, the chord,
the roar, the shriek when it is driven off. Its growl is one of the long fart
recordings played three times too slowly, and its roar is the same model of a
fart that the game's fallback sounds use, pushed to an absurd size – it stays
a creature of this game.

A phone's speaker reproduces almost nothing below about 150 Hz, and a "deep"
sound made only of deep frequencies would be silence there. So every sound
carries overtones that small speakers can play: the hum has a rasp and a
breath on top of its bass, the heartbeat a knock on top of its thud.

## Mind and body apart

The class that decides (`FartMonster`) knows nothing about meshes or sound; it
keeps a position, a phase and a prey. `MonsterVisual` and `MonsterAudio` only
read that. Its head, jaw and arms are separate rigid parts turned about their
joints by a little arithmetic every frame – there is no skeleton and no
animation file.

The split paid off twice. Tests can put the monster into any state and take a
picture or assert on it. And in a two-player match the joining device runs the
same body and voice from a state the host sends, without a second mind that
might disagree.

## What is still unknown

Whether it is frightening is a matter for a person in front of a screen with
the sound on, and that judgement has not been made yet
([Verification status](../reference/verification-status.md)). The timing is
also in question: bots alone finish a match in about a minute, so the monster
often arrives late. A longer match would give it more room.
