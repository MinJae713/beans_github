using System.Reflection;

[assembly: AssemblyMetadata(
    "Colosseum.FusionUnsafeBoundary",
    "allowUnsafeCode is enabled only because Fusion IL Weaver generated NetworkBehaviour accessors use Fusion.NetworkBehaviour.Ptr in this assembly.")]

[assembly: AssemblyMetadata(
    "Colosseum.FusionUnsafeRegression",
    "With allowUnsafeCode disabled, Build Both can pass but non-DevMode 2-client gameplay fails at runtime with FieldAccessException in Gun.IsReloading and RoomManager.CurrentRoomIndex.")]

[assembly: AssemblyMetadata(
    "Colosseum.FusionUnsafeGuardrail",
    "Hand-written unsafe blocks, pointer arithmetic, and unmanaged memory access are not permitted here without a separate reviewed issue and smaller dedicated assembly.")]
