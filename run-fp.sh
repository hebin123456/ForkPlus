#!/bin/bash
set +e
pkill -f "Xvfb" 2>/dev/null
sleep 1
Xvfb :99 -screen 0 1280x800x24 -nolisten tcp >/tmp/xvfb.log 2>&1 &
XVFB_PID=$!
sleep 2
export DISPLAY=:99
export DOTNET_ROOT=$(mise where dotnet@10.0.302 2>/dev/null || echo /root/.local/share/mise/installs/dotnet/10.0.302)
export PATH=$DOTNET_ROOT:$PATH
cd /workspace/src/ForkPlus/bin/Debug/net10.0
echo "=== Running ForkPlus (PID $$) ==="
timeout 25 ./ForkPlus > /tmp/fp-stdout.log 2> /tmp/fp-stderr.log
EC=$?
echo "=== EXIT CODE: $EC ==="
kill $XVFB_PID 2>/dev/null
echo "=== STDOUT ==="
cat /tmp/fp-stdout.log
echo "=== STDERR (first 200 lines) ==="
head -200 /tmp/fp-stderr.log
echo "=== ForkPlus data dir ==="
find /root/.local/share -maxdepth 3 -path '*ForkPlus*' 2>/dev/null | head -30
