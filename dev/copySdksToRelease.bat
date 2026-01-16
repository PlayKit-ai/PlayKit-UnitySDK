@echo off
setlocal

echo Copying PlayKit SDKs to release project...

set SOURCE_DIR=%~dp0Packages
set TARGET_DIR=%~dp0..\release\Assets

:: Create target directory if not exists
if not exist "%TARGET_DIR%" (
    echo Creating release/Assets directory...
    mkdir "%TARGET_DIR%"
)

:: Copy PlayKit SDK (including .meta files)
echo Copying com.playkit.sdk...
robocopy "%SOURCE_DIR%\com.playkit.sdk" "%TARGET_DIR%\PlayKit_SDK" /E /NFL /NDL /NJH /NJS /NC /NS /NP

:: Copy Steam Addon (including .meta files)
echo Copying com.playkit.sdk.steam...
robocopy "%SOURCE_DIR%\com.playkit.sdk.steam" "%TARGET_DIR%\PlayKit_SDK.SteamAddon" /E /NFL /NDL /NJH /NJS /NC /NS /NP

echo.
echo Done! SDKs copied to release/Assets/
echo - PlayKit_SDK
echo - PlayKit_SDK.SteamAddon

endlocal
pause
