#!/usr/bin/env bash
#
# build-osu-appimage.sh
#
# Builds osu!lazer from source for Linux (x64) and packages it as an AppImage.
#
# Requirements (the script checks for these):
#   - git
#   - .NET SDK 8.0+      (https://dotnet.microsoft.com/download)
#   - curl
#   - FUSE (libfuse2) for running the resulting AppImage
#
# Usage:
#   ./build-osu-appimage.sh [options]
#
# By default this builds the LOCAL checkout at:
#   /var/mnt/hddsata/Development/osu-tumthai
# Use --clone to fetch ppy/osu from GitHub instead.
#
# Options:
#   -s, --source  <dir>       Local osu! checkout to build (default: osu-tumthai)
#   -r, --ref     <git-ref>   Tag/branch/commit to build (only with --clone)
#       --clone               Clone ppy/osu from GitHub instead of using --source
#   -o, --outdir  <dir>       Where the .AppImage is written (default: ./dist)
#   -w, --workdir <dir>       Build working directory (default: ./.osu-build)
#       --rid     <rid>       .NET runtime identifier (default: linux-x64)
#       --keep                Keep the working directory after a successful build
#   -h, --help                Show this help
#
set -euo pipefail

# ----------------------------------------------------------------------------
# Defaults
# ----------------------------------------------------------------------------
REPO_URL="https://github.com/ppy/osu.git"
SOURCE="/var/mnt/hddsata/Development/osu-tumthai"   # local checkout to build
GIT_REF=""                       # empty => resolve from local repo / latest tag
OUTDIR="$(pwd)/dist"
WORKDIR="$(pwd)/.osu-build"
RID="linux-x64"
KEEP_WORKDIR=0
DO_CLONE=0                        # default: build the local SOURCE, do not clone

APPDIR=""                        # set later
PROJECT="osu.Desktop/osu.Desktop.csproj"

# ----------------------------------------------------------------------------
# Helpers
# ----------------------------------------------------------------------------
log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33mwarning:\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31merror:\033[0m %s\n' "$*" >&2; exit 1; }

usage() {
  sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

need() {
  command -v "$1" >/dev/null 2>&1 || die "required tool '$1' not found in PATH"
}

# ----------------------------------------------------------------------------
# Parse arguments
# ----------------------------------------------------------------------------
while [[ $# -gt 0 ]]; do
  case "$1" in
    -s|--source)  SOURCE="${2:?}";  DO_CLONE=0; shift 2 ;;
    -r|--ref)     GIT_REF="${2:?}"; shift 2 ;;
    --clone)      DO_CLONE=1;       shift ;;
    -o|--outdir)  OUTDIR="${2:?}";  shift 2 ;;
    -w|--workdir) WORKDIR="${2:?}"; shift 2 ;;
    --rid)        RID="${2:?}";     shift 2 ;;
    --keep)       KEEP_WORKDIR=1;   shift ;;
    -h|--help)    usage ;;
    *) die "unknown option: $1 (use --help)" ;;
  esac
done

OUTDIR="$(mkdir -p "$OUTDIR" && cd "$OUTDIR" && pwd)"
mkdir -p "$WORKDIR"
WORKDIR="$(cd "$WORKDIR" && pwd)"
PUBLISHDIR="$WORKDIR/publish"
APPDIR="$WORKDIR/osu.AppDir"

# ----------------------------------------------------------------------------
# Check prerequisites
# ----------------------------------------------------------------------------
log "Checking prerequisites"
need git
need curl
need dotnet

DOTNET_MAJOR="$(dotnet --version | cut -d. -f1)"
if [[ "${DOTNET_MAJOR:-0}" -lt 8 ]]; then
  die ".NET SDK 8.0+ required, found $(dotnet --version)"
fi
log ".NET SDK $(dotnet --version) detected"

# ----------------------------------------------------------------------------
# Resolve source (local checkout by default, or clone ppy/osu)
# ----------------------------------------------------------------------------
if [[ "$DO_CLONE" -eq 1 ]]; then
  SRCDIR="$WORKDIR/osu"

  if [[ -z "$GIT_REF" ]]; then
    log "Resolving latest osu!lazer release tag"
    # Tags are date-based (e.g. 2024.1224.0); the highest sorted tag is newest.
    GIT_REF="$(git ls-remote --tags --refs "$REPO_URL" \
                | awk -F/ '{print $NF}' \
                | grep -E '^[0-9]{4}\.[0-9]+\.[0-9]+$' \
                | sort -V \
                | tail -n1)"
    [[ -n "$GIT_REF" ]] || die "could not resolve latest release tag"
  fi

  log "Cloning $REPO_URL (ref: $GIT_REF)"
  rm -rf "$SRCDIR"
  git clone --depth 1 --branch "$GIT_REF" "$REPO_URL" "$SRCDIR" 2>/dev/null \
    || {
      # Fallback: ref is a commit, not a tag/branch
      warn "shallow clone of '$GIT_REF' failed, doing full clone"
      git clone "$REPO_URL" "$SRCDIR"
      git -C "$SRCDIR" checkout "$GIT_REF"
    }
else
  SRCDIR="$SOURCE"
  [[ -d "$SRCDIR" ]] || die "local source not found: $SRCDIR (use --source or --clone)"
  [[ -f "$SRCDIR/$PROJECT" ]] \
    || die "$SRCDIR does not look like an osu! checkout (missing $PROJECT)"
  log "Building local checkout at $SRCDIR"

  # Derive a version label from the local repo for the output filename.
  if [[ -z "$GIT_REF" ]]; then
    if [[ -d "$SRCDIR/.git" ]] && command -v git >/dev/null 2>&1; then
      GIT_REF="$(git -C "$SRCDIR" describe --tags --always --dirty 2>/dev/null || true)"
    fi
    [[ -n "$GIT_REF" ]] || GIT_REF="local"
  fi
fi
log "Version label: $GIT_REF"

# ----------------------------------------------------------------------------
# Publish a self-contained build
# ----------------------------------------------------------------------------
log "Publishing osu.Desktop ($RID, self-contained)"
rm -rf "$PUBLISHDIR"
dotnet publish "$SRCDIR/$PROJECT" \
  --configuration Release \
  --runtime "$RID" \
  --self-contained true \
  --framework net8.0 \
  -p:PublishSingleFile=false \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  --output "$PUBLISHDIR"

[[ -f "$PUBLISHDIR/osu!" ]] || die "expected launcher '$PUBLISHDIR/osu!' not found after publish"

# ----------------------------------------------------------------------------
# Assemble the AppDir
# ----------------------------------------------------------------------------
log "Assembling AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin" "$APPDIR/usr/share/applications" \
         "$APPDIR/usr/share/icons/hicolor/256x256/apps"

cp -a "$PUBLISHDIR/." "$APPDIR/usr/bin/"

# Icon: try to extract one from the source tree, otherwise generate a stub.
ICON_SRC="$(find "$SRCDIR" -type f -iname 'lazer.png' 2>/dev/null | head -n1 || true)"
ICON_DST="$APPDIR/usr/share/icons/hicolor/256x256/apps/osu.png"
if [[ -n "$ICON_SRC" ]]; then
  cp "$ICON_SRC" "$ICON_DST"
else
  warn "no icon found in source tree, using a placeholder"
  # 1x1 transparent PNG placeholder
  printf '\x89PNG\r\n\x1a\n\x00\x00\x00\rIHDR\x00\x00\x00\x01\x00\x00\x00\x01\x08\x06\x00\x00\x00\x1f\x15\xc4\x89\x00\x00\x00\nIDATx\x9cc\x00\x01\x00\x00\x05\x00\x01\r\n-\xb4\x00\x00\x00\x00IEND\xaeB`\x82' > "$ICON_DST"
fi
cp "$ICON_DST" "$APPDIR/osu.png"

# Desktop entry
cat > "$APPDIR/usr/share/applications/osu.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=osu!
Comment=osu! - rhythm is just a *click* away
Exec=osu!
Icon=osu
Categories=Game;
Terminal=false
StartupWMClass=osu!
MimeType=application/x-osu-beatmap-archive;application/x-osu-skin-archive;x-scheme-handler/osu;
EOF
cp "$APPDIR/usr/share/applications/osu.desktop" "$APPDIR/osu.desktop"

# AppRun entry point
cat > "$APPDIR/AppRun" <<'EOF'
#!/usr/bin/env bash
HERE="$(dirname "$(readlink -f "${0}")")"
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/bin:${LD_LIBRARY_PATH:-}"
exec "${HERE}/usr/bin/osu!" "$@"
EOF
chmod +x "$APPDIR/AppRun"

# ----------------------------------------------------------------------------
# Fetch appimagetool and build the AppImage
# ----------------------------------------------------------------------------
log "Fetching appimagetool"
case "$RID" in
  linux-x64)   AIT_ARCH="x86_64"  ;;
  linux-arm64) AIT_ARCH="aarch64" ;;
  *) die "unsupported RID for AppImage packaging: $RID" ;;
esac

AIT="$WORKDIR/appimagetool-$AIT_ARCH.AppImage"
if [[ ! -x "$AIT" ]]; then
  curl -fL --retry 3 -o "$AIT" \
    "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-${AIT_ARCH}.AppImage"
  chmod +x "$AIT"
fi

OUTFILE="$OUTDIR/osu-lazer-${GIT_REF}-${AIT_ARCH}.AppImage"
log "Building AppImage -> $OUTFILE"

# appimagetool needs FUSE; --appimage-extract-and-run avoids that requirement.
ARCH="$AIT_ARCH" "$AIT" --appimage-extract-and-run "$APPDIR" "$OUTFILE" \
  || die "appimagetool failed (is FUSE/libfuse2 available?)"

chmod +x "$OUTFILE"

# ----------------------------------------------------------------------------
# Cleanup
# ----------------------------------------------------------------------------
if [[ "$KEEP_WORKDIR" -eq 0 ]]; then
  log "Cleaning working directory"
  # Never delete a local --source checkout; only remove a clone we created.
  if [[ "$DO_CLONE" -eq 1 ]]; then
    rm -rf "$SRCDIR"
  fi
  rm -rf "$PUBLISHDIR" "$APPDIR"
fi

log "Done."
printf '\nAppImage: %s\n' "$OUTFILE"
printf 'Run it with:\n  %s\n' "$OUTFILE"
