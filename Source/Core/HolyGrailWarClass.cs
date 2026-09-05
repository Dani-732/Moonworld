using System.Collections.Generic;
using Verse;

namespace MoonWorld
{
    public enum HolyGrailWarClass
    {
        None,
        Saber,
        Archer,
        Lancer,
        Assassin,
        Caster,
        Rider,
        Berserker
    }

    public static class HolyGrailWarClassUtility
    {
        public static HolyGrailWarClass Opponent(HolyGrailWarClass playerClass)
        {
            if (playerClass == HolyGrailWarClass.Saber) return HolyGrailWarClass.Archer;
            if (playerClass == HolyGrailWarClass.Archer) return HolyGrailWarClass.Saber;
            return HolyGrailWarClass.None;
        }

        public static ServantIdentityDef PickOpponent(ServantIdentityDef player)
        {
            HolyGrailWarClass opponent = Opponent(player?.warClass ?? HolyGrailWarClass.None);
            if (opponent == HolyGrailWarClass.None) return null;
            List<ServantIdentityDef> candidates = new List<ServantIdentityDef>();
            foreach (ServantIdentityDef identity in DefDatabase<ServantIdentityDef>.AllDefsListForReading)
                if (identity.summonable && identity.warClass == opponent
                    && identity.servantKind?.race != null) candidates.Add(identity);
            return candidates.Count == 0 ? null : candidates.RandomElement();
        }
    }
}
