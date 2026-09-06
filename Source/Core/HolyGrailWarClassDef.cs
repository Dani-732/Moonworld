using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    // Public XML extension contract. defName is the stable seat identifier, independent of its pawn.
    public sealed class HolyGrailWarClassDef : Def
    {
        public HolyGrailWarClass legacyClass;
        public FactionDef oppositionFaction;
        public bool participatesInWar = true;
        public System.Type workshopRetreatPolicy = typeof(RetreatAfterServantDefeat);
        public int workshopRebuildDelayTicks = 180000;
        private WorkshopRetreatPolicy retreatPolicy;
        public WorkshopRetreatPolicy RetreatPolicy => retreatPolicy ??
            (retreatPolicy = (WorkshopRetreatPolicy)System.Activator.CreateInstance(workshopRetreatPolicy));

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (workshopRebuildDelayTicks < 0) yield return "工坊重建等待时间不能为负数。";
            if (workshopRetreatPolicy == null || workshopRetreatPolicy.IsAbstract
                || !typeof(WorkshopRetreatPolicy).IsAssignableFrom(workshopRetreatPolicy)
                || workshopRetreatPolicy.GetConstructor(System.Type.EmptyTypes) == null)
                yield return "工坊撤退策略必须继承 WorkshopRetreatPolicy 并提供公开无参构造函数。";
            if (oppositionFaction == null) yield return "职阶必须配置独立的敌方派系。";
            else if (!oppositionFaction.hidden || !oppositionFaction.permanentEnemy || !oppositionFaction.raidsForbidden)
                yield return "职阶派系必须隐藏、永久敌对且关闭原版随机突袭。";
            foreach (var other in DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading)
                if (other != this && (other.oppositionFaction == oppositionFaction
                    || (legacyClass != HolyGrailWarClass.None && other.legacyClass == legacyClass)))
                    yield return "职阶不能复用其他职阶的派系或旧职阶映射。";
        }

        public static HolyGrailWarClassDef Resolve(HolyGrailWarClass legacy)
        {
            if (legacy == HolyGrailWarClass.None) return null;
            foreach (var seat in DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading)
                if (seat.legacyClass == legacy) return seat;
            return null;
        }

        public static HolyGrailWarClassDef For(ServantIdentityDef identity)
        { return identity == null ? null : identity.classDef ?? Resolve(identity.warClass); }
    }
}
