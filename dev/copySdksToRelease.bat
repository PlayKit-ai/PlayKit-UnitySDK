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

:: Copy PlayKit SDK
echo Copying com.playkit.sdk...
if exist "%TARGET_DIR%\PlayKit_SDK" rmdir /s /q "%TARGET_DIR%\PlayKit_SDK"
xcopy "%SOURCE_DIR%\com.playkit.sdk" "%TARGET_DIR%\PlayKit_SDK\" /E /I /Y /Q

:: Copy Steam Addon
echo Copying com.playkit.sdk.steam...
if exist "%TARGET_DIR%\PlayKit_SDK.SteamAddon" rmdir /s /q "%TARGET_DIR%\PlayKit_SDK.SteamAddon"
xcopy "%SOURCE_DIR%\com.playkit.sdk.steam" "%TARGET_DIR%\PlayKit_SDK.SteamAddon\" /E /I /Y /Q

echo.
echo Done! SDKs copied to release/Assets/
echo - PlayKit_SDK
echo - PlayKit_SDK.SteamAddon

endlocal
pause
