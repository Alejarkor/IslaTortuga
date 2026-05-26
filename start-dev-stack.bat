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
  call :start_docker
  if errorlevel 1 exit /b 1
)

echo [1/3] Levantando PostgreSQL con docker compose...
docker compose up -d postgres
if errorlevel 1 (
  echo.
  echo [ERROR] No se ha podido levantar PostgreSQL.
  echo Revisa Docker Desktop y vuelve a intentarlo.
  echo.
  pause
  exit /b 1
)

echo [2/3] Comprobando API Nest...
call :launch_if_needed 3000 "API Nest" "IslaTortuga API" "pnpm run dev:api"

echo [3/3] Comprobando cliente Vite...
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

:start_docker
echo [INFO] Docker Desktop no esta en ejecucion. Arrancando...
set "DOCKER_EXE="
if exist "C:\Program Files\Docker\Docker\Docker Desktop.exe" (
  set "DOCKER_EXE=C:\Program Files\Docker\Docker\Docker Desktop.exe"
)
if not defined DOCKER_EXE (
  if exist "%LOCALAPPDATA%\Docker\Docker Desktop.exe" (
    set "DOCKER_EXE=%LOCALAPPDATA%\Docker\Docker Desktop.exe"
  )
)
if not defined DOCKER_EXE (
  echo [ERROR] No se ha encontrado Docker Desktop.exe. Arrancalo manualmente y vuelve a intentarlo.
  echo.
  pause
  exit /b 1
)

start "" "%DOCKER_EXE%"
echo [INFO] Esperando a que Docker Desktop este listo (puede tardar hasta 60s)...
call :wait_for_docker
if errorlevel 1 (
  echo [ERROR] Docker Desktop no ha arrancado a tiempo. Intentalo de nuevo cuando este listo.
  echo.
  pause
  exit /b 1
)
echo [OK] Docker Desktop listo.
echo.
exit /b 0

:wait_for_docker
set /a TRIES=0
:wait_for_docker_loop
timeout /t 3 /nobreak >nul
docker info >nul 2>nul
if not errorlevel 1 exit /b 0
set /a TRIES+=1
if !TRIES! GEQ 20 exit /b 1
echo   ... sigo esperando (!TRIES!/20^)
goto wait_for_docker_loop

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
