#!/bin/bash
# Build an Apple Silicon + Intel app using the installed macOS SDK. No downloads.
set -euo pipefail
if [[ "$(uname -s)" != Darwin ]]; then
  echo 'Build Gravity for macOS on a Mac with Xcode Command Line Tools installed.' >&2
  exit 1
fi
test_app=false
package=false
snapshots=false
for argument in "$@"; do
  case "$argument" in
    --test) test_app=true ;;
    --package) package=true ;;
    --snapshots) snapshots=true; test_app=true ;;
    *) echo "Usage: bash build-macos.sh [--test] [--package] [--snapshots]" >&2; exit 2 ;;
  esac
done
root="$(cd -- "$(dirname -- "$0")" && pwd)"
app="$root/bin/Gravity.app"
mkdir -p "$root/bin"
work="$(mktemp -d "$root/bin/macos-build.XXXXXX")"
trap 'rm -rf -- "$work"' EXIT
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
sdk="$(xcrun --sdk macosx --show-sdk-path)"
for arch in arm64 x86_64; do
  xcrun swiftc -swift-version 5 -parse-as-library -O -warnings-as-errors \
    -module-name Gravity -sdk "$sdk" -target "$arch-apple-macosx12.0" \
    -framework AppKit -framework PDFKit -framework UniformTypeIdentifiers \
    "$root"/macos/*.swift -o "$work/Gravity-$arch"
done
lipo -create "$work/Gravity-arm64" "$work/Gravity-x86_64" -output "$app/Contents/MacOS/Gravity"
cp "$root/macos/Info.plist" "$app/Contents/Info.plist"
cp "$root/LICENSE" "$app/Contents/Resources/LICENSE"
cp "$root/THIRD_PARTY_NOTICES.md" "$app/Contents/Resources/THIRD_PARTY_NOTICES.md"
cp "$root/assets/g-mark.png" "$app/Contents/Resources/g-mark.png"
icons="$work/Gravity.iconset"
mkdir -p "$icons"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" "$root/assets/gravity-icon-1024.png" --out "$icons/icon_${size}x${size}.png" >/dev/null
  double=$((size * 2))
  sips -z "$double" "$double" "$root/assets/gravity-icon-1024.png" --out "$icons/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$icons" -o "$app/Contents/Resources/Gravity.icns"
plutil -lint "$app/Contents/Info.plist"
codesign --force --sign - --timestamp=none "$app"
codesign --verify --strict "$app"
if "$test_app"; then
  test_args=(--self-test)
  if "$snapshots"; then test_args+=(--ui-snapshots "$root/output/mac-ui"); fi
  "$app/Contents/MacOS/Gravity" "${test_args[@]}"
fi
if "$package"; then
  mkdir -p "$root/dist"
  ditto -c -k --keepParent "$app" "$root/dist/Gravity-1.0.0-mac-universal.zip"
fi
echo 'Built bin/Gravity.app (Apple Silicon and Intel).'
