using System.Collections.Generic;
using Verse;

namespace MoonWorld
{
    public sealed class ServantSummonPoolDef : Def
    {
        public HolyGrailWarClass warClass;
        public HolyGrailWarClassDef classDef;
        public HolyGrailWarClassDef Seat => classDef ?? HolyGrailWarClassDef.Resolve(warClass);
        public List<ServantIdentityDef> servants = new List<ServantIdentityDef>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (Seat == null) yield return "召唤池必须指定有效职阶 Def。";
            if (servants == null) yield break;
            foreach (ServantIdentityDef identity in servants)
                if (identity == null || HolyGrailWarClassDef.For(identity) != Seat)
                    yield return "召唤池中的英灵不能为空，且必须属于该池职阶。";
        }

        public static ServantIdentityDef Pick(HolyGrailWarClass excludedClass = HolyGrailWarClass.None)
        {
            var pools = Candidates();
            var classes = new List<HolyGrailWarClassDef>();
            foreach (var seat in pools.Keys)
                if (excludedClass == HolyGrailWarClass.None || seat.legacyClass != excludedClass) classes.Add(seat);
            if (classes.Count == 0) return null;
            return pools[classes.RandomElement()].RandomElement();
        }

        public static Dictionary<HolyGrailWarClassDef, List<ServantIdentityDef>> Candidates()
        {
            var pools = new Dictionary<HolyGrailWarClassDef, List<ServantIdentityDef>>();
            foreach (ServantSummonPoolDef pool in DefDatabase<ServantSummonPoolDef>.AllDefsListForReading)
            {
                HolyGrailWarClassDef seat = pool.Seat;
                if (seat == null || !seat.participatesInWar || pool.servants == null) continue;
                foreach (ServantIdentityDef identity in pool.servants)
                {
                    if (identity == null || !identity.summonable || HolyGrailWarClassDef.For(identity) != seat
                        || identity.servantKind?.race == null) continue;
                    List<ServantIdentityDef> candidates;
                    if (!pools.TryGetValue(seat, out candidates))
                    {
                        candidates = new List<ServantIdentityDef>();
                        pools.Add(seat, candidates);
                    }
                    // Extension tables for one class and repeated references never add class weight.
                    if (!candidates.Contains(identity)) candidates.Add(identity);
                }
            }
            return pools;
        }
    }
}
