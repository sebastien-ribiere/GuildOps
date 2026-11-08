#!/bin/bash
# Construit les distributions Linux et Windows (export Godot .NET) dans dist/ : archives, README utilisateur, licences, inventaire, SHA256SUMS.
# Chaque étape propage son code de retour (set -e) ; l'export Windows est GÉNÉRÉ ici mais ne peut être EXÉCUTÉ que sous Windows.
#
# usage : tools/package.sh <chemin-godot-mono> [étiquette]        (prérequis : modèles d'export Godot 4.7.2 mono installés, dotnet 10)
set -euo pipefail
GODOT="$(realpath "$1")"
LABEL="${2:-B5.3}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GAME="$ROOT/src/GuildOps.Game"
DIST="$ROOT/dist"
BUILD="$DIST/build"          # exports bruts ; build/ (exports antérieurs éventuels) n'est jamais touché
STAGE="$DIST/stage"

rm -rf "$DIST"
mkdir -p "$BUILD/linux" "$BUILD/windows" "$STAGE"

echo "== Compilation Release =="
dotnet build "$GAME" -c Release -v q --nologo
echo "== Import Godot (cache propre) =="
rm -rf "$GAME/.godot"
"$GODOT" --headless --path "$GAME" --import
echo "== Export Linux x64 =="
"$GODOT" --headless --path "$GAME" --export-release "Linux x64" "$BUILD/linux/GuildOps.x86_64"
echo "== Export Windows x64 (généré depuis Linux : non exécutable ici) =="
"$GODOT" --headless --path "$GAME" --export-release "Windows x64" "$BUILD/windows/GuildOps.exe"

echo "== Licences =="
mkdir -p "$STAGE/LICENSES"
"$GODOT" --headless --path "$GAME" --script ../../tools/dump-godot-licenses.gd -- --out="$STAGE/LICENSES/THIRD_PARTY_NOTICES.txt"
cp "$GAME/Assets/fonts/OFL-PixeloidSans.txt" "$STAGE/LICENSES/OFL-PixeloidSans.txt"
DOTNET_ROOT_DIR="$(dirname "$(readlink -f "$(command -v dotnet)")")"
cp "$DOTNET_ROOT_DIR/LICENSE.txt" "$STAGE/LICENSES/DOTNET_LICENSE.txt"
cp "$DOTNET_ROOT_DIR/ThirdPartyNotices.txt" "$STAGE/LICENSES/DOTNET_ThirdPartyNotices.txt"

assemble() { # <plateforme> <dossier build> <nom archive>
  local platform="$1" src="$2" name="$3" dest="$DIST/$3"
  mkdir -p "$dest"
  cp -a "$src"/. "$dest"/
  cp -a "$STAGE/LICENSES" "$dest/LICENSES"
  cp "$ROOT/packaging/README_UTILISATEUR.md" "$dest/README_UTILISATEUR.md"
  cp "$GAME/Assets/b5_3/branding/app_icon_256.png" "$dest/GuildOps.png"
  python3 "$ROOT/tools/make-inventory.py" "$dest" "$platform" "$dest/INVENTAIRE_RESSOURCES_ET_LICENCES.md"
}
assemble "Linux x86_64" "$BUILD/linux" "GuildOps-$LABEL-linux-x86_64"
assemble "Windows x86_64" "$BUILD/windows" "GuildOps-$LABEL-windows-x86_64"

echo "== Archives =="
(cd "$DIST" && tar czf "GuildOps-$LABEL-linux-x86_64.tar.gz" "GuildOps-$LABEL-linux-x86_64" \
            && zip -qr "GuildOps-$LABEL-windows-x86_64.zip" "GuildOps-$LABEL-windows-x86_64" \
            && sha256sum "GuildOps-$LABEL-linux-x86_64.tar.gz" "GuildOps-$LABEL-windows-x86_64.zip" > SHA256SUMS)
rm -rf "$STAGE" "$BUILD"
ls -la "$DIST"
cat "$DIST/SHA256SUMS"
