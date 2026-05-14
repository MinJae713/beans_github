@echo off
REM ============================================
REM  Colosseum Local Test Runner
REM  Starts 1 Server + 2 Clients
REM ============================================
REM
REM  Prerequisites:
REM    Unity Editor > Colosseum > Build Both
REM
REM  Usage:
REM    run_local_test.bat        = Server + 2 Clients
REM    run_local_test.bat 1      = Server + 1 Client
REM ============================================

set SERVER_EXE=Builds\Server\Colosseum-Server.exe
set CLIENT_EXE=Builds\Client\Colosseum.exe
set CLIENT_COUNT=%1
if "%CLIENT_COUNT%"=="" set CLIENT_COUNT=2

if not exist "%SERVER_EXE%" (
    echo [ERROR] Server build not found: %SERVER_EXE%
    echo         Build it first: Unity Editor - Colosseum - Build Server
    pause
    exit /b 1
)

if not exist "%CLIENT_EXE%" (
    echo [ERROR] Client build not found: %CLIENT_EXE%
    echo         Build it first: Unity Editor - Colosseum - Build Client
    pause
    exit /b 1
)

echo ============================================
echo  Colosseum Local Test
echo  Server: %SERVER_EXE%
echo  Clients: %CLIENT_COUNT%
echo ============================================
echo.

echo [1] Starting Server...
REM -logFile - : stdout 출력 → PowerShell Tee-Object 로 cmd 창과 server.log 파일에 동시 기록
REM (자식 GameServer 프로세스의 stdout 도 LobbyServer 가 캡쳐 후 Debug.Log 로 같은 stream 에 forward)
start "Colosseum-Server" powershell -NoExit -Command "& '%SERVER_EXE%' -logFile - 2>&1 | Tee-Object -FilePath server.log"
timeout /t 3 /nobreak >nul

for /L %%i in (1,1,%CLIENT_COUNT%) do (
    echo [%%i] Starting Client %%i...
    start "Colosseum-Client-%%i" "%CLIENT_EXE%"
    timeout /t 1 /nobreak >nul
)

echo.
echo ============================================
echo  All processes started!
echo  Server log: server.log
echo ============================================
echo.
echo  Press any key to STOP all processes...
pause >nul

echo Stopping all processes...
taskkill /IM "Colosseum-Server.exe" /F >nul 2>&1
taskkill /IM "Colosseum.exe" /F >nul 2>&1
echo Done.
