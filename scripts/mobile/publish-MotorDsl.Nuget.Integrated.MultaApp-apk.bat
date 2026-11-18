@echo off
setlocal enabledelayedexpansion
REM ============================================================
REM  MotorDsl.Nuget.Integrated.MultaApp -- Genera bundle APK de MotorDsl.Nuget.Integrated.MultaApp (Release)
REM
REM  Uso:
REM    scripts\mobile\publish-MotorDsl.Nuget.Integrated.MultaApp-apk.bat
REM    scripts\mobile\publish-MotorDsl.Nuget.Integrated.MultaApp-apk.bat http://{host-de-pruebas}:5000
REM
REM  Si se pasa una URL como argumento, se patchea
REM  Resources/Raw/motordsl-config.json (backendBaseUrl) antes de
REM  compilar, para que el APK apunte a esa URL del backend.
REM
REM  IMPORTANTE: EmbedAssembliesIntoApk=true es obligatorio para
REM  que el APK firmado pueda lanzarse via "adb install".
REM ============================================================

set "PROJECT_ROOT=%~dp0..\.."
set "MOBILE_PROJECT=%PROJECT_ROOT%\samples\MotorDsl.Nuget.Integrated.MultaApp\MotorDsl.Nuget.Integrated.MultaApp.csproj"
set "CONFIG_JSON=%PROJECT_ROOT%\samples\MotorDsl.Nuget.Integrated.MultaApp\Resources\Raw\motordsl-config.json
set "ANDROID_SDK=C:\Program Files (x86)\Android\android-sdk"
set "OUT_DIR=%PROJECT_ROOT%\out\mobile"
set "OUT_APK=%PROJECT_ROOT%\out\mobile\MotorDsl.Nuget.Integrated.MultaApp.apk"

echo.
echo ============================================================
echo   MotorDsl.Nuget.Integrated.MultaApp -- Publish APK (Release)
echo ============================================================
echo.

REM ------------------------------------------------------------
REM  Patcheo opcional de backendBaseUrl en motordsl-config.json
REM ------------------------------------------------------------
if not "%~1"=="" (
    echo [0/4] Patcheando %CONFIG_JSON%
    echo        backendBaseUrl = %~1
    powershell -NoProfile -Command "$p='%CONFIG_JSON%'; $j = Get-Content -Raw -Path $p | ConvertFrom-Json; $j.backendBaseUrl = '%~1'; ($j | ConvertTo-Json -Depth 5) | Set-Content -Path $p -Encoding UTF8"
    if !ERRORLEVEL! neq 0 (
        echo ERROR al patchear motordsl-config.json
        goto :error
    )
    echo.
)

REM ------------------------------------------------------------
REM  Clean previo + build Release + publish APK
REM ------------------------------------------------------------
echo [1/4] Limpiando artefactos previos (Release, net10.0-android)...
dotnet clean "%MOBILE_PROJECT%" ^
    -f net10.0-android ^
    -c Release ^
    --nologo --verbosity quiet
if %ERRORLEVEL% neq 0 (
    echo ERROR: Fallo el clean de MotorDsl.Integrated.MultaApp
    goto :error
)
if exist "%OUT_DIR%" rmdir /S /Q "%OUT_DIR%"
echo.

echo [2/4] dotnet publish (Release, android-arm64, EmbedAssembliesIntoApk=true)
echo        Tarda varios minutos la primera vez.
dotnet publish "%MOBILE_PROJECT%" ^
    -f net10.0-android ^
    -c Release ^
    -p:AndroidSdkDirectory="%ANDROID_SDK%" ^
    -p:RuntimeIdentifier=android-arm64 ^
    -p:AndroidPackageFormat=apk ^
    -p:EmbedAssembliesIntoApk=true ^
    -o "%OUT_DIR%"
if %ERRORLEVEL% neq 0 (
    echo ERROR durante dotnet publish.
    goto :error
)
echo.

REM ------------------------------------------------------------
REM  Localizar el APK firmado y copiarlo a out\MotorDsl.Integrated.MultaApp.apk
REM ------------------------------------------------------------
echo [3/4] Buscando APK firmado en %OUT_DIR% ...
set "FOUND_APK="
for %%F in ("%OUT_DIR%\*-Signed.apk") do set "FOUND_APK=%%F"
if not defined FOUND_APK (
    for %%F in ("%OUT_DIR%\*.apk") do set "FOUND_APK=%%F"
)
if not defined FOUND_APK (
    echo ERROR: no se encontro ningun APK en %OUT_DIR%.
    goto :error
)
echo        APK origen: !FOUND_APK!

echo.
echo [4/4] Copiando a %OUT_APK% ...
copy /Y "!FOUND_APK!" "%OUT_APK%" >nul
if %ERRORLEVEL% neq 0 (
    echo ERROR al copiar el APK final.
    goto :error
)

echo.
echo ============================================================
echo   APK generado:
echo     %OUT_APK%
echo   Para instalarlo:
echo     adb install -r "%OUT_APK%"
echo ============================================================
echo.

goto :end
:error
echo.
echo ============================================================
echo   ERROR: Fallo el publish del APK.
echo ============================================================
exit /b 1
:end
endlocal
exit /b 0
