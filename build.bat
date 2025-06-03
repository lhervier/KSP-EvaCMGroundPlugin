@echo off
setlocal

msbuild TestPlugin.csproj /p:Configuration=Debug
