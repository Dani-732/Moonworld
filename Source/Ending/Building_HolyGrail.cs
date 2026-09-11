using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class Building_HolyGrail : Building
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn sel)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(sel))
                yield return option;

            if (sel == null || sel.Faction != Faction.OfPlayer)
                yield break;

            GameComponent_MoonWorld state = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            if (state == null || state.CurrentWarOutcome != WarOutcome.PlayerVictory)
            {
                yield return new FloatMenuOption("圣杯尚未回应胜利者", null);
                yield break;
            }

            yield return new FloatMenuOption("圣杯：许愿财富", () => HolyGrailEndingService.TryWishWealth(this, sel));
            yield return new FloatMenuOption("圣杯：许愿从者实体化", () => HolyGrailEndingService.TryWishMaterialization(this, sel));
        }
    }
}
