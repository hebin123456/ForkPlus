@echo off
setlocal

set "DOTNET_ROOT=%ProgramFiles%\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "SOLUTION=%~dp0ForkPlus.sln"

set "DEVENV="
if exist "%ProgramFiles%\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe" set "DEVENV=%ProgramFiles%\Microsoft Visual Studio\18\Community\Common7\IDE\devenv.exe"
if not defined DEVENV if exist "%ProgramFiles%\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe" set "DEVENV=%ProgramFiles%\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe"
if not defined DEVENV if exist "%ProgramFiles%\Microsoft Visual Studio\18\Enterprise\Common7\IDE\devenv.exe" set "DEVENV=%ProgramFiles%\Microsoft Visual Studio\18\Enterprise\Common7\IDE\devenv.exe"

if not exist "%DOTNET_ROOT%\dotnet.exe" (
  echo dotnet SDK was not found at "%DOTNET_ROOT%".
  echo Install the x64 .NET SDK or update DOTNET_ROOT in this script.
  exit /b 1
)

if not defined DEVENV (
  echo Visual Studio 2026 was not found under "%ProgramFiles%\Microsoft Visual Studio\18".
  exit /b 1
)

start "" "%DEVENV%" "%SOLUTION%"
