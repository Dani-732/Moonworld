using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    public static class MoonWorldDebugActions
    {
        [DebugAction("MoonWorld/敌方测试", "在鼠标处部署本届敌对主从", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DeployEnemyWarParty()
        {
            string rejection;
            if (!EnemyWarPartyService.TryDeploy(Find.CurrentMap, UI.MouseCell(), out rejection))
            {
                Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
                return;
            }
            HolyGrailWarEntry entry = Current.Game.GetComponent<GameComponent_MoonWorld>().CurrentWarEntry;
            Messages.Message(entry.EnemyIdentity.warClass + " 阵营主从已进入战场。",
                entry.EnemyServant, MessageTypeDefOf.ThreatBig, false);
        }

        [DebugAction("MoonWorld/敌方测试", "选中敌方从者：触发一次战败", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DefeatEnemyServant()
        {
            Pawn servant = Find.Selector.SingleSelectedThing as Pawn;
            if (EnemyContractUtility.HasEnemyContract(servant))
                ServantLifecycleService.Instance.TryResolveDefeat(servant);
        }

        [DebugAction("MoonWorld", "将选中殖民者设为御主", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void MakeSelectedPawnMaster()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer)
            {
                Messages.Message("请先选中一名玩家阵营殖民者。", MessageTypeDefOf.RejectInput, false);
                return;
            }
            HolyGrailWarEntryService.PrepareStartingCircuit(pawn);
            string rejection;
            if (HolyGrailWarEntryService.TryAccept(pawn, out rejection))
                Messages.Message(pawn.LabelShortCap + " 已接受本届圣杯战争邀请。", pawn, MessageTypeDefOf.PositiveEvent, false);
            else
                Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("MoonWorld", "在鼠标处召唤测试从者", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void SummonTestServant()
        {
            Pawn master = Find.Selector.SingleSelectedThing as Pawn;
            if (!MasterCircuitUtility.HasCircuit(master))
            {
                Messages.Message("请先选中拥有魔力回路的御主。", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Map map = Find.CurrentMap;
            IntVec3 cell = UI.MouseCell();
            if (map == null || !cell.InBounds(map) || !cell.Standable(map))
            {
                Messages.Message("请选择地图上的可站立位置。", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Pawn servant;
            string rejection;
            if (!ServantSummoningService.Instance.TrySummon(master, map, cell, out servant, out rejection))
            {
                Messages.Message(rejection, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Messages.Message("从者已完成召唤并加入殖民地，本届常规召唤资格已用尽。", servant, MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("MoonWorld/魔力测试", "选中御主：魔力回满", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FillSelectedMasterPrana()
        {
            Pawn master;
            if (!TryGetSelectedMaster(out master))
            {
                return;
            }

            Need_MasterPrana prana = master.needs.TryGetNeed<Need_MasterPrana>();
            prana.CurLevel = prana.MaxLevel;
            Messages.Message("御主魔力已回满。", master, MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("MoonWorld/魔力测试", "选中御主：魔力-10", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DrainSelectedMasterPrana()
        {
            Pawn master;
            if (!TryGetSelectedMaster(out master))
            {
                return;
            }

            Need_MasterPrana prana = master.needs.TryGetNeed<Need_MasterPrana>();
            prana.CurLevel = Mathf.Max(0f, prana.CurLevel - 10f);
            Messages.Message("御主魔力减少了 10。", master, MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("MoonWorld/魔力测试", "选中从者：魔力回满", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void FillSelectedServantPrana()
        {
            Pawn servant;
            if (!TryGetSelectedServant(out servant))
            {
                return;
            }

            Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
            prana.CurLevel = prana.MaxLevel;
            Messages.Message("从者魔力已回满。", servant, MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("MoonWorld/魔力测试", "选中从者：魔力+10", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void BoostSelectedServantPrana()
        {
            Pawn servant;
            if (!TryGetSelectedServant(out servant))
            {
                return;
            }

            Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
            prana.CurLevel = Mathf.Min(prana.MaxLevel, prana.CurLevel + 10f);
            Messages.Message("从者魔力增加了 10。", servant, MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("MoonWorld/魔力测试", "选中从者：魔力-10", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void DrainSelectedServantPrana()
        {
            Pawn servant;
            if (!TryGetSelectedServant(out servant))
            {
                return;
            }

            Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
            prana.CurLevel = Mathf.Max(0f, prana.CurLevel - 10f);
            Messages.Message("从者魔力减少了 10。", servant, MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("MoonWorld/魔力测试", "选中从者：魔力清空", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void EmptySelectedServantPrana()
        {
            Pawn servant;
            if (!TryGetSelectedServant(out servant))
            {
                return;
            }

            Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
            prana.CurLevel = 0f;
            Messages.Message("从者魔力已清空。", servant, MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("MoonWorld/魔力测试", "立即执行一次魔力结算", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ExecutePranaCycleNow()
        {
            int interval = Mathf.Max(1, MW_DefOf.MW_HolyGrailWarSettings.pranaUpdateIntervalTicks);
            PranaCycleService.Execute(interval);
            Messages.Message("已执行一次魔力结算。", MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("MoonWorld/战败测试", "选中从者：施加普通致命伤", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void ApplyFatalDamageToSelectedServant()
        {
            Pawn servant;
            if (!TryGetSelectedServant(out servant) || !ServantQuery.Instance.IsMaterialized(servant))
            {
                return;
            }

            BodyPartRecord brain = servant.health.hediffSet.GetBrain();
            BodyPartRecord target = brain ?? servant.RaceProps.body.corePart;
            servant.TakeDamage(new DamageInfo(DamageDefOf.Cut, 99999f, 999f, -1f, null, target));
        }

        [DebugAction("MoonWorld/战败测试", "选中从者：直接处决（应死亡）", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void KillSelectedServantDirectly()
        {
            Pawn servant;
            if (TryGetSelectedServant(out servant))
            {
                servant.Kill(null);
            }
        }

        private static bool TryGetSelectedMaster(out Pawn master)
        {
            master = Find.Selector.SingleSelectedThing as Pawn;
            if (master == null || !MasterCircuitUtility.HasCircuit(master))
            {
                Messages.Message("请先选中拥有魔力回路的御主。", MessageTypeDefOf.RejectInput, false);
                master = null;
                return false;
            }

            MasterCircuitUtility.EnsureMasterPranaNeed(master);
            Need_MasterPrana prana = master.needs.TryGetNeed<Need_MasterPrana>();
            if (prana == null)
            {
                Messages.Message("选中的御主没有御主魔力 Need。", MessageTypeDefOf.RejectInput, false);
                master = null;
                return false;
            }
            return true;
        }

        private static bool TryGetSelectedServant(out Pawn servant)
        {
            servant = Find.Selector.SingleSelectedThing as Pawn;
            if (servant == null || !ServantQuery.Instance.IsServant(servant))
            {
                Messages.Message("请先选中一名 MoonWorld 从者。", MessageTypeDefOf.RejectInput, false);
                servant = null;
                return false;
            }

            Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
            if (prana == null)
            {
                Messages.Message("选中的从者没有从者魔力 Need。", MessageTypeDefOf.RejectInput, false);
                servant = null;
                return false;
            }
            return true;
        }
    }
}
