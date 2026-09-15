#!/bin/bash
# Launcher for the World-Altering Editor (Linux/DesktopGL build of the stride-2048 fork).
# Usage:  ./wae.sh [path/to/map.map]
# Optional map argument opens that map directly.
DIR="$(dirname "$(readlink -f "$0")")/src/TSMapEditor/bin/Debug/net8.0"

if [ ! -f "$DIR/WorldAlteringEditor.dll" ]; then
    echo "Build not found at $DIR"
    echo "Build it first:  dotnet build src/TSMapEditor/TSMapEditor.csproj -c Debug"
    echo "(shader compilation needs: export MGFXC_WINE_PATH=\$HOME/.winemonogame and wine64 on PATH)"
    exit 1
fi

cd "$DIR" && exec dotnet WorldAlteringEditor.dll "$@"
