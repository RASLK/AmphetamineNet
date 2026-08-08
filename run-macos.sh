#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
CFG="${1:-Debug}"
OUT="$ROOT/bin/$CFG/net10.0"
DYLIB_SRC="$ROOT/Native/libcvdisplaylink_fix.dylib"
DYLIB_C="$ROOT/Native/cvdisplaylink_fix.c"

if [[ ! -f "$DYLIB_SRC" ]]; then
  clang -dynamiclib -o "$DYLIB_SRC" "$DYLIB_C" \
    -framework CoreVideo -framework CoreGraphics \
    -install_name @rpath/libcvdisplaylink_fix.dylib
fi

dotnet build -c "$CFG" -v q
cp -f "$DYLIB_SRC" "$OUT/libcvdisplaylink_fix.dylib"

: > /tmp/amphetamine-net-run.log
cd "$OUT"
# macOS 26+: Avalonia RenderTimer uses broken CVDisplayLinkCreateWithActiveCGDisplays
exec env \
  DYLD_INSERT_LIBRARIES="$OUT/libcvdisplaylink_fix.dylib" \
  AMPHETAMINE_NET_CVFIX=1 \
  ./AmphetamineNet >> /tmp/amphetamine-net-run.log 2>&1
