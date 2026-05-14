using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Colosseum.Game
{
    public enum CameraSlideMode { Static = 0, Wide = 1 }

    [Serializable]
    public class RoomData : ISerializationCallbackReceiver
    {
        public string roomName = "Room";
        public float width = 16f;
        public float cameraY = 0f;
        public float cameraSize = 5f;

        [FormerlySerializedAs("isWideRoom")]
        [HideInInspector]
        [SerializeField] private bool _legacyIsWideRoom;

        [SerializeField] private CameraSlideMode _cameraSlideMode = CameraSlideMode.Static;

        [HideInInspector] public float leftBound;
        [HideInInspector] public float rightBound;
        [HideInInspector] public float centerX;

        public CameraSlideMode cameraSlideMode => _cameraSlideMode;

        [Obsolete("Use cameraSlideMode == CameraSlideMode.Wide instead.")]
        public bool isWideRoom => _cameraSlideMode == CameraSlideMode.Wide;

        public void OnBeforeSerialize() { /* no-op */ }

        public void OnAfterDeserialize()
        {
            if (_legacyIsWideRoom)
            {
                _cameraSlideMode = CameraSlideMode.Wide;
                _legacyIsWideRoom = false;
            }
        }
    }
}
