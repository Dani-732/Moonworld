using HarmonyLib;
using Verse;

namespace MoonWorld
{
    [StaticConstructorOnStartup]
    public static class MoonWorldStartup
    {
        static MoonWorldStartup()
        {
            new Harmony("ly243.moonworld").PatchAll();
        }
    }
}
