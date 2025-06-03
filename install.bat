@echo off
setlocal

echo =====================================
echo Removing existing plugin folder
echo =====================================

rmdir /s /q "%KSPDIR%\GameData\TestPlugin"

echo.
echo =====================================
echo Copying new plugin
echo =====================================

mkdir "%KSPDIR%\GameData\TestPlugin"
copy Output\bin\TestPlugin.dll "%KSPDIR%\GameData\TestPlugin\TestPlugin.dll"

echo.
echo Plugin installed
