@echo off
setlocal

set "ROOT_DIR=%~dp0..\.."
for %%I in ("%ROOT_DIR%") do set "ROOT_DIR=%%~fI"

set "SERVER_DIR=%ROOT_DIR%\server"
set "LOCALBUILD_DIR=%ROOT_DIR%\infra\localbuild"
set "WORKER_DIR=%ROOT_DIR%\worker-agent"
set "UNITY_SERVER_EXE=%ROOT_DIR%\unity\Builds\Server\Colosseum-Server.exe"
set "UNITY_CLIENT_EXE=%ROOT_DIR%\unity\Builds\Client\Colosseum.exe"
set "BUILT_JAR=%SERVER_DIR%\build\libs\server-0.0.1-SNAPSHOT.jar"
set "TARGET_JAR=%LOCALBUILD_DIR%\server-0.0.1-SNAPSHOT.jar"
set "WORKER_BASE_URL=http://127.0.0.1:8080"
set "ROOM_API_BASE_URL=http://127.0.0.1:8081/api/v1"

REM ───── A303-171 임시: EC2 Lobby와 SessionName 격리 ─────
REM 공유 AppId의 "ColosseumLobby"와 분리하기 위해 로컬 전용 이름 사용.
REM 아래 두 줄로 인해 이 배치에서 띄우는 모든 자식 프로세스(Worker/Lobby/Client)가
REM COLOSSEUM_LOBBY_SESSION 환경변수를 상속받아 EC2와 다른 매치메이킹 풀로 격리됨.
REM 테스트 끝나면 두 줄 모두 삭제 또는 주석 처리.
set "LOBBY_SESSION=Local_171"
set "COLOSSEUM_LOBBY_SESSION=%LOBBY_SESSION%"
REM ──────────────────────────────────────────────────────

echo ============================================
echo Colosseum Local Launcher
echo Root: %ROOT_DIR%
echo ============================================
echo.
echo [1/6] Unity Build Both is manual.
echo Please Initialize First In Unity Hub...:
echo   Colosseum ^> Build Both (Server + Client)
echo.
pause

if not exist "%UNITY_SERVER_EXE%" (
    echo [ERROR] Server build not found:
    echo   %UNITY_SERVER_EXE%
    exit /b 1
)

if not exist "%UNITY_CLIENT_EXE%" (
    echo [ERROR] Client build not found:
    echo   %UNITY_CLIENT_EXE%
    exit /b 1
)

echo [2/6] Building Spring Boot JAR...
pushd "%SERVER_DIR%"
call gradlew.bat bootJar
if errorlevel 1 (
    popd
    echo [ERROR] gradlew bootJar failed.
    exit /b 1
)
popd

if not exist "%BUILT_JAR%" (
    echo [ERROR] Built JAR not found:
    echo   %BUILT_JAR%
    exit /b 1
)

echo [3/6] Copying JAR to infra/localbuild...
if exist "%TARGET_JAR%" del /f /q "%TARGET_JAR%"
copy /y "%BUILT_JAR%" "%TARGET_JAR%" >nul
if errorlevel 1 (
    echo [ERROR] Failed to copy JAR to:
    echo   %TARGET_JAR%
    exit /b 1
)

echo [4/6] Starting Spring Boot + Redis with docker compose...
pushd "%LOCALBUILD_DIR%"
docker compose up -d --build
if errorlevel 1 (
    popd
    echo [ERROR] docker compose up -d failed.
    exit /b 1
)
popd

echo [5/7] Starting Worker Agent in a new terminal...
start "Colosseum Worker Agent" cmd /k "cd /d ""%WORKER_DIR%"" && dotnet run -- --game-server-exe ""%UNITY_SERVER_EXE%"" --listen-url ""http://0.0.0.0:8080"" --room-api-base-url ""%ROOM_API_BASE_URL%"""

echo [6/7] Starting Lobby Server in a new terminal...
start "Colosseum Lobby Server" cmd /k "set COLOSSEUM_LOBBY_SESSION=%LOBBY_SESSION% && set COLOSSEUM_WORKER_BASE_URL=%WORKER_BASE_URL% && set COLOSSEUM_ROOM_API_BASE_URL=%ROOM_API_BASE_URL% && ""%UNITY_SERVER_EXE%"" --mode lobby"

echo [7/7] Starting Unity clients...
start "Colosseum Client 1" "%UNITY_CLIENT_EXE%"
start "Colosseum Client 2" "%UNITY_CLIENT_EXE%"

echo.
echo Done.
echo.
echo Notes:
echo - Colosseum-Server.exe is used in two ways:
echo     1. This script starts one Lobby process with --mode lobby
echo     2. Worker Agent starts additional Game processes with --mode game when a room is created
echo - Do not start a separate manual Game-mode server outside Worker Agent unless you are debugging that flow.
echo - If needed, check docker logs with:
echo     docker logs colosseum-api
echo.
pause
endlocal
