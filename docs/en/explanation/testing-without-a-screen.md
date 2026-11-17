# Explanation: testing without a screen

Most of this game was checked by programs rather than by playing it. This page
explains how that works, what it is good for, and where it stops.

## The idea

Unity's editor can run without a window. Started that way and told to execute
one method, it can enter Play mode, let the real game run, look at it from
outside, and exit with a verdict. Each "runner" in `Assets/Editor` is such a
method. Because the world is built by code
([How the world is built](how-the-world-is-built.md)), nothing needs to be
clicked into place first.

The game's clock is put on fixed steps of a thirtieth of a second, as fast as
the computer can compute them. A four-minute match takes well under a minute,
and runs the same way every time.

## Five kinds of check

**A match with nobody in it.** `PlaytestRunner` lets the bots play and watches
them: do they collect on every deck, climb the ramps, deliver, and never stand
still for long? It counts every error the game logs, then reloads the scene
the way a player pressing `R` would. Many bugs are simply "a bot got stuck" or
"something threw an exception in minute three".

**Scripted fingers.** The touch controls cannot be tried on a computer by
hand. But the game reads fingers through one small function, and a test can
replace it: put a finger down here, drag it there, put a second one down while
the first is held. `TouchTestRunner` checks that the stick walks and sprints,
that dragging turns the view by the right angle, that both work at once, that
buttons work and that a finger which started on a button never turns the view.

**The monster on a leash.** `MonsterTestRunner` first waits for the monster to
arrive by itself and checks when and where it does. Then it uses a back door
to put the monster wherever a check needs it – next to a player who stands
still, on another deck, under the crosshair.

**Two minutes in a paddock.** `AnimalTestRunner` lets the horses and unicorns
wander while everybody else stands still, and follows each one: how far it
walked, how often it farted, how close it came to a column, whether it ever
stood over a hole, and whether everything it dropped lies on floor that can be
reached. Then it stands an animal where a check needs it – tail towards a
hole, tail towards the edge, in a corner with somebody walking up – and counts
what a backpack gains from a horse's fart and from a rainbow.

**Two programs talking.** For two-player, two copies of the built game run on
one computer, one hosting and one joining, both playing by themselves and each
writing a status line every second. Comparing the two logs shows whether they
agree.

## Pictures are evidence only if somebody looks

`ShotRunner` renders the game from fixed viewpoints. That a file was written
proves nothing; the pictures have to be looked at. This is how the black
floors, the missing lower jaw of the monster and a sound credit running off
the screen were found. The HUD needs a trick – an overlay only exists on a
real screen, so for the photograph it is hung in front of the camera.

## What programs cannot check

- **Sound.** A program can measure loudness and pitch, not whether a roar is
  frightening.
- **Feel.** Whether the stick is comfortable under a thumb, or the monster too
  fast to be fun.
- **Real devices.** A phone's graphics chip, its permissions, its habit of
  leaving the Wi-Fi when the screen goes dark.

Each of these bit at least once. A build that looked fine on the computer sat
on a black screen on a phone – twice – and the phone dropped off the network
before its log could be read. Two days later the log showed why: the app had
been started from the computer and from its icon in the same second. Nothing a
test on the computer would ever have found. The honest list of what has not
been tried is
[Verification status](../reference/verification-status.md).

## Two lessons from the tests themselves

**A test can be wrong.** The touch test once failed because the player
happened to walk through a fart: the farts lie in random places, and the test
had assumed an exact backpack count. It had passed before by luck. The check
now compares before and after.

**Cleaning up is part of the test.** The first two-player runs "failed"
because programs from the previous run were still holding the network port.
Nothing was wrong with the game.

## Why bother, for a small game

Because the alternative is not "test by hand" but "do not test". Nobody plays
through every deck, both control schemes, the monster's every phase and a
restart after each small change. A minute of computer time does.

How to run all of it: [Run the automated checks](../how-to/run-the-automated-checks.md).
