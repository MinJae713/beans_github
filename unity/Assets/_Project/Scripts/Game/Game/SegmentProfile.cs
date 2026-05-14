using System;
using UnityEngine;

namespace Colosseum.Game
{
    public enum SegmentRole
    {
        VictoryRun = 0,
        Combat = 1,
    }

    public enum PlaceholderKind
    {
        Floor = 0,
        Wall = 1,
        Cover = 2,
        ReflectWall = 3,
        SightBlocker = 4,
        FallZone = 5,
        PitTrigger = 6,
        GenericCollider = 7,
    }

    [Serializable]
    public struct SegmentEntry
    {
        public int index;
        public SegmentRole role;
        public float leftBoundX;
        public float rightBoundX;
        public bool hasMultiFloor;
    }

    [Serializable]
    public struct PlaceholderEntry
    {
        public string entryName;
        public int segmentIndex;
        public PlaceholderKind kind;
        public Vector2 worldPosition;
        public Vector2 size;
        public float floorLayer;
    }

    [CreateAssetMenu(fileName = "SegmentProfile", menuName = "Colosseum/Segment Profile", order = 0)]
    public class SegmentProfile : ScriptableObject
    {
        [Header("Width Ratio (spec 02 §2.1)")]
        public float segmentWidthRatio = 5f;
        public float viewportWidthRatio = 2f;

        [Header("Vertical Floor Structure (spec 02 §2.5.1, D68)")]
        public float floor1Y = 0f;
        public float floorUnit = 2f;

        [Header("Jump Limit (spec 02 §2.5.2, D69)")]
        public float jumpLimitFloors = 1.5f;

        [Header("Camera (spec 02 §2.1)")]
        public float cameraOrthographicSize = 5f;
        public float cameraLerpSpeed = 5f;

        [Header("Segments (S1..S7)")]
        public SegmentEntry[] segments;

        [Header("Placeholders (Phase 1 plan v4 §3 작업 1.3)")]
        public PlaceholderEntry[] placeholders;

        public bool TryGetSegment(int index, out SegmentEntry entry)
        {
            if (segments != null)
            {
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i].index == index)
                    {
                        entry = segments[i];
                        return true;
                    }
                }
            }
            entry = default;
            return false;
        }
    }
}
