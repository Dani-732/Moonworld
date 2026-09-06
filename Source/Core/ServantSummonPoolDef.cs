using System.Collections.Generic;
using Verse;

namespace MoonWorld
{
    public sealed class ServantSummonPoolDef : Def
    {
        public HolyGrailWarClass warClass;
        public List<ServantIdentityDef> servants = new List<ServantIdentityDef>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (!HolyGrailWarClassUtility.IsWarClass(warClass)) yield return "召唤池必须指定七种正式职阶之一。";
            if (servants == null) yield break;
            foreach (ServantIdentityDef identity in servants)
                if (identity == null || identity.warClass != warClass)
                    yield return "召唤池中的英灵不能为空，且必须属于该池职阶。";
        }

        public static ServantIdentityDef Pick(HolyGrailWarClass excludedClass = HolyGrailWarClass.None)
        {
            var classes = new List<HolyGrailWarClass>();
            var pools = new Dictionary<HolyGrailWarClass, List<ServantIdentityDef>>();
            foreach (ServantSummonPoolDef pool in DefDatabase<ServantSummonPoolDef>.AllDefsListForReading)
            {
                if (!HolyGrailWarClassUtility.IsWarClass(pool.warClass)
                    || pool.warClass == excludedClass || pool.servants == null) continue;
                foreach (ServantIdentityDef identity in pool.servants)
                {
                    if (identity == null || !identity.summonable || identity.warClass != pool.warClass
                        || identity.servantKind?.race == null) continue;
                    List<ServantIdentityDef> candidates;
                    if (!pools.TryGetValue(pool.warClass, out candidates))
                    {
                        candidates = new List<ServantIdentityDef>();
                        pools.Add(pool.warClass, candidates);
                        classes.Add(pool.warClass);
                    }
                    // Extension tables for one class and repeated references never add class weight.
                    if (!candidates.Contains(identity)) candidates.Add(identity);
                }
            }
            if (classes.Count == 0) return null;
            return pools[classes.RandomElement()].RandomElement();
        }
    }
}
