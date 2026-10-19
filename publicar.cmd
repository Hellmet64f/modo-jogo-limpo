@echo off
setlocal
rem ===================================================================
rem  Publicar o Modo Jogo Limpo no GitHub (repositorio publico)
rem
rem  Uso:  publicar.cmd <utilizador> <repositorio> <token>
rem  Ex:  publicar.cmd miguel modo-jogo-limpo ghp_xxxxxxxxxxxx
rem ===================================================================

set "GIT=C:\Program Files\Git\cmd\git.exe"
set "DIR=%~dp0"

if "%~3"=="" (
    echo.
    echo Falta informacao.
    echo.
    echo   publicar.cmd  ^<utilizador^>  ^<repositorio^>  ^<token^>
    echo.
    echo Exemplo:
    echo   publicar.cmd miguel modo-jogo-limpo ghp_xxxxxxxxxxxxxxxxxxxx
    echo.
    pause
    exit /b 1
)

set "USER=%~1"
set "REPO=%~2"
set "TOKEN=%~3"
set "URL=https://%USER%:%TOKEN%@github.com/%USER%/%REPO%.git"

echo.
echo === Publicando em github.com/%USER%/%REPO% ===
echo.

cd /d "%DIR%"

rem o remote
"%GIT%" remote remove origin 2>nul
"%GIT%" remote add origin %URL%
"%GIT%" branch -M main

rem garante que nao ha ficheiros grandes
"%GIT%" count-objects -vH

echo.
echo === Enviando commits ===
"%GIT%" push -u origin main
set "ERRO=%ERRORLEVEL%"

if not "%ERRO%"=="0" (
    echo.
    echo [FALHOU] Erro %ERRO%. Verifica o token e se o repositorio existe.
    echo.
    echo O token precisa do scope 'repo' (repositorios).
    pause
    exit /b %ERRO%
)

rem limpa o token do remote para nao ficar guardado em texto simples
"%GIT%" remote set-url origin https://github.com/%USER%/%REPO%.git

echo.
echo === PRONTO! ===
echo.
echo   https://github.com/%USER%/%REPO%
echo.
echo O repositorio foi criado com o codigo, o executavel e o README.
echo.
pause
