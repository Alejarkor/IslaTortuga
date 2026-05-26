@echo off
setlocal EnableDelayedExpansion

set "ROOT_DIR=%~dp0"
cd /d "%ROOT_DIR%"

echo ==========================================
echo   Isla Tortuga - Parada de desarrollo
echo ==========================================
echo.

call :stop_port 3000 "API Nest"
call :stop_port 5173 "Cliente Vite"
echo [3/3] Parando PostgreSQL de docker compose...
docker compose stop postgres >nul 2>nul
if errorlevel 1 (
  echo [INFO] No se ha podido parar postgres o ya estaba detenido.
) else (
  echo [OK] PostgreSQL detenido.
)

echo.
echo El game server de Unity no se detiene desde este script.
echo Si esta abierto en el editor, tendras que pararlo manualmente.
echo.
echo Stack detenido.
echo.
pause
exit /b 0

:stop_port
set "PORT=%~1"
set "LABEL=%~2"
set "FOUND_PID="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%PORT% .*LISTENING"') do (
  set "FOUND_PID=%%P"
  goto :have_pid
)

echo [INFO] %LABEL% no esta escuchando en el puerto %PORT%.
exit /b 0

:have_pid
echo [INFO] Cerrando %LABEL% en el puerto %PORT% ^(PID !FOUND_PID!^)...
taskkill /PID !FOUND_PID! /T /F >nul 2>nul
if errorlevel 1 (
  echo [WARN] No se ha podido cerrar el PID !FOUND_PID!.
) else (
  echo [OK] %LABEL% detenido.
)
exit /b 0
