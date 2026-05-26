@echo off
setlocal EnableDelayedExpansion

set "ROOT_DIR=%~dp0"
cd /d "%ROOT_DIR%"

echo ==========================================
echo   Isla Tortuga - Lanzador de desarrollo
echo ==========================================
echo.

if not exist ".env" (
  if not exist ".env.example" (
    echo [ERROR] No existen ni .env ni .env.example en la raiz del proyecto.
    echo.
    pause
    exit /b 1
  )

  echo [INFO] No existe .env. Creando uno nuevo desde .env.example...
  copy /Y ".env.example" ".env" >nul
  if errorlevel 1 (
    echo [ERROR] No se ha podido crear .env desde .env.example.
    echo.
    pause
    exit /b 1
  )

  echo [OK] .env creado correctamente.
  echo [AVISO] Revisa JWT_SECRET y cualquier otra variable si quieres personalizarlas.
  echo.
)

where pnpm >nul 2>nul
if errorlevel 1 (
  echo [ERROR] pnpm no esta disponible en PATH.
  echo.
  pause
  exit /b 1
)

where docker >nul 2>nul
if errorlevel 1 (
  echo [ERROR] docker no esta disponible en PATH.
  echo.
  pause
  exit /b 1
)

docker info >nul 2>nul
if errorlevel 1 (
  echo [ERROR] Docker Desktop no esta listo.
  echo Abre Docker Desktop manualmente y vuelve a lanzar este script.
  echo.
  pause
  exit /b 1
)

echo [1/4] Levantando PostgreSQL con docker compose...
docker compose up -d postgres
if errorlevel 1 (
  echo.
  echo [ERROR] No se ha podido levantar PostgreSQL.
  echo Revisa Docker Desktop y vuelve a intentarlo.
  echo.
  pause
  exit /b 1
)

echo [2/4] Aplicando migraciones Prisma...
call :apply_prisma_migrations
if errorlevel 1 exit /b 1

echo [3/4] Comprobando API Nest...
call :launch_if_needed 3000 "API Nest" "IslaTortuga API" "pnpm run dev:api"

echo [4/4] Comprobando cliente Vite...
call :launch_if_needed 5173 "Cliente Vite" "IslaTortuga Client" "pnpm run dev:client"

echo.
echo Stack lanzado.
echo.
echo Comprueba estas URLs:
echo - API:         http://localhost:3000/health
echo - Cliente:     http://localhost:5173
echo.
echo El game server ahora no se lanza desde este script.
echo Levantalo manualmente desde Unity Editor con la escena del server abierta.
echo Si Unity escucha en 5055, puedes comprobarlo en:
echo - Unity Server: http://localhost:5055/health
echo.
echo Para ver el estado actual usa status-dev-stack.bat
echo Para parar el stack usa stop-dev-stack.bat
echo.
pause
exit /b 0

:apply_prisma_migrations
if not exist "%ROOT_DIR%apps\server\node_modules\.bin\prisma.cmd" (
  echo [WARN] No se ha encontrado Prisma CLI en apps\server\node_modules\.bin.
  echo [WARN] Se omite la aplicacion automatica de migraciones.
  exit /b 0
)

pushd "%ROOT_DIR%apps\server" >nul
cmd /c ""%ROOT_DIR%apps\server\node_modules\.bin\prisma.cmd" migrate deploy"
set "PRISMA_EXIT=%ERRORLEVEL%"
popd >nul

if not "%PRISMA_EXIT%"=="0" (
  echo.
  echo [ERROR] No se han podido aplicar las migraciones Prisma.
  echo Revisa la conexion a PostgreSQL y el estado de node_modules.
  echo.
  pause
  exit /b 1
)

exit /b 0

:launch_if_needed
set "PORT=%~1"
set "LABEL=%~2"
set "WINDOW_TITLE=%~3"
set "RUN_COMMAND=%~4"
call :get_port_pid %PORT%
if defined PORT_PID (
  echo [INFO] %LABEL% ya esta levantado en el puerto %PORT% ^(PID !PORT_PID!^). No se abre otra copia.
) else (
  echo [INFO] Abriendo %LABEL% en nueva ventana...
  start "%WINDOW_TITLE%" /D "%ROOT_DIR%" cmd /k "%RUN_COMMAND%"
)
exit /b 0

:get_port_pid
set "PORT_PID="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%~1 .*LISTENING"') do (
  set "PORT_PID=%%P"
  goto :eof
)
exit /b 0
