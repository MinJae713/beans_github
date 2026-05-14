using System;
using System.Diagnostics;

namespace Colosseum.Contracts
{
    public static class VfxEventBus
    {
        public static event Action<VfxEventPayload> Published;
        public static event Action<VfxEventPayload, Exception> HandlerFailed;

        public static void Publish(VfxEventPayload payload)
        {
            var handlers = Published;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<VfxEventPayload> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(payload);
                }
                catch (Exception ex)
                {
                    NotifyHandlerFailed(payload, ex);
                    Debug.WriteLine($"[Colosseum/VFX] Presentation handler failed: {ex.Message}");
                }
            }
        }

        private static void NotifyHandlerFailed(VfxEventPayload payload, Exception exception)
        {
            var handlers = HandlerFailed;
            if (handlers == null)
            {
                return;
            }

            foreach (Action<VfxEventPayload, Exception> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(payload, exception);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Colosseum/VFX] Failure handler failed: {ex.Message}");
                }
            }
        }
    }
}
