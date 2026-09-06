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
        public static ServantIdentityDef PickOpponent(ServantIdentityDef player)
        {
            var candidates = ServantSummonPoolDef.Candidates();
            var seat = HolyGrailWarClassDef.For(player);
            if (seat == null) return null;
            candidates.Remove(seat);
            var seats = new List<HolyGrailWarClassDef>(candidates.Keys);
            return seats.Count == 0 ? null : candidates[seats.RandomElement()].RandomElement();
        }
    }
}
