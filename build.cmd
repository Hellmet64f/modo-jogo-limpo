@echo off
setlocal
rem ===================================================================
rem  MODO JOGO LIMPO - script de build
rem  Compila os arquivos .cs em app\ para o executavel ModoJogo.exe
rem
rem  Uso:  build.cmd          (compila e substitui o exe na raiz)
rem        build.cmd --test   (so compila para app\ sem substituir)
rem ===================================================================

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "APP=%~dp0app"
set "SAIDA=%~dp0ModoJogo.exe"

if not exist "%CSC%" (
    echo [ERRO] csc.exe nao encontrado em %CSC%
    echo Instale o .NET Framework 4.x Developer Pack.
    exit /b 1
)

echo.
echo === Compilando ModoJogo.exe ===
echo     Fontes : %APP%
echo     Saida  : %SAIDA%
echo.

pushd "%APP%"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /debug- ^
  /out:"%SAIDA%" ^
  /win32icon:"ModoJogo.exe.ico" ^
  /win32manifest:"app.manifest" ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  /reference:System.Web.Extensions.dll ^
  /reference:Microsoft.VisualBasic.dll ^
  ModoJogo.cs UI.cs SeletorWallpaper.cs Version.cs

set "ERRO=%ERRORLEVEL%"
popd

if not "%ERRO%"=="0" (
    echo.
    echo [FALHOU] Erro de compilacao. Codigo %ERRO%.
    exit /b %ERRO%
)

echo.
echo [OK] ModoJogo.exe gerado.
for %%F in ("%SAIDA%") do echo     Tamanho: %%~zF bytes
for %%F in ("%SAIDA%") do echo     Data   : %%~tF
echo.
echo Feche o Modo Jogo que estiver aberto antes de substituir o exe.
exit /b 0

