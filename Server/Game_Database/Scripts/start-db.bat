@echo off
docker compose up -d
echo.
echo Base de datos levantada.
echo Adminer: http://localhost:8080
echo PostgreSQL: localhost:5432
pause