namespace Colosseum.Contracts
{
    public readonly struct VfxEventPayload
    {
        public readonly VfxEventType EventType;
        public readonly string CardKey;
        public readonly string EffectKey;
        public readonly int SourceTick;
        public readonly int SourceObjectId;
        public readonly int SourceEventSequence;
        public readonly VfxPredictionState PredictionState;
        public readonly int SourcePlayerSlot;
        public readonly int TargetPlayerSlot;
        public readonly VfxVector2 WorldPosition;
        public readonly VfxVector2 Direction;
        public readonly float RadiusHint;
        public readonly float DurationHint;
        public readonly float MagnitudeHint;
        public readonly VfxAttachPoint AttachPoint;

        public VfxEventPayload(
            VfxEventType eventType,
            string cardKey,
            string effectKey,
            int sourceTick,
            int sourceObjectId,
            int sourceEventSequence,
            VfxPredictionState predictionState,
            int sourcePlayerSlot,
            int targetPlayerSlot,
            VfxVector2 worldPosition,
            VfxVector2 direction,
            float radiusHint,
            float durationHint,
            float magnitudeHint,
            VfxAttachPoint attachPoint)
        {
            EventType = eventType;
            CardKey = cardKey ?? string.Empty;
            EffectKey = effectKey ?? string.Empty;
            SourceTick = sourceTick;
            SourceObjectId = sourceObjectId;
            SourceEventSequence = sourceEventSequence;
            PredictionState = predictionState;
            SourcePlayerSlot = sourcePlayerSlot;
            TargetPlayerSlot = targetPlayerSlot;
            WorldPosition = worldPosition;
            Direction = direction;
            RadiusHint = radiusHint;
            DurationHint = durationHint;
            MagnitudeHint = magnitudeHint;
            AttachPoint = attachPoint;
        }

        public string GetDedupeKey()
        {
            if (SourceObjectId != 0 && SourceEventSequence != 0)
            {
                return $"{EventType}:{SourceObjectId}:{SourceEventSequence}:{EffectKey}:{CardKey}";
            }

            return $"{EventType}:{SourcePlayerSlot}:{TargetPlayerSlot}:{SourceTick}:{EffectKey}";
        }
    }
}
