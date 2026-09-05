using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class ScenPart_HolyGrailWar : ScenPart
    {
        public override void PreMapGenerate()
        {
            GameInitData data = Find.GameInitData;
            if (data == null || data.startingPawnCount < 1 || data.startingAndOptionalPawns.Count == 0) return;
            HolyGrailWarEntryService.PrepareStartingCircuit(data.startingAndOptionalPawns[0]);
        }

        public override void PostGameStart()
        {
            ChoiceLetter_HolyGrailWar.Offer(expires: false);
        }
    }
}
