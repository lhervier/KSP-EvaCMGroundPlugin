@echo off
setlocal

echo.
echo -------------------------------------------
echo Updating the KSP-Shared submodule
echo -------------------------------------------

REM Fetch the latest commits from the submodule's tracked branch (main),
REM instead of staying on the SHA pinned by the parent repository.
git submodule update --remote --merge KSP-Shared
if errorlevel 1 (
    echo ERROR: Failed to update KSP-Shared submodule
    exit /b 1
)

echo.
echo KSP-Shared submodule updated successfully
echo.
echo If the library changed, remember to commit the new pointer:
echo   git add KSP-Shared ^&^& git commit -m "Bump KSP-Shared"
