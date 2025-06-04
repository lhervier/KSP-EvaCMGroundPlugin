@echo off
setlocal

echo ===============================
echo Building EvaCMGroundMod
echo ===============================

echo Removing Release folder
rmdir /s /q Release
if errorlevel 1 (
    echo ERROR: Failed to remove the Release folder
    exit /b 1
)

echo Creating Release folder
mkdir Release
if errorlevel 1 (
    echo ERROR: Failed to create the Release folder
    exit /b 1
)
mkdir Release\EvaCMGroundMod
if errorlevel 1 (
    echo ERROR: Failed to create the Mod folder
    exit /b 1
)

echo Building Mod DLL
dotnet build
if errorlevel 1 (
    echo ERROR: Failed to build the Mod DLL
    exit /b 1
)

echo Copying Mod dll files
copy /y "Output\bin\EvaCMGroundMod.dll" "Release\EvaCMGroundMod"
if errorlevel 1 (
    echo ERROR: Failed to copy the Mod DLL
    exit /b 1
)

echo Copying Config file
copy /y "eva_cm_ground.cfg" "Release\EvaCMGroundMod"
if errorlevel 1 (
    echo ERROR: Failed to copy the Config file
    exit /b 1
)

echo Zipping Mod
powershell -Command "Compress-Archive -Path 'Release\EvaCMGroundMod\*' -DestinationPath 'Release\EvaCMGroundMod.zip' -Force"
if errorlevel 1 (
    echo ERROR: Failed to zip the Mod
    exit /b 1
)

echo Removing Mod folder
rmdir /s /q Release\EvaCMGroundMod
if errorlevel 1 (
    echo ERROR: Failed to remove the Mod folder
    exit /b 1
)


echo Build Complete
