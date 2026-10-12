@echo off
setlocal
echo ============================================================
echo   Building File Converter Desktop Standalone Application
echo ============================================================

echo [1/2] Compiling React frontend with Vite...
cd client
call npm run build
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Frontend build failed.
    cd ..
    pause
    exit /b %ERRORLEVEL%
)
cd ..

echo [2/2] Publishing standalone executable to ReleaseBuild folder...
dotnet publish FileConverter.Desktop/FileConverter.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./ReleaseBuild
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Backend publish failed.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo ============================================================
echo   SUCCESS! Standalone Desktop Executable created:
echo   .\ReleaseBuild\FileConverter.Desktop.exe
echo ============================================================
pause
