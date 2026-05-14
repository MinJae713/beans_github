using Fusion;
using UnityEngine;

namespace Colosseum.Network
{
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 Direction;
        public NetworkBool IsJumpPressed;
        public NetworkBool IsFirePressed;
        public Vector2 AimDirection;
    }
}
