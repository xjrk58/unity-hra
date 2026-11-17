# Videos

Two short films about the game are made from its own screenshots and sounds:
a "how to play" and a (made-up, tongue-in-cheek) round of reviews, each in
English and Czech. Nothing is filmed: pictures drift and cross-fade, captions
fade in over them, and the sound track is an oom-pah vamp computed by the
script plus the game's fart recordings and monster sounds.

## What you need

- The screenshots: `Shots/desk`, `Shots/mobile`, `Shots/lite` from `ShotRunner`
  and `Shots/net` from the built game (see `docs/en/how-to/run-the-automated-checks.md`).
- The monster's sounds as files: **Tools ▸ Prdivý závod ▸ Export synthesised
  fart sounds** writes them into `FartSounds/`.
- Python 3 with Pillow and NumPy, and Blender 4 (its video editor does the
  cutting and encoding; no ffmpeg is needed).

## Make them

```sh
for v in howto_en howto_cs review_en review_cs; do
  python3 Tools/video/make_slides.py Tools/video/$v.json Logs/video/$v
  blender -b --factory-startup -P Tools/video/assemble.py -- Logs/video/$v/timeline.json "$PWD/Videos/$v.mp4"
done
```

Each takes a few minutes and comes out at 1920×1080, about two minutes long.
The storyboards are the `.json` files here: one entry per picture with its
title, text, length and sounds. Change the words there.

## A review that talks: NotebookLM

Google's NotebookLM can turn a text into a podcast in which two hosts discuss
it. To get one about the game:

1. Open <https://notebooklm.google.com>, create a notebook and add
   `notebooklm/review-brief.md` as a source.
2. In the *Audio Overview* box choose **Customize**, paste the text of
   `notebooklm/prompt-en.txt` (or `prompt-cs.txt`, with the output language set
   to Czech), and generate. It takes a few minutes.
3. Download the recording and run

```sh
Tools/video/review_from_podcast.sh ~/Downloads/the-recording.wav Videos/review-podcast.mp4
```

The result is the podcast as the sound track, with the game's screenshots
drifting past for as long as it lasts.

The four finished videos are in [`Videos/`](../../Videos): `how-to-play-en.mp4`,
`jak-se-to-hraje-cs.mp4`, `review-en.mp4` and `recenze-cs.mp4`.
