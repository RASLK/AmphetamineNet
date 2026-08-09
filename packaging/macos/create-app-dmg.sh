#!/usr/bin/env bash
# Build AmphetamineNet.app + UDZO DMG from a published single-file binary.
# Usage:
#   ./create-app-dmg.sh <published-binary> <version> <output-dmg-path> [icon-source.png|ico] [dylib-path]
set -euo pipefail

SRC_BIN="${1:?published binary path}"
VERSION="${2:?version}"
OUT_DMG="${3:?output .dmg path}"
ICON_SRC="${4:-}"
DYLIB_SRC="${5:-}"

ROOT="$(cd "$(dirname "$0")" && pwd)"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

# .app is built directly inside the DMG staging root to avoid a second
# full copy of the (potentially large, self-contained) bundle.
DMG_ROOT="${STAGE}/dmgroot"
APP_DIR="${DMG_ROOT}/AmphetamineNet.app"
MACOS_DIR="${APP_DIR}/Contents/MacOS"
RES_DIR="${APP_DIR}/Contents/Resources"
mkdir -p "$MACOS_DIR" "$RES_DIR"

cp "$SRC_BIN" "${MACOS_DIR}/AmphetamineNet"
chmod +x "${MACOS_DIR}/AmphetamineNet"

# The app self-relaunches with DYLD_INSERT_LIBRARIES pointed at this dylib
# (see Program.cs) — it is looked up next to the executable.
if [[ -n "$DYLIB_SRC" && -f "$DYLIB_SRC" ]]; then
  cp "$DYLIB_SRC" "${MACOS_DIR}/libcvdisplaylink_fix.dylib"
fi

sed "s/__VERSION__/${VERSION}/g" "${ROOT}/Info.plist" > "${APP_DIR}/Contents/Info.plist"

# Optional .icns from PNG (or first frame of ICO via sips when possible).
if [[ -n "$ICON_SRC" && -f "$ICON_SRC" ]]; then
  ICONSET="${STAGE}/AppIcon.iconset"
  mkdir -p "$ICONSET"
  # Prefer a PNG; sips can often read .ico too on recent macOS.
  BASE="${STAGE}/icon-base.png"
  if [[ "$ICON_SRC" == *.png ]]; then
    cp "$ICON_SRC" "$BASE"
  else
    sips -s format png "$ICON_SRC" --out "$BASE" >/dev/null 2>&1 || cp "$ICON_SRC" "$BASE"
  fi

  for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$BASE" --out "${ICONSET}/icon_${size}x${size}.png" >/dev/null
    dbl=$((size * 2))
    sips -z "$dbl" "$dbl" "$BASE" --out "${ICONSET}/icon_${size}x${size}@2x.png" >/dev/null
  done

  if iconutil -c icns "$ICONSET" -o "${RES_DIR}/AppIcon.icns" 2>/dev/null; then
    echo "Embedded AppIcon.icns"
  else
    echo "Warning: iconutil failed — DMG will use default icon"
    # Avoid broken CFBundleIconFile reference.
    /usr/libexec/PlistBuddy -c "Delete :CFBundleIconFile" "${APP_DIR}/Contents/Info.plist" 2>/dev/null || true
  fi
else
  /usr/libexec/PlistBuddy -c "Delete :CFBundleIconFile" "${APP_DIR}/Contents/Info.plist" 2>/dev/null || true
fi

ln -s /Applications "${DMG_ROOT}/Applications"

mkdir -p "$(dirname "$OUT_DMG")"
rm -f "$OUT_DMG"
hdiutil create \
  -volname "AmphetamineNet" \
  -srcfolder "$DMG_ROOT" \
  -ov \
  -format UDZO \
  "$OUT_DMG"

echo "Created $OUT_DMG"
ls -lh "$OUT_DMG"
