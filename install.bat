@echo off
setlocal

echo =====================================
echo Removing existing plugin folder
echo =====================================

rmdir /s /q "%KSPDIR%\GameData\EvaCMGround"

echo.
echo =====================================
echo Copying new plugin
echo =====================================

mkdir "%KSPDIR%\GameData\EvaCMGround"
copy Output\bin\EvaCMGroundMod.dll "%KSPDIR%\GameData\EvaCMGround\EvaCMGroundMod.dll"

echo.
echo Plugin installed
