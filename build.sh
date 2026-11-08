#!/bin/bash
# Builds Kye for Linux, Windows, macOS, Android and Web with Unity, then packages them.
#
# Usage: ./build.sh [platform ...]     platforms: linux windows mac android web (default: all)
# Env:   UNITY=/path/to/Unity          override the Unity editor binary
#
# Packages (all in Export/Packages/):
#   Kye-<version>.tar.gz     Linux
#   Kye-<version>.AppImage   Linux, built from Stuff/appimage
#   Kye-<version>.zip        Windows (game files at the zip root, as the Launcher auto-updater expects)
#   Kye-Setup.exe            Windows installer, built from Stuff/Launcher
#   Kye-<version>-macOS.zip  macOS (contains Kye.app)
#   Kye-<version>.apk        Android
# Export/Web/ holds the WebGL build (not packaged).
#
# Unity's "*_BackUpThisFolder_ButDontShipItWithYourGame" and "*_BurstDebugInformation_DoNotShip"
# folders are left in the platform folders but are excluded from every package and the installer.
set -euo pipefail

cd "$(dirname "$0")"
ROOT=$PWD
UNITY=${UNITY:-$HOME/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Editor/Unity}
VERSION=$(sed -n 's/^  bundleVersion: //p' ProjectSettings/ProjectSettings.asset | head -n1 | tr -d '\r')
NAME="Kye-$VERSION"
EXPORT=$ROOT/Export
PKG=$EXPORT/Packages
LOGS=$EXPORT/logs
EXCLUDES=('*_BackUpThisFolder_ButDontShipItWithYourGame' '*_BurstDebugInformation_DoNotShip')

[ -x "$UNITY" ] || { echo "Unity not found at $UNITY (set UNITY=...)" >&2; exit 1; }
[ -n "$VERSION" ] || { echo "Could not read bundleVersion" >&2; exit 1; }

PLATFORMS=("$@")
[ ${#PLATFORMS[@]} -gt 0 ] || PLATFORMS=(linux windows mac android web)

echo "Building $NAME with $UNITY"
if [ ${#PLATFORMS[@]} -eq 5 ]; then
  # Full build: clear the whole export folder. Partial builds only clear their own folders.
  rm -rf "$EXPORT"
fi
mkdir -p "$LOGS" "$PKG"

# unity_build <build target> <export folder> [extra args]
unity_build() {
  local target=$1 dir=$2; shift 2
  rm -rf "$EXPORT/$dir"
  echo "=== Unity: $target -> Export/$dir"
  "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
    -buildTarget "$target" -executeMethod BuildScript.Build \
    -exportPath "$EXPORT/$dir" -logFile "$LOGS/$dir.log" "$@" \
    || { echo "Unity build failed for $target, see $LOGS/$dir.log" >&2; exit 1; }
}

# Copies a folder's contents to a destination, leaving out Unity's non-shipping folders.
copy_clean() {
  local src=$1 dest=$2 ex=()
  for p in "${EXCLUDES[@]}"; do ex+=(--exclude="$p"); done
  mkdir -p "$dest"
  tar -C "$src" "${ex[@]}" -cf - . | tar -C "$dest" -xf -
}

for p in "${PLATFORMS[@]}"; do
  case $p in
    linux)
      unity_build Linux64 Linux
      ex=(); for e in "${EXCLUDES[@]}"; do ex+=(--exclude="$e"); done
      tar -C "$EXPORT/Linux" "${ex[@]}" --transform "s,^\./,," -czf "$PKG/$NAME.tar.gz" .
      # AppImage: put the cleaned Linux build in AppDir/usr/bin
      BIN=$ROOT/Stuff/appimage/AppDir/usr/bin
      rm -rf "$BIN"; copy_clean "$EXPORT/Linux" "$BIN"
      (cd "$ROOT/Stuff/appimage" && ARCH=x86_64 ./appimagetool-x86_64.AppImage AppDir "$PKG/$NAME.AppImage")
      ;;
    windows)
      unity_build Win64 Windows
      clean=$(mktemp -d); copy_clean "$EXPORT/Windows" "$clean"
      (cd "$clean" && zip -qr "$PKG/$NAME.zip" .)
      # Launcher installer: replace nsis/app with this build, then build Kye-Setup.exe
      APP=$ROOT/Stuff/Launcher/nsis/app
      rm -rf "$APP"; mkdir -p "$APP"; cp -a "$clean/." "$APP/"
      rm -rf "$clean"
      (cd "$ROOT/Stuff/Launcher" && ./build.sh)
      cp "$ROOT/Stuff/Launcher/nsis/Kye-Setup.exe" "$PKG/"
      ;;
    mac)
      unity_build OSXUniversal Mac
      # zip only Kye.app (ditto-style: keep symlinks with -y)
      (cd "$EXPORT/Mac" && zip -qry "$PKG/$NAME-macOS.zip" Kye.app -x "*_BurstDebugInformation_DoNotShip*")
      ;;
    android)
      unity_build Android Android -apkName "$NAME"
      mv "$EXPORT/Android/$NAME.apk" "$PKG/"
      ;;
    web)
      unity_build WebGL Web
      ;;
    *) echo "Unknown platform: $p" >&2; exit 1 ;;
  esac
done

echo "Done: $NAME"
find "$PKG" -type f -exec ls -lh {} +
