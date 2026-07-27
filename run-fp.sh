#!/bin/bash
set +e
pkill -9 -f Xvfb 2>/dev/null
sleep 1
rm -f /tmp/xvfb.log /tmp/fp-stdout.log /tmp/fp-stderr.log
# Use display :77 to avoid conflicts with any leftover X servers
/usr/bin/Xvfb :77 -screen 0 1280x800x24 -nolisten tcp >/tmp/xvfb.log 2>&1 &
XVFB_PID=$!
sleep 2
if ! kill -0 $XVFB_PID 2>/dev/null; then
  echo "Xvfb failed to start:"
  cat /tmp/xvfb.log
  exit 1
fi
cd /workspace/src/ForkPlus/bin/Debug/net10.0
echo "=== Running ForkPlus (PID $$) ==="
DISPLAY=:77 DOTNET_ROOT=/root/.dotnet timeout 15 /root/.dotnet/dotnet exec ./ForkPlus.dll > /tmp/fp-stdout.log 2> /tmp/fp-stderr.log
EC=$?
kill -9 $XVFB_PID 2>/dev/null
echo "=== EXIT CODE: $EC (124 = timeout, expected for GUI app) ==="
echo "=== STDOUT ==="
cat /tmp/fp-stdout.log
echo "=== STDERR (last 60 lines) ==="
tail -60 /tmp/fp-stderr.log
echo "=== App log (last 30 lines) ==="
tail -30 /root/.local/share/ForkPlus/logs/fork.log 2>/dev/null
