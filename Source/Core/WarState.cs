using UnityEngine;
using Verse;

namespace MoonWorld
{
    public sealed class GameComponent_MoonWorld : GameComponent
    {
        public int warStartTick = -1;

        public GameComponent_MoonWorld(Game game) { }

        public override void GameComponentTick()
        {
            int interval = Mathf.Max(1, MW_DefOf.MW_HolyGrailWarSettings.pranaUpdateIntervalTicks);
            if (Find.TickManager.TicksGame % interval == 0)
            {
                PranaCycleService.Execute(interval);
            }
        }

        public void RecordWarStartIfNeeded()
        {
            if (warStartTick < 0)
            {
                warStartTick = Find.TickManager.TicksGame;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref warStartTick, "warStartTick", -1);
        }
    }
}
