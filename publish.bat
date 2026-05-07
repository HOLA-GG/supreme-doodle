@echo off
echo ============================================
echo  Security Monitor - Script de Publicacion
echo  Version 2.0 (Framework-Dependent)
echo ============================================
echo.

echo [1/4] Limpiando carpeta Publish...
rmdir /S /Q Publish 2>nul
mkdir Publish\SoldierService
mkdir Publish\SoldierConfig
mkdir Publish\Commander

echo [2/4] Publicando Soldier Service (Independiente)...
dotnet publish SecurityMonitor.Soldier\SecurityMonitor.Soldier.csproj -c Release -o Publish\SoldierService -p:PublishSingleFile=true -p:PublishTrimmed=true

echo [3/4] Publicando Soldier Config Tool (Independiente + Carpeta Completa)...
dotnet publish SecurityMonitor.Soldier.Config\SecurityMonitor.Soldier.Config.csproj -c Release -o Publish\SoldierConfig -p:PublishSingleFile=false

echo [4/4] Publicando Commander Desktop (Independiente + Carpeta Completa)...
dotnet publish SecurityMonitor.Commander.Desktop\SecurityMonitor.Commander.Desktop.csproj -c Release -o Publish\Commander -p:PublishSingleFile=false

echo.
echo ============================================
echo  Publicacion completada exitosamente!
echo ============================================
echo.
echo  NOTA: El equipo destino necesita .NET 8 Runtime
echo  Descargar: https://dotnet.microsoft.com/download/dotnet/8.0
echo.
pause
exit /b 0

:error
echo.
echo ERROR: Fallo en la publicacion!
pause
exit /b 1
