@echo off
echo Starting Vite Frontend Dev Server and Desktop C# Host...
start "FileFlow Frontend (Vite)" cmd /c "cd client && npm.cmd run dev"
timeout /t 2 /nobreak >nul
start "FileFlow Desktop" dotnet run --project FileConverter.Desktop\FileConverter.Desktop.csproj
