@echo off
setlocal

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

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] dotnet no esta disponible en PATH.
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

echo [2/4] Abriendo API Nest en nueva ventana...
start "IslaTortuga API" /D "%ROOT_DIR%" cmd /k "pnpm run dev:api"

echo [3/4] Abriendo cliente Vite en nueva ventana...
start "IslaTortuga Client" /D "%ROOT_DIR%" cmd /k "pnpm run dev:client"

echo [4/4] Abriendo game server C# en nueva ventana...
start "IslaTortuga Game Server" /D "%ROOT_DIR%" cmd /k "dotnet run --project .\src\IslaTortuga.Server\IslaTortuga.Server.csproj"

echo.
echo Stack lanzado.
echo.
echo Comprueba estas URLs:
echo - API:         http://localhost:3000/health
echo - Game Server: http://localhost:5055/health
echo - Cliente:     http://localhost:5173
echo.
echo Si alguna ventana muestra error de puerto ocupado, cierra la instancia vieja y vuelve a lanzar este script.
echo.
pause
