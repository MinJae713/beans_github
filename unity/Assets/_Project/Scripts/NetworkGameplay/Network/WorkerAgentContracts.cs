using System.Threading.Tasks;

namespace Colosseum.Network
{
    public class WorkerInfo
    {
        public string WorkerId;
        public string DisplayName;
        public string BaseUrl;
        public bool Enabled = true;
    }

    public class SpawnRoomRequest
    {
        public string SessionName;
        public string RoomName;
        public string HostNickname;
        public int MaxPlayers;
    }

    public class SpawnRoomResponse
    {
        public bool Ok;
        public string ErrorCode;
    }

    public interface IWorkerSelector
    {
        WorkerInfo SelectWorker();
    }

    public interface IWorkerAgentClient
    {
        Task<SpawnRoomResponse> SpawnRoomAsync(WorkerInfo worker, SpawnRoomRequest request);
        void Tick();
        void ShutdownAll();
    }
}
