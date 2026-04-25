@echo off
chcp 65001 >nul
echo ========================================
Echo clears compilation cache and temporary files
echo ========================================
echo.

Echo [1/4] Clean up the bin directory ..
for /d /r . %%d in (bin) do @if exist "%%d" (
Echo delete:%% d
rd /s /q "%%d" 2>nul
)

echo.
Echo [2/4] Clean up the obj directory ..
for /d /r . %%d in (obj) do @if exist "%%d" (
Echo delete:%% d
rd /s /q "%%d" 2>nul
)

echo.
Echo [3/4] Clean up the. vs directory ..
if exist ".vs" (
Echo delete:. vs
rd /s /q ".vs" 2>nul
)

echo.
Echo [4/4] clears the. idea directory ..
if exist ".idea" (
Echo delete:. idea
rd /s /q ".idea" 2>nul
)

echo.
echo ========================================
Echo [✓] Cleanup completed!
echo ========================================
echo.
Echo can now recompile the project:
echo   dotnet clean
echo   dotnet build
echo.
pause