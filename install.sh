#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [ -z "${KSPDIR:-}" ]; then
    echo "ERROR: KSPDIR is not set (path to your Kerbal Space Program install)"
    exit 1
fi

if [ ! -f "Release/EvaCMGroundMod.zip" ]; then
    echo "ERROR: Release/EvaCMGroundMod.zip not found — run ./build.sh first"
    exit 1
fi

echo "====================================="
echo "Removing existing Mod folder"
echo "====================================="
rm -rf "$KSPDIR/GameData/EvaCMGround"

echo
echo "====================================="
echo "Unzipping Mod"
echo "====================================="
mkdir -p "$KSPDIR/GameData/EvaCMGround"
unzip -o "Release/EvaCMGroundMod.zip" -d "$KSPDIR/GameData/EvaCMGround"

echo
echo "Mod installed"
