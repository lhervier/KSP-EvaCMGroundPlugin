@echo off
setlocal

echo ===============================
echo Building EvaCMGroundMod
echo ===============================

if "%KSPDIR%"=="" goto :no_kspdir

set "KSP_DATA_DIR=%KSPDIR%\KSP_x64_Data"
if not exist "%KSP_DATA_DIR%\Managed\Assembly-CSharp.dll" goto :no_managed

echo Using KSPDIR: %KSPDIR%
echo Using KSP_DATA_DIR: %KSP_DATA_DIR%

echo Removing Release folder
if exist Release rmdir /s /q Release
if errorlevel 1 (
    echo ERROR: Failed to remove the Release folder
    exit /b 1
)

echo Creating Release folder
mkdir Release\EvaCMGroundMod
if errorlevel 1 (
    echo ERROR: Failed to create the Mod folder
    exit /b 1
)

echo Building Mod DLL
dotnet build EvaCMGroundMod.csproj -p:KSPDIR="%KSPDIR%" -p:KSP_DATA_DIR="%KSP_DATA_DIR%"
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
copy /y "GameData\EvaCMGroundMod\eva_cm_ground.cfg" "Release\EvaCMGroundMod"
if errorlevel 1 (
    echo ERROR: Failed to copy the Config file
    exit /b 1
)

echo Copying ReRootTest ModuleManager patch (TEST PROBE)
copy /y "GameData\EvaCMGroundMod\ReRootTest.cfg" "Release\EvaCMGroundMod"
if errorlevel 1 (
    echo ERROR: Failed to copy ReRootTest.cfg
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
echo.
echo Run at: %date% %time%
exit /b 0

:no_kspdir
echo ERROR: KSPDIR is not set - set it to your Kerbal Space Program install path
exit /b 1

:no_managed
echo ERROR: KSP managed assemblies not found at: %KSP_DATA_DIR%\Managed
exit /b 1
