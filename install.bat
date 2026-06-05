@echo off
setlocal

if "%KSPDIR%"=="" goto :no_kspdir

if not exist "Release\EvaCMGroundMod.zip" (
    echo ERROR: Release\EvaCMGroundMod.zip not found - run build.bat first
    exit /b 1
)

echo =====================================
echo Removing existing Mod folder
echo =====================================

if exist "%KSPDIR%\GameData\EvaCMGroundMod" rmdir /s /q "%KSPDIR%\GameData\EvaCMGroundMod"
if errorlevel 1 (
    echo ERROR: Failed to remove the Mod folder
    exit /b 1
)

echo.
echo =====================================
echo Unzipping Mod
echo =====================================

powershell -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -Path 'Release\EvaCMGroundMod.zip' -DestinationPath '%KSPDIR%\GameData\EvaCMGroundMod' -Force"
if errorlevel 1 (
    echo ERROR: Failed to unzip the Mod
    exit /b 1
)

echo.
echo Mod installed
echo.
echo Run at: %date% %time%
exit /b 0

:no_kspdir
echo ERROR: KSPDIR is not set - set it to your Kerbal Space Program install path
exit /b 1
