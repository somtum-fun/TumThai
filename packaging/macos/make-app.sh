#!/usr/bin/env bash
# TumThai: wraps a `dotnet publish -r osx-arm64|osx-x64 --self-contained` output into TumThai.app
# (Contents/MacOS = the publish output, Contents/Info.plist, Contents/Resources/lazer.icns), like ppy's osu-deploy.
# Works on Linux and macOS. There's no Apple developer key, so afterwards ad-hoc sign the whole bundle: Apple Silicon
# won't load unsigned code, and only the .NET apphost and runtime come signed (not BASS, ffmpeg or veldrid-spirv).
# On macOS: `codesign --force --deep --sign - TumThai.app`; on Linux: `rcodesign sign TumThai.app` (apple-codesign).
#
# Usage: make-app.sh <publish dir> <output dir> <version> [name] [bundle id]   ->   <output dir>/<name>.app
# (name defaults to TumThai, bundle id to fun.somtum.tumthai; dev and Next builds pass their own so they install beside it)
set -euo pipefail
PUBLISH="${1:?publish dir}"
OUTDIR="${2:?output dir}"
VERSION="${3:?version}"
NAME="${4:-TumThai}"
BUNDLE_ID="${5:-fun.somtum.tumthai}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP="$OUTDIR/$NAME.app"

[[ -x "$PUBLISH/tumthai" ]] || { echo "error: $PUBLISH/tumthai not found (publish osu.Desktop for osx-*)" >&2; exit 1; }

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -a "$PUBLISH/." "$APP/Contents/MacOS/"
sed -e "s/@VERSION@/${VERSION%%-*}/g" -e "s/@NAME@/$NAME/g" -e "s/@BUNDLEID@/$BUNDLE_ID/g" "$HERE/Info.plist" > "$APP/Contents/Info.plist"
cp "$HERE/lazer.icns" "$APP/Contents/Resources/lazer.icns"
printf 'APPL????' > "$APP/Contents/PkgInfo"
chmod +x "$APP/Contents/MacOS/tumthai"
echo "$APP"
