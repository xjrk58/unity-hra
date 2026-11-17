# Reference: graphics tiers

Three render pipeline assets in `Assets/Settings`, each tied to a Unity
quality level. All are written by `GraphicsSetup.Apply`; edit the numbers
there, not in the assets.

| | Desktop | Phone | Old phone |
|---|---|---|---|
| Pipeline asset | `GamePipeline` | `GameMobilePipeline` | `GameLitePipeline` |
| Quality level | default (*Ultra* on desktop) | *Medium* | *Low* |
| Used by | desktop builds | `PrdivyZavod.apk` | `PrdivyZavod-old.apk` |
| Rendering path | Forward+ | Forward+ | Forward+ |
| Anti-aliasing (MSAA) | 4× | 2× | off |
| Render scale | 100 % | 80 % | 60 % |
| Sun shadows | 4096, 4 cascades, 65 m, soft | 2048, 2 cascades, 50 m, soft | 1024, 1 cascade, 28 m, hard |
| Ambient occlusion (SSAO) | yes | no | no |
| Bloom | high quality | standard | off |
| Reflection probes | 128 px, blended | 128 px, blended | 64 px, not blended |
| Ceiling lamps lit | all | all | only the deck the camera is on |
| Sky texture | 4096 px | 2048 px (Android) | 2048 px (Android) |
| HDR, ACES tonemapping, colour grading, fog | yes | yes | yes |

Common to all: linear colour space, one directional sun aligned with the sun
in the sky picture, three-colour ambient light, one box-projected reflection
probe per deck captured once at the start, soft particles.

The gas – a fart's swirl and beam of light, bursts, the monster's smoke,
rainbows – is drawn with soft particles, which read the depth of the scene.
All three renderers therefore copy that depth right after the solid things are
drawn (`copyDepthMode = AfterOpaques`). With Unity's default, after the
transparent things, the two phone tiers drew no gas at all; the desktop tier
only escaped because its ambient occlusion makes a depth pass of its own.

Why the old-phone tier lights only one deck's lamps: on graphics chips limited
to OpenGL ES 3.0 the pipeline handles 16 lights besides the sun, and the car
park has up to 27 lamps.

## Which tier a device gets

A build starts in the quality level set for its platform:

- desktop: the project's default level, with the desktop pipeline;
- `BuildGame.BuildAndroid`: sets Android's default to *Medium*;
- `BuildGame.BuildAndroidOld`: sets it to *Low* for that build, then back.

There is no in-game switch. To see a tier on a computer:
`ShotRunner.Run -mobileQuality` or `-liteQuality`.

## Measured

On an AMD Radeon 780M (integrated graphics), rendering one 1080p view inside
the editor: about 12–29 ms a frame on the desktop tier, about 8 ms on the phone
tier, about 7 ms on the old-phone tier. These numbers swing with whatever else
the computer is doing and say nothing about phones; nothing has been measured
on a phone.
