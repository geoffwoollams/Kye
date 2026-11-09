#!/bin/bash
# Builds Kye for Linux, Windows, macOS, Android and Web with Unity, then packages them.
#
# Usage: ./build.sh [platform ...]     platforms: linux windows mac android web (default: all)
# Env:   UNITY=/path/to/Unity          override the Unity editor binary
#
# Everything for a version goes in Export/<version>/ (other versions are left alone):
#   Linux/ Windows/ Mac/ Android/ Web/   the Unity builds, with their debug info
#   logs/                                Unity build logs
#   Packages/                            the release packages:
#   Kye-<version>.tar.gz     Linux
#   Kye-<version>.AppImage   Linux, built from Stuff/appimage
#   Kye-<version>.zip        Windows (game files at the zip root, as the Launcher auto-updater expects)
#   Kye-Setup.exe            Windows installer, built from Stuff/Launcher
#   Kye-<version>-macOS.zip  macOS (contains Kye.app)
#   Kye-<version>.apk        Android
#   kye-portmaster.zip       PortMaster port (Kye.sh + kye/), built from Stuff/portmaster/unityloader
# The Web build is not packaged.
#
# Unity's "*_BackUpThisFolder_ButDontShipItWithYourGame" and "*_BurstDebugInformation_DoNotShip"
# folders are left in the platform folders but are excluded from every package and the installer.
set -euo pipefail

cd "$(dirname "$0")"
ROOT=$PWD
UNITY=${UNITY:-$HOME/Unity/Hub/Editor/$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)/Editor/Unity}
VERSION=$(sed -n 's/^  bundleVersion: //p' ProjectSettings/ProjectSettings.asset | head -n1 | tr -d '\r')
NAME="Kye-$VERSION"
EXPORT=$ROOT/Export/$VERSION   # everything for this version; other versions are never touched
PKG=$EXPORT/Packages
LOGS=$EXPORT/logs
EXCLUDES=('*_BackUpThisFolder_ButDontShipItWithYourGame' '*_BurstDebugInformation_DoNotShip')

[ -x "$UNITY" ] || { echo "Unity not found at $UNITY (set UNITY=...)" >&2; exit 1; }
[ -n "$VERSION" ] || { echo "Could not read bundleVersion" >&2; exit 1; }

PLATFORMS=("$@")
[ ${#PLATFORMS[@]} -gt 0 ] || PLATFORMS=(linux windows mac android web)

echo "Building $NAME with $UNITY"
if [ ${#PLATFORMS[@]} -eq 5 ]; then
  # Full build: clear this version's export folder. Partial builds only clear their own folders.
  rm -rf "$EXPORT"
fi
mkdir -p "$LOGS" "$PKG"

# Timing: per platform, the Unity build time and the packaging time are recorded.
TIMES=()          # "platform|unity seconds|total seconds"
UNITY_SECS=0
fmt() { printf '%dm%02ds' $(($1 / 60)) $(($1 % 60)); }

# unity_build <build target> <export folder> [extra args]
unity_build() {
  local target=$1 dir=$2; shift 2
  rm -rf "$EXPORT/$dir"
  local t0=$SECONDS
  echo "=== Unity: $target -> Export/$VERSION/$dir"
  "$UNITY" -batchmode -nographics -quit -projectPath "$ROOT" \
    -buildTarget "$target" -executeMethod BuildScript.Build \
    -exportPath "$EXPORT/$dir" -logFile "$LOGS/$dir.log" "$@" \
    || { echo "Unity build failed for $target, see $LOGS/$dir.log" >&2; exit 1; }
  UNITY_SECS=$((SECONDS - t0))
}

# Copies a folder's contents to a destination, leaving out Unity's non-shipping folders.
copy_clean() {
  local src=$1 dest=$2 ex=() e
  for e in "${EXCLUDES[@]}"; do ex+=(--exclude="$e"); done
  mkdir -p "$dest"
  tar -C "$src" "${ex[@]}" -cf - . | tar -C "$dest" -xf -
}

START=$SECONDS
for p in "${PLATFORMS[@]}"; do
  P0=$SECONDS; UNITY_SECS=0
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
      cp "$EXPORT/Android/$NAME.apk" "$PKG/"
      # PortMaster: unpack the apk's lib/ and assets/bin/Data/ into the unityloader port, then zip it
      PM=$ROOT/Stuff/portmaster/unityloader
      sed -i "s/^versionName=.*/versionName=\"$VERSION\"/" "$PM/kye/kye.toml"
      APK=$(mktemp -d); unzip -q "$PKG/$NAME.apk" 'lib/*' 'assets/bin/Data/*' -d "$APK"
      mkdir -p "$PM/kye/gamedata/lib" "$PM/kye/gamedata/assets/bin"
      cp -a "$APK/lib/." "$PM/kye/gamedata/lib/"
      rm -rf "$PM/kye/gamedata/assets/bin/Data"
      cp -a "$APK/assets/bin/Data" "$PM/kye/gamedata/assets/bin/Data"
      rm -rf "$APK" "$PKG/kye-portmaster.zip"
      (cd "$PM" && zip -qry "$PKG/kye-portmaster.zip" Kye.sh kye)
      ;;
    web)
      unity_build WebGL Web
      ;;
    *) echo "Unknown platform: $p" >&2; exit 1 ;;
  esac
  TIMES+=("$p|$UNITY_SECS|$((SECONDS - P0))")
  echo "--- $p finished in $(fmt $((SECONDS - P0)))"
done

echo
echo "===== Build stats: $NAME ====="
printf '%-10s %10s %10s %10s\n' Platform Unity Packaging Total
for t in "${TIMES[@]}"; do
  IFS='|' read -r plat u tot <<< "$t"
  printf '%-10s %10s %10s %10s\n' "$plat" "$(fmt "$u")" "$(fmt $((tot - u)))" "$(fmt "$tot")"
done
printf '%-10s %32s\n' Total "$(fmt $((SECONDS - START)))"
echo
echo "Packages in $PKG:"
find "$PKG" -type f -printf '%s %f\n' | sort -k2 | numfmt --field=1 --to=iec | awk '{printf "  %8s  %s\n", $1, $2}'
echo "Unity logs: $LOGS"
