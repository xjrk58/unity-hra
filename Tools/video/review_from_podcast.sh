#!/bin/bash
# Turns a recorded review - for example the "Audio Overview" podcast that
# NotebookLM makes from Tools/video/notebooklm/review-brief.md - into a video:
# the recording as the sound track, the game's screenshots drifting past for as
# long as it lasts.
#
#   Tools/video/review_from_podcast.sh PODCAST.wav [OUT.mp4]
#
# The recording may be .wav, .m4a or .mp3. Needs the screenshots in Shots/
# (see docs: "Run the automated checks"), Python 3 with Pillow and NumPy, and Blender.
set -e
P=$(cd "$(dirname "$0")/../.." && pwd)
VOICE=$(realpath "$1")
OUT=${2:-$P/Videos/review-podcast.mp4}
mkdir -p "$P/Videos" "$P/Logs/video/podcast"
python3 "$P/Tools/video/make_slides.py" "$P/Tools/video/podcast_pictures.json" "$P/Logs/video/podcast" --voice "$VOICE"
blender -b --factory-startup -P "$P/Tools/video/assemble.py" -- "$P/Logs/video/podcast/timeline.json" "$(realpath -m "$OUT")" | grep -E "WROTE|Error"
