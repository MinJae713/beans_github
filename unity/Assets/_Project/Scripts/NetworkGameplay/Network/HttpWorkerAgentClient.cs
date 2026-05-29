using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Colosseum.Contracts;
using UnityEngine;

namespace Colosseum.Network
{
    /// <summary>
    /// Lobby -> Worker Agent / Spring Room API HTTP 클라이언트.
    /// Worker Agent에는 방 spawn 요청을 보내고,
    /// Spring Room API 에서는 중앙 Room Registry 목록을 조회한다.
    /// </summary>
    public class HttpWorkerAgentClient : IWorkerAgentClient
    {
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logError;
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _roomApiBaseUrl;
        private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan SpawnRequestTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan RoomListRequestTimeout = TimeSpan.FromSeconds(5);

        public HttpWorkerAgentClient(
            string roomApiBaseUrl,
            Action<string> logInfo,
            Action<string> logError)
        {
            _roomApiBaseUrl = roomApiBaseUrl?.TrimEnd('/');
            _logInfo = logInfo;
            _logError = logError;
            _httpClient.Timeout = DefaultHttpTimeout;
        }

        public async Task<SpawnRoomResponse> SpawnRoomAsync(WorkerInfo worker, SpawnRoomRequest request)
        {
            if (worker == null || !worker.Enabled || string.IsNullOrEmpty(worker.BaseUrl))
            {
                return new SpawnRoomResponse
                {
                    Ok = false,
                    ErrorCode = "WORKER_DISABLED"
                };
            }

            try
            {
                string url = worker.BaseUrl.TrimEnd('/') + "/workers/spawn-room";
                var payload = new SpawnRoomRequestPayload
                {
                    sessionName = request.SessionName,
                    roomName = request.RoomName,
                    hostNickname = request.HostNickname ?? string.Empty,
                    maxPlayers = request.MaxPlayers,
                    sceneName = request.SceneName ?? SelectableSceneExtensions.DefaultSceneName
                };
                string json = JsonUtility.ToJson(payload);
                
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var cts = new CancellationTokenSource(SpawnRequestTimeout);
                using var response = await _httpClient.PostAsync(url, content, cts.Token);
                string body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logError?.Invoke($"[WorkerAgent/HTTP] Spawn failed: HTTP {(int)response.StatusCode}");
                    return new SpawnRoomResponse { Ok = false, ErrorCode = "ROOM_CREATE_FAILED" };
                }

                bool ok = body.IndexOf("\"ok\":true", StringComparison.OrdinalIgnoreCase) >= 0;
                string errorCode = ExtractJsonString(body, "errorCode");

                if (ok)
                    _logInfo?.Invoke($"[WorkerAgent/HTTP] Spawn request accepted for room '{request.RoomName}'");

                return new SpawnRoomResponse
                {
                    Ok = ok,
                    ErrorCode = ok ? null : (string.IsNullOrEmpty(errorCode) ? "ROOM_CREATE_FAILED" : errorCode)
                };
            }
            catch (Exception e)
            {
                _logError?.Invoke($"[WorkerAgent/HTTP] Spawn exception: {e.Message}");
                return new SpawnRoomResponse { Ok = false, ErrorCode = "ROOM_CREATE_FAILED" };
            }
        }

        public async Task<string> FetchRoomListJsonAsync()
        {
            if (string.IsNullOrEmpty(_roomApiBaseUrl))
            {
                _logError?.Invoke("[RoomApi/HTTP] Room API base URL is empty.");
                return "[]";
            }

            try
            {
                string url = _roomApiBaseUrl + "/rooms";
                using var cts = new CancellationTokenSource(RoomListRequestTimeout);
                using var response = await _httpClient.GetAsync(url, cts.Token);
                response.EnsureSuccessStatusCode();
                string body = await response.Content.ReadAsStringAsync();
                return ConvertRoomApiResponseToLobbyJson(body);
            }
            catch (Exception e)
            {
                _logError?.Invoke($"[RoomApi/HTTP] Fetch room list failed: {e.Message}");
                return "[]";
            }
        }

        public void ShutdownAll()
        {
            _httpClient.Dispose();
        }

        public void Tick() { }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ExtractJsonString(string json, string key)
        {
            string search = $"\"{key}\":\"";
            int start = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += search.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private static int ExtractJsonInt(string json, string key, int defaultValue = 0)
        {
            string search = $"\"{key}\":";
            int start = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return defaultValue;
            start += search.Length;

            int end = start;
            while (end < json.Length && json[end] != ',' && json[end] != '}')
                end++;

            if (int.TryParse(json.Substring(start, end - start).Trim(), out int result))
                return result;
            return defaultValue;
        }

        private static string ConvertRoomApiResponseToLobbyJson(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "[]")
                return "[]";

            var sb = new StringBuilder("[");
            bool first = true;
            int i = 0;
            while (i < json.Length)
            {
                int start = json.IndexOf('{', i);
                if (start < 0) break;
                int end = json.IndexOf('}', start);
                if (end < 0) break;

                string obj = json.Substring(start, end - start + 1);
                string sessionName = ExtractJsonString(obj, "sessionName") ?? string.Empty;
                string roomName = ExtractJsonString(obj, "roomName") ?? sessionName;
                string hostNickname = ExtractJsonString(obj, "hostNickname") ?? string.Empty;
                string status = ExtractJsonString(obj, "status") ?? "waiting";
                int playerCount = ExtractJsonInt(obj, "playerCount");
                int maxPlayers = ExtractJsonInt(obj, "maxPlayers", 2);

                string roomNameEscaped = Escape(roomName);
                string hostEscaped = Escape(hostNickname);

                if (!first) sb.Append(",");
                sb.Append($"{{\"name\":\"{roomNameEscaped}\",\"session\":\"{sessionName}\"," +
                    $"\"host\":\"{hostEscaped}\"," +
                    $"\"players\":{playerCount},\"max\":{maxPlayers}," +
                    $"\"status\":\"{status}\"}}");
                first = false;

                i = end + 1;
            }

            sb.Append("]");
            return sb.ToString();
        }
    }

    [Serializable]
    internal class SpawnRoomRequestPayload
    {
        public string sessionName;
        public string roomName;
        public string hostNickname;
        public int maxPlayers;
        public string sceneName;
    }
}
