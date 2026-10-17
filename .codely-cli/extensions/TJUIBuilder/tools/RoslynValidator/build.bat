@echo off
REM Build script for RoslynValidator on Windows
REM Usage: build.bat [runtime]

setlocal

set SCRIPT_DIR=%~dp0
set OUTPUT_DIR=%SCRIPT_DIR%bin
set PROJECT=%SCRIPT_DIR%RoslynValidator.csproj
set RUNTIME=%1
if "%RUNTIME%"=="" set RUNTIME=win-x64

echo Building RoslynValidator for %RUNTIME%...

REM Clean previous build
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"

REM Restore dependencies
dotnet restore "%PROJECT%"

if %ERRORLEVEL% neq 0 (
    echo Error: Failed to restore dependencies
    exit /b 1
)

REM Build and publish
dotnet publish "%PROJECT%" -c Release -r %RUNTIME% --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:PublishReadyToRun=false -o "%OUTPUT_DIR%"

if %ERRORLEVEL% neq 0 (
    echo Error: Build failed
    exit /b 1
)

REM dotnet publish writes intermediate build outputs under bin\Release even when -o points
REM at bin. Remove them so release archives contain one authoritative executable.
if exist "%OUTPUT_DIR%\Release" rmdir /s /q "%OUTPUT_DIR%\Release"

echo.
echo Build successful!
echo Output: %OUTPUT_DIR%\RoslynValidator.exe
echo.

REM Show usage example
echo Usage example:
echo   %OUTPUT_DIR%\RoslynValidator.exe --config config.json --json

endlocal
