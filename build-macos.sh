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
mkdir -p "$root/bin"
work="$(mktemp -d "$root/bin/macos-build.XXXXXX")"
destination="$root/bin/Gravity.app"
app="$work/Gravity.app"
cleanup() {
  if [[ -e "$work/Previous.app" || -L "$work/Previous.app" ]] && [[ ! -e "$destination" && ! -L "$destination" ]]; then
    mv "$work/Previous.app" "$destination" || return
  fi
  rm -rf -- "$work"
}
trap cleanup EXIT
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
cp "$root/assets/gravity-icon-1024.png" "$app/Contents/Resources/gravity-icon-1024.png"
cp "$root/src/Lockup.png" "$app/Contents/Resources/Lockup.png"
cp "$root/third_party/lucide/icons/save.svg" "$app/Contents/Resources/save.svg"
mkdir -p "$app/Contents/Resources/licenses"
cp "$root/third_party/lucide/LICENSE" "$app/Contents/Resources/licenses/Lucide.txt"
# Bundle both icons: Finder gets the document icon from CFBundleDocumentTypes.
xcrun swiftc -swift-version 5 -O -warnings-as-errors -sdk "$sdk" -framework AppKit \
  "$root/macos/AppIcon.swift" "$root/tools/MakeMacIcon.swift" -o "$work/MakeMacIcon"
for name in Gravity GravityPdf; do
  source="$root/assets/gravity-icon-1024.png"
  if [[ "$name" == GravityPdf ]]; then source="$root/assets/gravity-pdf-512.png"; fi
  icons="$work/$name.iconset"
  mkdir -p "$icons"
  if [[ "$name" == Gravity ]]; then
    "$work/MakeMacIcon" "$source" "$icons"
  else
    for size in 16 32 128 256 512; do
      sips -z "$size" "$size" "$source" --out "$icons/icon_${size}x${size}.png" >/dev/null
      double=$((size * 2))
      sips -z "$double" "$double" "$source" --out "$icons/icon_${size}x${size}@2x.png" >/dev/null
    done
  fi
  iconutil -c icns "$icons" -o "$app/Contents/Resources/$name.icns"
done
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
  ditto -c -k --keepParent "$app" "$work/Gravity-1.0.0-mac-universal.zip"
fi
# Install only the complete, validated bundle. Reusing an old Resources directory
# could carry obsolete or unrelated files into the next app and archive.
if [[ -e "$destination" || -L "$destination" ]]; then mv "$destination" "$work/Previous.app"; fi
mv "$app" "$destination"
if "$package"; then
  mv "$work/Gravity-1.0.0-mac-universal.zip" "$root/dist/Gravity-1.0.0-mac-universal.zip"
fi
echo 'Built bin/Gravity.app (Apple Silicon and Intel).'
