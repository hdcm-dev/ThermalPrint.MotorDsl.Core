@echo off
setlocal enabledelayedexpansion
echo ===================================================
echo  MotorDsl - Build local (sin pack ni publicacion)
echo ===================================================
echo.

set SRC_DIR=%~dp0..\..\src

:: MotorDsl.Pax es net10.0-android: hace falta la workload android. Aca no se fuerza
:: PaxNeptune: si esta el SDK PAX local (scripts/pax-sdk/preparar-sdk.sh) compila con el
:: binding NeptuneLite; si no, sin el. Para publicar, publish-motordsl-nuget.bat fuerza
:: -p:PaxNeptune=false, igual que cd-nuget.yml.

:: -------------------------------------------------------
:: [1/2] Restore de las 9 librerias
:: -------------------------------------------------------
echo [1/2] Restore de las 9 librerias ...
for %%P in (Printing.Abstractions Core Parser Rendering Extensions Network Bluetooth Maui Pax) do (
    dotnet restore "%SRC_DIR%\MotorDsl.%%P\MotorDsl.%%P.csproj" --nologo
    if !errorlevel! neq 0 (
        echo ERROR: Fallo restore de MotorDsl.%%P. Abortando.
        exit /b 1
    )
)
echo.

:: -------------------------------------------------------
:: [2/2] Build Release de las 9 librerias
:: -------------------------------------------------------
echo [2/2] Build Release de las 9 librerias ...
for %%P in (Printing.Abstractions Core Parser Rendering Extensions Network Bluetooth Maui Pax) do (
    dotnet build "%SRC_DIR%\MotorDsl.%%P\MotorDsl.%%P.csproj" -c Release --no-restore --nologo
    if !errorlevel! neq 0 (
        echo ERROR: Fallo build de MotorDsl.%%P. Abortando.
        exit /b 1
    )
)
echo.

echo ===================================================
echo  Build completado correctamente
echo    MotorDsl.Printing.Abstractions
echo    MotorDsl.Core
echo    MotorDsl.Parser
echo    MotorDsl.Rendering
echo    MotorDsl.Extensions
echo    MotorDsl.Bluetooth
echo    MotorDsl.Network
echo    MotorDsl.Maui
echo    MotorDsl.Pax
echo.
echo  Este script NO empaqueta ni publica en nuget.org.
echo ===================================================
echo.
echo Presiona cualquier tecla para cerrar esta ventana...
pause > nul
