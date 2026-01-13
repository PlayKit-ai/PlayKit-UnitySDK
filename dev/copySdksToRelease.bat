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

:: Copy PlayKit SDK (excluding .meta files)
echo Copying com.playkit.sdk...
if exist "%TARGET_DIR%\PlayKit_SDK" rmdir /s /q "%TARGET_DIR%\PlayKit_SDK"
robocopy "%SOURCE_DIR%\com.playkit.sdk" "%TARGET_DIR%\PlayKit_SDK" /E /XF *.meta /NFL /NDL /NJH /NJS /NC /NS /NP

:: Copy Steam Addon (excluding .meta files)
echo Copying com.playkit.sdk.steam...
if exist "%TARGET_DIR%\PlayKit_SDK.SteamAddon" rmdir /s /q "%TARGET_DIR%\PlayKit_SDK.SteamAddon"
robocopy "%SOURCE_DIR%\com.playkit.sdk.steam" "%TARGET_DIR%\PlayKit_SDK.SteamAddon" /E /XF *.meta /NFL /NDL /NJH /NJS /NC /NS /NP

echo.
echo Done! SDKs copied to release/Assets/
echo - PlayKit_SDK
echo - PlayKit_SDK.SteamAddon

endlocal
pause
