using System.Collections.Generic;

namespace Colosseum.Network
{
    public interface IRoomRegistry
    {
        void Register(RoomSnapshot snapshot);
        void UpdateStatus(string sessionName, string status);
        void UpdatePlayerCount(string sessionName, int playerCount);
        void Remove(string sessionName);
        void ReplaceAll(List<RoomSnapshot> snapshots);
        List<RoomSnapshot> GetAll();
    }

    public class InMemoryRoomRegistry : IRoomRegistry
    {
        private readonly Dictionary<string, RoomSnapshot> _rooms = new();
        private readonly object _lock = new();

        public void Register(RoomSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrEmpty(snapshot.SessionName))
                return;

            lock (_lock)
            {
                _rooms[snapshot.SessionName] = Clone(snapshot);
            }
        }

        public void UpdateStatus(string sessionName, string status)
        {
            lock (_lock)
            {
                if (_rooms.TryGetValue(sessionName, out var snapshot))
                    snapshot.Status = status;
            }
        }

        public void UpdatePlayerCount(string sessionName, int playerCount)
        {
            lock (_lock)
            {
                if (_rooms.TryGetValue(sessionName, out var snapshot))
                    snapshot.PlayerCount = playerCount;
            }
        }

        public void Remove(string sessionName)
        {
            if (string.IsNullOrEmpty(sessionName))
                return;

            lock (_lock)
            {
                _rooms.Remove(sessionName);
            }
        }

        public void ReplaceAll(List<RoomSnapshot> snapshots)
        {
            lock (_lock)
            {
                _rooms.Clear();
                if (snapshots == null)
                    return;

                foreach (var snapshot in snapshots)
                {
                    if (snapshot == null || string.IsNullOrEmpty(snapshot.SessionName))
                        continue;

                    _rooms[snapshot.SessionName] = Clone(snapshot);
                }
            }
        }

        public List<RoomSnapshot> GetAll()
        {
            lock (_lock)
            {
                var result = new List<RoomSnapshot>(_rooms.Count);
                foreach (var room in _rooms.Values)
                    result.Add(Clone(room));
                return result;
            }
        }

        private static RoomSnapshot Clone(RoomSnapshot snapshot)
        {
            return new RoomSnapshot
            {
                SessionName = snapshot.SessionName,
                RoomName = snapshot.RoomName,
                HostNickname = snapshot.HostNickname,
                Status = snapshot.Status,
                PlayerCount = snapshot.PlayerCount,
                MaxPlayers = snapshot.MaxPlayers
            };
        }
    }
}
