#!/bin/bash
# Builds Kye for Linux, Windows, macOS, Android and Web with Unity, then packages them,
# plus a zip of the source code and build tools.
#
# Usage: ./build.sh [platform ...]     platforms: linux windows mac android web source (default: all)
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
#   Kye-<version>-web.zip    Web (the WebGL build files at the zip root)
#   Kye-<version>-source.zip Source code and build tools, inside a Kye-<version>-source/ folder. Built from git's
#                            file list (tracked + untracked, minus .gitignore) so it includes uncommitted changes.
#                            Also left out: generated/binary build inputs (see the source case below).
#                            Needs no Unity build; runs last so it matches the builds made in the same run.
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

ALL_PLATFORMS=(linux windows mac android web source)
PLATFORMS=("$@")
[ ${#PLATFORMS[@]} -gt 0 ] || PLATFORMS=("${ALL_PLATFORMS[@]}")

# Unity is only needed when something other than the source zip is built.
if [ "${PLATFORMS[*]}" != "source" ]; then
  [ -x "$UNITY" ] || { echo "Unity not found at $UNITY (set UNITY=...)" >&2; exit 1; }
fi
[ -n "$VERSION" ] || { echo "Could not read bundleVersion" >&2; exit 1; }

echo "Building $NAME with $UNITY"
if [ ${#PLATFORMS[@]} -eq ${#ALL_PLATFORMS[@]} ]; then
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
      # The installer's settings.ini must name the version being installed (the Launcher compares it with the
      # update manifest). It uses major.minor.build, so drop any trailing letter such as the "a" in 3.1.0014a.
      sed -i "s/^InstalledVersion=.*/InstalledVersion=${VERSION%%[!0-9.]*}/" "$ROOT/Stuff/Launcher/nsis/settings.ini"
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
      # PortMaster: the unedited APK is the game. unityloader reads the game data from
      # it and extracts the native libraries on first run, so only what the APK lacks goes next to it.
      PM=$ROOT/Stuff/portmaster/unityloader
      GD=$PM/kye/gamedata
      rm -rf "$GD/assets" "$GD/lib"      # nothing extracted from older builds may be left behind
      mkdir -p "$GD/lib/arm64-v8a"
      cp "$PKG/$NAME.apk" "$GD/base.apk"
      # libc++_shared.so isn't in a Unity APK: take it from this Unity editor's NDK (stripped to save space)
      NDK=$(dirname "$UNITY")/Data/PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/linux-x86_64
      [ -f "$NDK/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so" ] \
        || { echo "libc++_shared.so not found in the Unity NDK ($NDK)" >&2; exit 1; }
      cp "$NDK/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so" "$GD/lib/arm64-v8a/"
      "$NDK/bin/llvm-strip" --strip-unneeded "$GD/lib/arm64-v8a/libc++_shared.so"
      # Unity reads time zones from an Android-format tzdata file: build it from this machine's zoneinfo
      python3 "$ROOT/Stuff/portmaster/make_android_tzdata.py" "$GD/usr/share/zoneinfo/tzdata"
      rm -f "$PKG/kye-portmaster.zip"
      # -n .apk: store the APK as it is (it's already compressed)
      (cd "$PM" && zip -qry -n .apk "$PKG/kye-portmaster.zip" Kye.sh kye)
      ;;
    web)
      unity_build WebGL Web
      # zip the web build contents at the zip root (leave out Unity's debug-only folders)
      rm -f "$PKG/$NAME-web.zip"
      (cd "$EXPORT/Web" && zip -qry "$PKG/$NAME-web.zip" . -x "*_BackUpThisFolder_ButDontShipItWithYourGame*" "*_BurstDebugInformation_DoNotShip*")
      ;;
    source)
      # File list = git's tracked + untracked files minus .gitignore, so uncommitted changes are included.
      # (.gitignore already leaves out Library, Export, *.apk, the generated csproj/sln, etc.)
      # Then drop what the build regenerates or what is a large binary input. libc++_shared.so is
      # .gitignored but is wanted, so it is added back. Third-party folders and unityloader stay in.
      SRC=$(mktemp -d); list=$SRC/files; GD=Stuff/portmaster/unityloader/kye/gamedata
      git ls-files -z --cached --others --exclude-standard | sort -zu \
        | grep -zvE "^(Export/|Stuff/appimage/AppDir/usr/bin/|Stuff/Launcher/nsis/app/|$GD/(lib/|assets/))|(^|/)appimagetool-x86_64\.AppImage$|\.apk$" > "$list.raw"
      [ -f "$GD/lib/arm64-v8a/libc++_shared.so" ] && printf '%s\0' "$GD/lib/arm64-v8a/libc++_shared.so" >> "$list.raw"
      # git still lists files deleted in the working tree: keep only what exists, under the wrapper folder
      while IFS= read -r -d '' f; do
        { [ -e "$f" ] || [ -L "$f" ]; } && printf '%s\0' "$NAME-source/$f"
      done < "$list.raw" > "$list"
      ln -s "$ROOT" "$SRC/$NAME-source"   # lets zip store every path under the wrapper folder
      rm -f "$PKG/$NAME-source.zip"
      (cd "$SRC" && zip -qy "$PKG/$NAME-source.zip" -@ < <(tr '\0' '\n' < "$list"))
      rm -rf "$SRC"
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
