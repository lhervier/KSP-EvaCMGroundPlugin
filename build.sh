#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [ -z "${KSPDIR:-}" ]; then
    echo "ERROR: KSPDIR is not set (path to your Kerbal Space Program install)"
    exit 1
fi

if [ ! -d "$KSPDIR/KSP_x64_Data/Managed" ]; then
    echo "ERROR: KSP managed assemblies not found at: $KSPDIR/KSP_x64_Data/Managed"
    exit 1
fi

echo "==============================="
echo "Building EvaCMGroundMod"
echo "==============================="

echo "Removing Release folder"
rm -rf Release

echo "Creating Release folder"
mkdir -p Release/EvaCMGroundMod

echo "Building Mod DLL"
dotnet build
if [ ! -f "Output/bin/EvaCMGroundMod.dll" ]; then
    echo "ERROR: EvaCMGroundMod.dll was not produced"
    exit 1
fi

echo "Copying Mod dll files"
cp -f "Output/bin/EvaCMGroundMod.dll" "Release/EvaCMGroundMod/"

echo "Copying Config file"
cp -f "eva_cm_ground.cfg" "Release/EvaCMGroundMod/"

echo "Zipping Mod"
rm -f "Release/EvaCMGroundMod.zip"
(cd Release/EvaCMGroundMod && zip -r ../EvaCMGroundMod.zip .)

echo "Removing Mod folder"
rm -rf Release/EvaCMGroundMod

echo "Build Complete"
