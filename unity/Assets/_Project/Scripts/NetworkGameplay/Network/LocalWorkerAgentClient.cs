using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace Colosseum.Network
{
    /// <summary>
    /// 현재 단계의 하드코딩 Worker Agent.
    /// 외부 API 서버 없이 같은 머신에서 GameServer 프로세스를 실행하고,
    /// stdout 구조화 마커를 RoomRegistry에 반영한다.
    /// </summary>
    public class LocalWorkerAgentClient : IWorkerAgentClient
    {
        private readonly string _configuredExePath;
        private readonly IRoomRegistry _roomRegistry;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logError;
        private readonly Dictionary<string, Process> _processes = new();

        public LocalWorkerAgentClient(
            string configuredExePath,
            IRoomRegistry roomRegistry,
            Action<string> logInfo,
            Action<string> logError)
        {
            _configuredExePath = configuredExePath;
            _roomRegistry = roomRegistry;
            _logInfo = logInfo;
            _logError = logError;
        }

        public Task<SpawnRoomResponse> SpawnRoomAsync(WorkerInfo worker, SpawnRoomRequest request)
        {
            if (worker == null || !worker.Enabled)
            {
                return Task.FromResult(new SpawnRoomResponse
                {
                    Ok = false,
                    ErrorCode = "WORKER_DISABLED"
                });
            }

            string exePath = _configuredExePath;
            if (string.IsNullOrEmpty(exePath))
                exePath = Environment.GetCommandLineArgs()[0];

            // 알려진 씬만 허용 (CLI 인자 인젝션 차단)
            string safeScene = request.SceneName;
            if (safeScene != "SampleScene" && safeScene != "MVPArena" && safeScene != "DuskCity")
                safeScene = "MVPArena";

            string arguments =
                $"-logFile - --mode game --session \"{request.SessionName}\" --room-name \"{request.RoomName}\" --max-players {request.MaxPlayers} --scene \"{safeScene}\"";

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                var process = Process.Start(startInfo);
                if (process == null)
                {
                    return Task.FromResult(new SpawnRoomResponse
                    {
                        Ok = false,
                        ErrorCode = "ROOM_CREATE_FAILED"
                    });
                }

                _processes[request.SessionName] = process;
                _roomRegistry.Register(new RoomSnapshot
                {
                    SessionName = request.SessionName,
                    RoomName = request.RoomName,
                    HostNickname = request.HostNickname ?? string.Empty,
                    Status = "waiting",
                    PlayerCount = 0,
                    MaxPlayers = request.MaxPlayers
                });

                string capturedSessionName = request.SessionName;
                string capturedRoomName = request.RoomName;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;

                    if (e.Data.StartsWith("##STATUS:"))
                    {
                        _roomRegistry.UpdateStatus(capturedSessionName, e.Data.Substring(9));
                        _logInfo?.Invoke($"[WorkerAgent] Room '{capturedRoomName}' status → {e.Data.Substring(9)}");
                        return;
                    }

                    if (e.Data.StartsWith("##PLAYERS:"))
                    {
                        if (int.TryParse(e.Data.Substring(10), out int count))
                            _roomRegistry.UpdatePlayerCount(capturedSessionName, count);
                        return;
                    }

                    _logInfo?.Invoke($"[GameServer:{capturedRoomName}] {e.Data}");
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        _logError?.Invoke($"[GameServer:{capturedRoomName}] {e.Data}");
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _logInfo?.Invoke(
                    $"[WorkerAgent] Spawned game server: {request.RoomName} (session: {request.SessionName}, PID: {process.Id}, worker: {worker.DisplayName})");

                return Task.FromResult(new SpawnRoomResponse
                {
                    Ok = true,
                    ErrorCode = null
                });
            }
            catch (Exception e)
            {
                _logError?.Invoke($"[WorkerAgent] Process.Start failed: {e.Message}");
                return Task.FromResult(new SpawnRoomResponse
                {
                    Ok = false,
                    ErrorCode = "ROOM_CREATE_FAILED"
                });
            }
        }

        public void Tick()
        {
            var deadSessions = new List<string>();
            foreach (var kvp in _processes)
            {
                try
                {
                    if (kvp.Value.HasExited)
                    {
                        deadSessions.Add(kvp.Key);
                        _logInfo?.Invoke($"[WorkerAgent] Game server exited: {kvp.Key} (exit code: {kvp.Value.ExitCode})");
                    }
                }
                catch (Exception)
                {
                    deadSessions.Add(kvp.Key);
                }
            }

            foreach (var sessionName in deadSessions)
            {
                if (_processes.TryGetValue(sessionName, out var process))
                {
                    process.Dispose();
                    _processes.Remove(sessionName);
                }
                _roomRegistry.Remove(sessionName);
            }
        }

        public void ShutdownAll()
        {
            foreach (var kvp in _processes)
            {
                try
                {
                    if (!kvp.Value.HasExited)
                    {
                        kvp.Value.Kill();
                        _logInfo?.Invoke($"[WorkerAgent] Killed game server process: {kvp.Key}");
                    }
                }
                catch (Exception e)
                {
                    _logError?.Invoke($"[WorkerAgent] Failed to kill process {kvp.Key}: {e.Message}");
                }
            }

            foreach (var process in _processes.Values)
                process.Dispose();
            _processes.Clear();
        }
    }
}
