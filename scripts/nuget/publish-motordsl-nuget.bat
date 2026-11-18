@echo off
setlocal enabledelayedexpansion
echo ===================================================
echo  MotorDsl - Build, test y publicacion en nuget.org
echo ===================================================
echo.

:: -------------------------------------------------------
:: Paquetes: los 9 de la libreria. MotorDsl.Pax es net10.0-android: la maquina que corre
:: este script necesita la workload android (dotnet workload install android), ademas de
:: la ios para Bluetooth y Maui. MotorDsl.Pax es el contrato IPaxPrinterDevice, el transport,
:: el simulador y el perfil: no trae binarios del SDK del fabricante; la implementacion real
:: es externa (RealDeviceFactory). Ver docs/09_devops/guia-publicacion-nuget_v1.0.md.
:: -------------------------------------------------------

:: -------------------------------------------------------
:: [0/7] Resolver API Key (env var o prompt interactivo)
:: -------------------------------------------------------
if "%MOTORDSL_NUGET_API_KEY%"=="" (
    echo.
    echo ADVERTENCIA: La API key se mostrara en pantalla al escribirla.
    echo Para mayor seguridad, setear MOTORDSL_NUGET_API_KEY antes de ejecutar el script.
    echo Generar una key en: https://www.nuget.org/account/apikeys
    echo.
    set /p MOTORDSL_NUGET_API_KEY="NuGet.org API Key: "
    if "!MOTORDSL_NUGET_API_KEY!"=="" (
        echo ERROR: La API key es obligatoria. Abortando.
        exit /b 1
    )
)

set NUGET_SOURCE=https://api.nuget.org/v3/index.json

:: -------------------------------------------------------
:: Calcular siguiente version consultando nuget.org
:: (auto-bump del patch a partir de la ultima version publicada de cada paquete)
:: -------------------------------------------------------
echo Consultando ultimas versiones publicadas en nuget.org ...

for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Core"') do set CORE_NEXT=%%i
for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Parser"') do set PARSER_NEXT=%%i
for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Rendering"') do set RENDERING_NEXT=%%i
for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Extensions"') do set EXTENSIONS_NEXT=%%i
for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Printing.Abstractions"') do set PRINTING_NEXT=%%i
for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Bluetooth"') do set BLUETOOTH_NEXT=%%i
for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Maui"') do set MAUI_NEXT=%%i
for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Network"') do set NETWORK_NEXT=%%i
for /f "delims=" %%i in ('powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0get-next-version.ps1" -PackageName "MotorDsl.Pax"') do set PAX_NEXT=%%i

if "%CORE_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Core. & exit /b 1 )
if "%PARSER_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Parser. & exit /b 1 )
if "%RENDERING_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Rendering. & exit /b 1 )
if "%EXTENSIONS_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Extensions. & exit /b 1 )
if "%PRINTING_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Printing.Abstractions. & exit /b 1 )
if "%BLUETOOTH_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Bluetooth. & exit /b 1 )
if "%MAUI_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Maui. & exit /b 1 )
if "%NETWORK_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Network. & exit /b 1 )
if "%PAX_NEXT%"=="" ( echo ERROR: No se pudo calcular version de MotorDsl.Pax. & exit /b 1 )

:: Version unificada = max de los 9 paquetes, para mantenerlos alineados.
:: Un paquete que todavia no existe en nuget.org (hoy MotorDsl.Pax) hace que get-next-version.ps1
:: devuelva el fallback 1.0.0, que al ser el menor no altera el max y hace que el paquete nuevo
:: salga directamente con la version unificada vigente.
set MOTORDSL_VERSION=%CORE_NEXT%
for %%V in (%PARSER_NEXT% %RENDERING_NEXT% %EXTENSIONS_NEXT% %PRINTING_NEXT% %BLUETOOTH_NEXT% %MAUI_NEXT% %NETWORK_NEXT% %PAX_NEXT%) do (
    for /f "delims=" %%M in ('powershell -NoProfile -Command "if ([version]'%%V' -gt [version]'!MOTORDSL_VERSION!') { '%%V' } else { '!MOTORDSL_VERSION!' }"') do set MOTORDSL_VERSION=%%M
)

if "!MOTORDSL_VERSION!"=="" (
    echo ERROR: No se pudo calcular la version unificada. Abortando.
    exit /b 1
)

echo.
echo  Configuracion:
echo    Fuente:            %NUGET_SOURCE%
echo    API Key:           [oculta]
echo    Version unificada: !MOTORDSL_VERSION!
echo      ^(Core next=%CORE_NEXT%, Parser next=%PARSER_NEXT%, Rendering next=%RENDERING_NEXT%, Extensions next=%EXTENSIONS_NEXT%^)
echo      ^(Printing.Abstractions next=%PRINTING_NEXT%, Bluetooth next=%BLUETOOTH_NEXT%, Maui next=%MAUI_NEXT%^)
echo      ^(Network next=%NETWORK_NEXT%, Pax next=%PAX_NEXT%^)
echo.
echo    Los 9 paquetes se publicaran con la misma version para evitar NU1605.
echo.

set SRC_DIR=%~dp0..\..\src
set NUPKG_DIR=%~dp0..\..\nupkg

if not exist "%NUPKG_DIR%" mkdir "%NUPKG_DIR%"

:: -------------------------------------------------------
:: [1/7] Restore de las 9 librerias
:: -------------------------------------------------------
echo [1/7] Restore de las 9 librerias ...
for %%P in (Printing.Abstractions Core Parser Rendering Extensions Network Bluetooth Maui Pax) do (
    dotnet restore "%SRC_DIR%\MotorDsl.%%P\MotorDsl.%%P.csproj" --nologo
    if !errorlevel! neq 0 (
        echo ERROR: Fallo restore de MotorDsl.%%P. Abortando.
        exit /b 1
    )
)
echo.

:: -------------------------------------------------------
:: [2/7] Build Release de las 9 librerias
:: -------------------------------------------------------
echo [2/7] Build Release de las 9 librerias ...
for %%P in (Printing.Abstractions Core Parser Rendering Extensions Network Bluetooth Maui Pax) do (
    dotnet build "%SRC_DIR%\MotorDsl.%%P\MotorDsl.%%P.csproj" -c Release --no-restore --nologo /p:Version=%MOTORDSL_VERSION% /p:MotorDslVersion=%MOTORDSL_VERSION%
    if !errorlevel! neq 0 (
        echo ERROR: Fallo build de MotorDsl.%%P. Abortando.
        exit /b 1
    )
)
echo.

:: -------------------------------------------------------
:: [3/7] Tests
:: -------------------------------------------------------
echo [3/7] Ejecutando tests ...
dotnet restore "%SRC_DIR%\MotorDsl.Tests\MotorDsl.Tests.csproj" --nologo
dotnet test "%SRC_DIR%\MotorDsl.Tests\MotorDsl.Tests.csproj" -c Release --nologo
if %errorlevel% neq 0 (
    echo ERROR: Fallaron los tests. Abortando publicacion.
    exit /b 1
)
echo.

:: -------------------------------------------------------
:: [4/7] Pack de las 9 librerias
:: -------------------------------------------------------
echo [4/7] Empaquetando las 9 librerias ...
for %%P in (Printing.Abstractions Core Parser Rendering Extensions Network Bluetooth Maui Pax) do (
    dotnet pack "%SRC_DIR%\MotorDsl.%%P\MotorDsl.%%P.csproj" -c Release --no-build --nologo -p:PackageVersion=%MOTORDSL_VERSION% -p:MotorDslVersion=%MOTORDSL_VERSION% -p:PackageReleaseNotes="Release %MOTORDSL_VERSION%" -o "%NUPKG_DIR%"
    if !errorlevel! neq 0 (
        echo ERROR: Fallo pack de MotorDsl.%%P. Abortando.
        exit /b 1
    )
)
echo.

echo Paquetes generados en %NUPKG_DIR%:
dir /b "%NUPKG_DIR%\MotorDsl.*.%MOTORDSL_VERSION%.nupkg"
echo.

:: -------------------------------------------------------
:: [5/7] Push a nuget.org (orden: Printing.Abstractions -> Core/Parser/Rendering/Extensions -> Network -> Bluetooth/Maui -> Pax)
:: Nota: con --skip-duplicate, un 409 (duplicado) devuelve errorlevel 0.
:: Por lo tanto cualquier errorlevel != 0 es una falla real (403 auth, red, etc).
:: -------------------------------------------------------
echo [5/7] Publicando en nuget.org ...
for %%P in (Printing.Abstractions Core Parser Rendering Extensions Network Bluetooth Maui Pax) do (
    set NUPKG=%NUPKG_DIR%\MotorDsl.%%P.%MOTORDSL_VERSION%.nupkg
    if not exist "!NUPKG!" (
        echo ERROR: No se encontro !NUPKG!
        exit /b 1
    )
    echo       Publicando MotorDsl.%%P.%MOTORDSL_VERSION%.nupkg ...
    dotnet nuget push "!NUPKG!" --source "%NUGET_SOURCE%" --api-key "%MOTORDSL_NUGET_API_KEY%" --skip-duplicate
    if !errorlevel! neq 0 (
        echo.
        echo ERROR: Fallo el push de MotorDsl.%%P ^(errorlevel !errorlevel!^).
        echo        Causas tipicas: 403 = API key invalida o sin permisos sobre el paquete.
        echo        Revisa el output anterior. Abortando antes de taggear.
        exit /b 1
    )
    echo       MotorDsl.%%P publicado ^(o ya existia y fue saltado^).
)
echo.

:: -------------------------------------------------------
:: [6/7] Tag git y push a GitHub
:: -------------------------------------------------------
echo [6/7] Tag git v%MOTORDSL_VERSION% y push a GitHub ...
set GIT_TAG=v%MOTORDSL_VERSION%

git rev-parse --is-inside-work-tree >nul 2>&1
if errorlevel 1 (
    echo ADVERTENCIA: No es un repositorio git. Saltando tag/push.
    goto :skip_git
)

git rev-parse "%GIT_TAG%" >nul 2>&1
if not errorlevel 1 (
    echo INFO: El tag %GIT_TAG% ya existe localmente. Saltando creacion.
    goto :push_tag
)

git tag -a "%GIT_TAG%" -m "Release %MOTORDSL_VERSION%"
if errorlevel 1 (
    echo ADVERTENCIA: No se pudo crear el tag %GIT_TAG%. Saltando push.
    goto :skip_git
)
echo       Tag local %GIT_TAG% creado en HEAD.

:push_tag
git push origin "%GIT_TAG%"
if errorlevel 1 (
    echo ADVERTENCIA: Fallo el push del tag %GIT_TAG% a origin. Verifica acceso a GitHub.
) else (
    echo       Tag %GIT_TAG% pusheado a origin ^(disparara el workflow cd-nuget.yml^).
)

:skip_git
echo.

:: -------------------------------------------------------
:: [7/7] Limpieza y resumen
:: -------------------------------------------------------
echo [7/7] Limpiando cache HTTP de NuGet ...
dotnet nuget locals http-cache --clear
echo.

echo ===================================================
echo  Publicacion completada en nuget.org
echo    MotorDsl.Printing.Abstractions  %MOTORDSL_VERSION%
echo    MotorDsl.Core                   %MOTORDSL_VERSION%
echo    MotorDsl.Parser                 %MOTORDSL_VERSION%
echo    MotorDsl.Rendering              %MOTORDSL_VERSION%
echo    MotorDsl.Extensions             %MOTORDSL_VERSION%
echo    MotorDsl.Bluetooth              %MOTORDSL_VERSION%
echo    MotorDsl.Network                %MOTORDSL_VERSION%
echo    MotorDsl.Maui                   %MOTORDSL_VERSION%
echo    MotorDsl.Pax                    %MOTORDSL_VERSION%  ^(contrato y simulador^)
echo.
echo  La indexacion en nuget.org puede demorar varios minutos.
echo ===================================================
echo.
echo Presiona cualquier tecla para cerrar esta ventana...
pause > nul
