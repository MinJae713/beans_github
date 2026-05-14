# 로컬 환경에서 빌드 방법!
- 아주 간단합니다
1. Unity Hub에서 Build Both
2. Jar 파일 생성
  - 레포 루트에서 아래 순서로 실행하면 됩니다.
    ```bash
    cd server
    ./gradlew bootJar
    ```
  - 빌드가 끝나면 `server/build/libs/server-0.0.1-SNAPSHOT.jar`가 생성됩니다.
  - 생성된 파일을 `infra/localbuild/server-0.0.1-SNAPSHOT.jar` 위치로 복사한 뒤, `infra/localbuild/`에서 `docker compose up -d`를 실행하면 됩니다.

3. 아래 명령어 실행 (현재 폴더 위치에서 실행하면 됩니다)
    ```bash
    docker compose up -d
    ```
  - Spring 프로젝트와 Redis가 실행됩미다!
4. ASP.NET 프로젝트 실행
  - 그때 알려준 그 방식입니다!
    ```bash
    dotnet run -- --game-server-exe "C:\Users\SSAFY\Documents\ssafy_yoom\project\free_project\S14P31A303\unity\Builds\Server\Colosseum-Server.exe" --listen-url "http://0.0.0.0:8080"
    ``` 
5. Builds/Server/Colosseum-Server.exe 실행
6. Builds/Client/Colosseum.exe 실행
  - 이거는 플레이어 수 만큼 실행!
  - 여기까지 하면 실행이 잘 될겁니다

<br>

# 실행 종료 방법
1. 현재 폴더 위치에서 이거 실행
- 이렇게 실행하면 됩니다
  ```bash
  docker compose stop
  docker compose down
  ```
2. 바ㅏ로 내려갑니다

<br>

# Appendix. Redis 데이터 및 로그 확인 방법
1. Spring boot 로그 확인 방법
  - 이렇게 확인하면 됩니다
    ```bash
    docker logs colosseum-api
    ```
2. Redis 데이터 확인 방법
- Docker Desktop의 Redis Container ID 확인! (colosseum-redis)
- Redis CLI 접속
  ```bash
  docker exec -it [위 ID 복붙] redis-cli
  ```
- 이후 이렇게 보면 됩니다!
  ```bash
  keys * # 전체 키 목록
  smembers rooms:all # 실행중인 방 목록
  get [keys에 나온 방(rooms:all은 안됩니다)] # 방 세부 정보
  ```
