#!/bin/bash
# ============================================
#  Colosseum 로컬 테스트 실행 스크립트 (Mac/Linux)
#  서버 1개 + 클라이언트 2개를 동시에 실행
# ============================================
#
#  사전 준비:
#    Unity Editor > Colosseum > Build Both
#
#  사용법:
#    ./run_local_test.sh          → 서버 + 클라이언트 2개
#    ./run_local_test.sh 1        → 서버 + 클라이언트 1개
# ============================================

SERVER_EXE="Builds/Server-Linux/Colosseum-Server"
CLIENT_EXE="Builds/Client/Colosseum"
CLIENT_COUNT=${1:-2}

PIDS=()

cleanup() {
    echo ""
    echo "Stopping all Colosseum processes..."
    for pid in "${PIDS[@]}"; do
        kill "$pid" 2>/dev/null
    done
    echo "Done."
    exit 0
}

trap cleanup SIGINT SIGTERM

# 빌드 파일 확인
if [ ! -f "$SERVER_EXE" ]; then
    echo "[ERROR] Server build not found: $SERVER_EXE"
    echo "        Unity Editor에서 Colosseum > Build Server 를 먼저 실행하세요."
    exit 1
fi

if [ ! -f "$CLIENT_EXE" ]; then
    echo "[ERROR] Client build not found: $CLIENT_EXE"
    echo "        Unity Editor에서 Colosseum > Build Client 를 먼저 실행하세요."
    exit 1
fi

echo "============================================"
echo " Colosseum Local Test"
echo " Server: $SERVER_EXE --server"
echo " Clients: ${CLIENT_COUNT}x $CLIENT_EXE"
echo "============================================"
echo ""

# 서버 시작
echo "[1] Starting Server..."
"$SERVER_EXE" --server -logFile server.log &
PIDS+=($!)
sleep 3

# 클라이언트 시작
for i in $(seq 1 $CLIENT_COUNT); do
    echo "[$(($i+1))] Starting Client $i..."
    "$CLIENT_EXE" -screen-width 960 -screen-height 540 &
    PIDS+=($!)
    sleep 1
done

echo ""
echo "============================================"
echo " All processes started!"
echo " Server log: server.log"
echo " Press Ctrl+C to stop all processes."
echo "============================================"

wait
