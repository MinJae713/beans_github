using System;

namespace Colosseum.Network
{
    [Serializable]
    public class RoomSnapshot
    {
        public string SessionName;
        public string RoomName;
        public string HostNickname;
        public string Status;
        public int PlayerCount;
        public int MaxPlayers;
    }
}
