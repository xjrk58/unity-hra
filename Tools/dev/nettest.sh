#!/bin/bash
# Two-player link test on one machine: runs a host and an auto-driven joiner
# (both without a window, "-netBot" plays for them) and leaves their logs in
# Logs/net-host.log and Logs/net-client.log. Needs the desktop build
# (Tools > Prdivý závod > Build player).
#
#   Tools/dev/nettest.sh [HOST_SECONDS] [JOINER_SECONDS] [extra host flags...]
#
# The joiner is stopped first when JOINER_SECONDS < HOST_SECONDS (the host must
# carry on alone), the host first the other way round (the joiner must go back
# to the menu).
P=$(cd "$(dirname "$0")/../.." && pwd)
G="$P/Builds/PrdivyZavod/PrdivyZavod.x86_64"
HOST_SECONDS=${1:-80}
JOINER_SECONDS=${2:-62}
shift 2 2>/dev/null
mkdir -p "$P/Logs"
rm -f "$P/Logs/net-host.log" "$P/Logs/net-client.log"
cd "$P/Builds/PrdivyZavod" || exit 1
timeout "$HOST_SECONDS" "$G" -batchmode -nographics -host -netBot "$@" -logFile "$P/Logs/net-host.log" >/dev/null 2>&1 &
timeout "$JOINER_SECONDS" "$G" -batchmode -nographics -join 127.0.0.1 -netBot -logFile "$P/Logs/net-client.log" >/dev/null 2>&1 &
wait
exit 0
