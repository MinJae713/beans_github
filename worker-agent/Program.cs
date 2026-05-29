using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

string gameServerExePath = GetArg(args, "--game-server-exe")
    ?? Environment.GetEnvironmentVariable("COLOSSEUM_GAME_SERVER_EXE")
    ?? throw new InvalidOperationException("Missing --game-server-exe or COLOSSEUM_GAME_SERVER_EXE");

string listenUrl = GetArg(args, "--listen-url")
    ?? Environment.GetEnvironmentVariable("COLOSSEUM_WORKER_LISTEN_URL")
    ?? "http://0.0.0.0:8080";

string roomApiBaseUrl = (GetArg(args, "--room-api-base-url")
    ?? Environment.GetEnvironmentVariable("COLOSSEUM_ROOM_API_BASE_URL")
    ?? "http://127.0.0.1:8081/api/v1").TrimEnd('/');

string workerId = GetArg(args, "--worker-id")
    ?? Environment.GetEnvironmentVariable("COLOSSEUM_WORKER_ID")
    ?? Environment.MachineName;

string workerDisplayName = GetArg(args, "--worker-display-name")
    ?? Environment.GetEnvironmentVariable("COLOSSEUM_WORKER_DISPLAY_NAME")
    ?? workerId;

var runningRooms = new Dictionary<string, RunningRoom>(StringComparer.Ordinal);
var syncLock = new object();
var roomReadyTimeout = TimeSpan.FromSeconds(15);
using var roomApiHttpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(5)
};

app.MapGet("/workers/health", () =>
{
    lock (syncLock)
    {
        return Results.Ok(new
        {
            ok = true,
            activeRooms = runningRooms.Count
        });
    }
});

app.MapGet("/workers/rooms", () =>
{
    lock (syncLock)
    {
        return Results.Ok(runningRooms.Values.Select(room => room.Snapshot).ToList());
    }
});

app.MapPost("/workers/spawn-room", async (SpawnRoomRequest request) =>
{
    var workerRequestReceivedAt = DateTimeOffset.UtcNow;
    var workerRequestSw = Stopwatch.StartNew();
    Console.WriteLine($"[Colosseum-Timing][Worker - Request From Lobby][WT1] room='{request.RoomName}' session='{request.SessionName}' at='{workerRequestReceivedAt:O}'");

    IResult CreateFailureResponse(string errorCode)
    {
        workerRequestSw.Stop();
        var workerResponseAt = DateTimeOffset.UtcNow;
        Console.WriteLine($"[Colosseum-Timing][Worker - Response To Lobby][WT4] room='{request.RoomName}' session='{request.SessionName}' ok=false error='{errorCode}' at='{workerResponseAt:O}' elapsed_from_wt1_ms={workerRequestSw.ElapsedMilliseconds}");
        return Results.Ok(new SpawnRoomResponse(false, errorCode));
    }

    if (string.IsNullOrWhiteSpace(request.SessionName) || string.IsNullOrWhiteSpace(request.RoomName))
    {
        return CreateFailureResponse("room_name_empty");
    }

    if (!IsValidName(request.SessionName))
    {
        return CreateFailureResponse("invalid_session_name");
    }

    if (!IsValidName(request.RoomName))
    {
        return CreateFailureResponse("invalid_room_name");
    }

    lock (syncLock)
    {
        if (runningRooms.ContainsKey(request.SessionName))
            return CreateFailureResponse("duplicate_session");
    }

    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = gameServerExePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-logFile");
        startInfo.ArgumentList.Add("-");
        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("game");
        startInfo.ArgumentList.Add("--session");
        startInfo.ArgumentList.Add(request.SessionName);
        startInfo.ArgumentList.Add("--room-name");
        startInfo.ArgumentList.Add(request.RoomName);
        startInfo.ArgumentList.Add("--max-players");
        startInfo.ArgumentList.Add(request.MaxPlayers.ToString());
        startInfo.ArgumentList.Add("--scene");
        startInfo.ArgumentList.Add(request.SceneName ?? "SampleScene");

        var process = Process.Start(startInfo);
        if (process == null)
            return CreateFailureResponse("ROOM_CREATE_FAILED");
        var gameServerInitializedAt = DateTimeOffset.UtcNow;
        var gameServerCreateSw = Stopwatch.StartNew();
        var readyLogged = 0;
        Console.WriteLine($"[Colosseum-Timing][Worker - Game Server initialized][WT2] room='{request.RoomName}' session='{request.SessionName}' pid={process.Id} at='{gameServerInitializedAt:O}'");

        var room = new RunningRoom
        {
            Process = process,
            Snapshot = new RoomSnapshot
            {
                SessionName = request.SessionName,
                RoomName = request.RoomName,
                HostNickname = request.HostNickname ?? string.Empty,
                Status = "waiting",
                PlayerCount = 0,
                MaxPlayers = request.MaxPlayers
            },
            RoomApiCreated = false,
            PendingStatus = null,
            PendingPlayerCount = null
        };

        lock (syncLock)
        {
            runningRooms[request.SessionName] = room;
        }

        var roomReadyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
                return;

            if (e.Data.StartsWith("##READY:", StringComparison.Ordinal))
            {
                if (string.Equals(e.Data.Substring(8), "session_open", StringComparison.OrdinalIgnoreCase))
                {
                    if (Interlocked.Exchange(ref readyLogged, 1) == 0)
                    {
                        gameServerCreateSw.Stop();
                        var gameServerReadyAt = DateTimeOffset.UtcNow;
                        Console.WriteLine($"[Colosseum-Timing][Worker - Game Server Create Finished][WT3] room='{request.RoomName}' session='{request.SessionName}' ready='session_open' at='{gameServerReadyAt:O}' elapsed_from_wt2_ms={gameServerCreateSw.ElapsedMilliseconds}");
                    }
                    roomReadyTcs.TrySetResult(true);
                }
                return;
            }

            if (e.Data.StartsWith("##STATUS:", StringComparison.Ordinal))
            {
                string nextStatus = e.Data.Substring(9);
                bool shouldPatch = false;

                lock (syncLock)
                {
                    if (runningRooms.TryGetValue(request.SessionName, out var tracked))
                    {
                        tracked.Snapshot.Status = nextStatus;
                        if (tracked.RoomApiCreated)
                        {
                            shouldPatch = true;
                        }
                        else
                        {
                            tracked.PendingStatus = nextStatus;
                        }
                    }
                }

                if (shouldPatch)
                    _ = PatchRoomAsync(new RoomPatchRequestDto(SessionName: request.SessionName, Status: nextStatus, PlayerCount: null));
                return;
            }

            if (e.Data.StartsWith("##PLAYERS:", StringComparison.Ordinal))
            {
                if (int.TryParse(e.Data.Substring(10), out int count))
                {
                    bool shouldPatch = false;

                    lock (syncLock)
                    {
                        if (runningRooms.TryGetValue(request.SessionName, out var tracked))
                        {
                            tracked.Snapshot.PlayerCount = count;
                            if (tracked.RoomApiCreated)
                            {
                                shouldPatch = true;
                            }
                            else
                            {
                                tracked.PendingPlayerCount = count;
                            }
                        }
                    }

                    if (shouldPatch)
                        _ = PatchRoomAsync(new RoomPatchRequestDto(SessionName: request.SessionName, Status: null, PlayerCount: count));
                }
                return;
            }

            Console.WriteLine($"[GameServer:{request.RoomName}] {e.Data}");
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.Error.WriteLine($"[GameServer:{request.RoomName}] {e.Data}");
        };

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            roomReadyTcs.TrySetException(
                new InvalidOperationException($"Game server exited before session became ready: {request.SessionName}"));
            _ = CleanupRoomAsync(request.SessionName, stopProcess: false, deleteRoomApiEntry: true);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var createPayload = new RoomCreateRequestDto(
            request.SessionName,
            request.RoomName,
            request.HostNickname ?? string.Empty,
            "waiting",
            0,
            request.MaxPlayers,
            workerId,
            workerDisplayName,
            listenUrl,
            process.Id
        );

        var createResponse = await roomApiHttpClient.PostAsJsonAsync(roomApiBaseUrl + "/rooms", createPayload);
        if (!createResponse.IsSuccessStatusCode)
        {
            await CleanupRoomAsync(request.SessionName, stopProcess: true, deleteRoomApiEntry: false);
            Console.Error.WriteLine($"[WorkerAgent] Room API create failed: HTTP {(int)createResponse.StatusCode}");
            return CreateFailureResponse("ROOM_CREATE_FAILED");
        }

        RoomPatchRequestDto? pendingPatch = null;
        lock (syncLock)
        {
            if (runningRooms.TryGetValue(request.SessionName, out var tracked))
            {
                tracked.RoomApiCreated = true;
                if (tracked.PendingStatus != null || tracked.PendingPlayerCount.HasValue)
                {
                    pendingPatch = new RoomPatchRequestDto(
                        SessionName: request.SessionName,
                        Status: tracked.PendingStatus,
                        PlayerCount: tracked.PendingPlayerCount);
                    tracked.PendingStatus = null;
                    tracked.PendingPlayerCount = null;
                }
            }
        }

        if (pendingPatch != null)
            _ = PatchRoomAsync(pendingPatch);

        try
        {
            await WaitForRoomReadyAsync(roomReadyTcs.Task, roomReadyTimeout);
        }
        catch (Exception e)
        {
            await CleanupRoomAsync(request.SessionName, stopProcess: true, deleteRoomApiEntry: true);
            Console.Error.WriteLine($"[WorkerAgent] Room ready wait failed for '{request.RoomName}': {e.Message}");
            return CreateFailureResponse("ROOM_CREATE_FAILED");
        }

        workerRequestSw.Stop();
        var workerResponseAt = DateTimeOffset.UtcNow;
        Console.WriteLine($"[Colosseum-Timing][Worker - Response To Lobby][WT4] room='{request.RoomName}' session='{request.SessionName}' ok=true at='{workerResponseAt:O}' elapsed_from_wt1_ms={workerRequestSw.ElapsedMilliseconds}");
        Console.WriteLine($"[WorkerAgent] Spawned room '{request.RoomName}' ({request.SessionName}) pid={process.Id}");
        return Results.Ok(new SpawnRoomResponse(true, null));
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"[WorkerAgent] Spawn failed: {e.Message}");
        return CreateFailureResponse("ROOM_CREATE_FAILED");
    }
});

app.Lifetime.ApplicationStopping.Register(() =>
{
    List<string> sessionNames;
    lock (syncLock)
    {
        sessionNames = runningRooms.Keys.ToList();
    }

    try
    {
        Task.WhenAll(sessionNames.Select(sessionName =>
            CleanupRoomAsync(sessionName, stopProcess: true, deleteRoomApiEntry: true))).GetAwaiter().GetResult();
    }
    catch
    {
        // ignore
    }
});

app.Run(listenUrl);

static async Task WaitForRoomReadyAsync(Task readyTask, TimeSpan timeout)
{
    var completedTask = await Task.WhenAny(readyTask, Task.Delay(timeout));
    if (completedTask != readyTask)
        throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0}s waiting for game server readiness.");

    await readyTask;
}

static void TryStopProcess(Process process)
{
    try
    {
        if (!process.HasExited)
            process.Kill();
    }
    catch
    {
        // ignore
    }
}

static void WaitForProcessExit(Process process)
{
    try
    {
        process.WaitForExit(1000);
    }
    catch
    {
        // ignore
    }
}

async Task CleanupRoomAsync(string sessionName, bool stopProcess, bool deleteRoomApiEntry)
{
    RunningRoom? tracked;

    lock (syncLock)
    {
        if (!runningRooms.TryGetValue(sessionName, out tracked) || tracked.CleanupStarted)
            return;

        tracked.CleanupStarted = true;
        runningRooms.Remove(sessionName);
    }

    try
    {
        if (stopProcess)
            TryStopProcess(tracked.Process);

        WaitForProcessExit(tracked.Process);

        if (deleteRoomApiEntry && tracked.RoomApiCreated)
            await DeleteRoomAsync(sessionName);
    }
    finally
    {
        tracked.Process.Dispose();
    }
}

async Task PatchRoomAsync(RoomPatchRequestDto payload)
{
    try
    {
        var response = await roomApiHttpClient.PatchAsJsonAsync(
            roomApiBaseUrl + "/rooms", payload);

        if (!response.IsSuccessStatusCode)
            Console.Error.WriteLine($"[WorkerAgent] Room API patch failed for '{payload.SessionName}': HTTP {(int)response.StatusCode}");
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"[WorkerAgent] Room API patch exception for '{payload.SessionName}': {e.Message}");
    }
}

async Task DeleteRoomAsync(string sessionName)
{
    try
    {
        var response = await roomApiHttpClient.DeleteAsync(
            roomApiBaseUrl + "/rooms/" + Uri.EscapeDataString(sessionName));

        if (!response.IsSuccessStatusCode)
            Console.Error.WriteLine($"[WorkerAgent] Room API delete failed for '{sessionName}': HTTP {(int)response.StatusCode}");
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"[WorkerAgent] Room API delete exception for '{sessionName}': {e.Message}");
    }
}

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}

static bool IsValidName(string roomName)
{
    return Regex.IsMatch(roomName, "^[A-Za-z0-9_-]{1,32}$");
}

record SpawnRoomRequest(string SessionName, string RoomName, string? HostNickname, int MaxPlayers, string? SceneName);
record SpawnRoomResponse(bool Ok, string? ErrorCode);
record RoomCreateRequestDto(
    string SessionName,
    string RoomName,
    string HostNickname,
    string Status,
    int PlayerCount,
    int MaxPlayers,
    string WorkerId,
    string WorkerDisplayName,
    string WorkerBaseUrl,
    int GameServerPid
);
record RoomPatchRequestDto(string? SessionName, string? Status, int? PlayerCount);

class RoomSnapshot
{
    public string SessionName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string HostNickname { get; set; } = string.Empty;
    public string Status { get; set; } = "waiting";
    public int PlayerCount { get; set; }
    public int MaxPlayers { get; set; }
}

class RunningRoom
{
    public required Process Process { get; set; }
    public required RoomSnapshot Snapshot { get; set; }
    public bool RoomApiCreated { get; set; }
    public bool CleanupStarted { get; set; }
    public string? PendingStatus { get; set; }
    public int? PendingPlayerCount { get; set; }
}
