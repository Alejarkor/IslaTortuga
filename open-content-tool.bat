@echo off
setlocal

set "ROOT_DIR=%~dp0"
cd /d "%ROOT_DIR%"

dotnet run --project .\src\IslaTortuga.ContentTool\IslaTortuga.ContentTool.csproj
