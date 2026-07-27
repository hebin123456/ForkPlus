#!/bin/bash
set +e
pkill -f "Xvfb" 2>/dev/null
sleep 1
Xvfb :99 -screen 0 1280x800x24 -nolisten tcp >/workspace/xvfb.log 2>&1 &
XVFB_PID=$!
sleep 2
export DISPLAY=:99
cd /workspace/src/ForkPlus/bin/Debug/net10.0
rm -f /workspace/fp-stderr.log /workspace/fp-stdout.log
echo "=== Starting ForkPlus ==="
timeout 15 /root/.dotnet/dotnet exec --runtimeconfig ForkPlus.runtimeconfig.json ForkPlus.dll > /workspace/fp-stdout.log 2> /workspace/fp-stderr.log
EC=$?
echo "=== EXIT CODE: $EC ==="
kill $XVFB_PID 2>/dev/null
echo "=== STDERR lines ==="
wc -l /workspace/fp-stderr.log
echo "=== STDERR ==="
head -100 /workspace/fp-stderr.log
echo "=== STDOUT ==="
head -30 /workspace/fp-stdout.log
