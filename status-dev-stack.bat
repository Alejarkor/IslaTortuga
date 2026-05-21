@echo off
setlocal

set "ROOT_DIR=%~dp0"
cd /d "%ROOT_DIR%"

echo ==========================================
echo   Isla Tortuga - Estado del stack
echo ==========================================
echo.

call :show_port 3000 "API Nest"
call :show_port 5173 "Cliente Vite"
call :show_port 5055 "Game Server"
call :show_port 5432 "PostgreSQL"

echo.
pause
exit /b 0

:show_port
setlocal EnableDelayedExpansion
set "PORT=%~1"
set "LABEL=%~2"
set "PID="
for /f "tokens=5" %%P in ('netstat -ano ^| findstr /R /C:":%PORT% .*LISTENING"') do (
  set "PID=%%P"
  goto :show_found
)
:show_found
if defined PID (
  echo [RUNNING] %LABEL% escuchando en %PORT% ^(PID !PID!^)
) else (
  echo [STOPPED] %LABEL% no esta escuchando en %PORT%
)
endlocal
exit /b 0
