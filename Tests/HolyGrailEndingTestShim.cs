using Verse;

namespace MoonWorld
{
    // The lifecycle and war-startup hosts exercise callers without loading Unity map APIs.
    // Production behavior is checked by the dedicated HolyGrailEndingPolicy tests and runtime contract check.
    internal static class HolyGrailEndingService
    {
        internal static void OnVictory(GameComponent_MoonWorld state) { }
        internal static void Tick(GameComponent_MoonWorld state) { }
        internal static bool IsPermanentlyMaterialized(Pawn servant) { return false; }
    }
}
