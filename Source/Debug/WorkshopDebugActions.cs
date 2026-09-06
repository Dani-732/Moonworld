using System.Collections.Generic;
using System.Text;
using LudeonTK;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    public static class WorkshopDebugActions
    {
        [DebugAction("MoonWorld", "工坊调试：状态与重建", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        public static void Open()
        {
            var war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
            var options = new List<FloatMenuOption>();
            if (war?.CurrentWarEntry != null)
                foreach (var enemy in war.CurrentWarEntry.Enemies)
                {
                    EnemyWarParticipant selected = enemy;
                    string label = enemy.Seat?.label ?? "未知职阶";
                    options.Add(new FloatMenuOption(label + "：查看状态", () => Show(war, selected)));
                    options.Add(new FloatMenuOption(label + "：跳过时间并尝试重建", () =>
                    {
                        bool rebuilt = WorkshopRebuildService.TryRebuild(war, selected, out string reason, ignoreTime: true);
                        Show(war, selected, rebuilt ? "已使用原主从重建工坊。" : "重建未执行：" + reason);
                    }));
                }
            if (options.Count == 0) Find.WindowStack.Add(new Dialog_MessageBox("本届没有敌方阵营记录。"));
            else Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void Show(GameComponent_MoonWorld war, EnemyWarParticipant enemy, string result = null)
        {
            var text = new StringBuilder(result == null ? "工坊调试报告" : result);
            text.Append("\n职阶：").Append(enemy.Seat?.label).Append("；战争：").Append(war.CurrentWarOutcome)
                .Append("；当前 Tick：").Append(GenTicks.TicksAbs);
            AppendPawn(text, "御主", enemy.EnemyMaster);
            AppendPawn(text, "从者", enemy.EnemyServant);
            Pawn servant = enemy.EnemyServant;
            Need_Prana prana = servant?.needs?.TryGetNeed<Need_Prana>();
            text.Append("\n存在状态：").Append(servant?.TryGetComp<CompServantState>()?.PresenceState)
                .Append("；魔力：").Append(prana == null ? "无" : prana.CurLevel.ToString("F1") + "/" + prana.MaxLevel.ToString("F1"))
                .Append("\n场外供魔有效：").Append(EnemyContractUtility.IsResting(servant))
                .Append("；契约有效：").Append(EnemyContractUtility.HasEnemyContract(servant))
                .Append("\n最短休整剩余：").Append((EnemyRestUtility.TicksRemaining(servant) / 60000f).ToString("F2")).Append(" 天")
                .Append("；重建期限：").Append(enemy.WorkshopRebuildAtTickAbs);
            int count = 0;
            foreach (var worldObject in Find.WorldObjects.AllWorldObjects)
                if (worldObject is Site_WarWorkshop site && site.OwnerMaster == enemy.EnemyMaster && !site.Destroyed)
                {
                    count++;
                    text.Append("\n工坊 #").Append(site.ID).Append("：地块 ").Append(site.Tile).Append("；地图存在：").Append(site.HasMap)
                        .Append("\n战败记录：").Append(site.ServantDefeatedHere).Append("；撤退命令：").Append(site.RetreatOrdered)
                        .Append("；御主逃脱：").Append(site.MasterEscaped).Append("；从者逃脱：").Append(site.ServantEscaped);
                    if (site.HasMap)
                    {
                        bool removable = site.ShouldRemoveMapNow(out bool removeSite);
                        text.Append("\n地图允许卸载：").Append(removable).Append("；同时移除工坊：").Append(removeSite);
                    }
                }
            text.Append("\n现存工坊数：").Append(count)
                .Append("\n正常重建检查：").Append(WorkshopRebuildService.RebuildRejection(war, enemy) ?? "条件通过，等待周期选址。")
                .Append("\n跳过时间后检查：").Append(WorkshopRebuildService.RebuildRejection(war, enemy, ignoreTime: true) ?? "条件通过，可尝试选址。");
            string report = text.ToString();
            Log.Message("[MoonWorld][工坊调试] " + report);
            Find.WindowStack.Add(new Dialog_MessageBox(report, "复制报告", () => GUIUtility.systemCopyBuffer = report, "关闭"));
        }

        private static void AppendPawn(StringBuilder text, string label, Pawn pawn)
        {
            text.Append("\n").Append(label).Append("：");
            if (pawn == null) { text.Append("引用为空"); return; }
            text.Append(pawn.LabelShortCap).Append(" [").Append(pawn.ThingID).Append("]")
                .Append("\n地图：").Append(pawn.MapHeld?.uniqueID.ToString() ?? "无").Append("；坐标：").Append(pawn.Position)
                .Append("；已落地：").Append(pawn.Spawned).Append("；世界角色：").Append(Find.WorldPawns.Contains(pawn))
                .Append("\n死亡：").Append(pawn.Dead).Append("；销毁：").Append(pawn.Destroyed).Append("；倒地：").Append(pawn.Downed)
                .Append("；被俘：").Append(pawn.IsPrisoner).Append("；奴隶：").Append(pawn.IsSlave)
                .Append("\n持有容器：").Append(pawn.ParentHolder?.GetType().Name ?? "无")
                .Append("；职责：").Append(pawn.GetLord()?.LordJob?.GetType().Name ?? "无")
                .Append("；工作：").Append(pawn.CurJob?.def?.defName ?? "无");
        }

        internal static void TraceExit(Map source, Pawn pawn, string stage)
        {
            if (!Prefs.DevMode || !(source?.Parent is Site_WarWorkshop site) || !EnemyContractUtility.IsWarPawn(pawn)) return;
            var text = new StringBuilder("[MoonWorld][工坊离图] " + stage + " 工坊#" + site.ID + " Tick=" + GenTicks.TicksAbs);
            AppendPawn(text, "角色", pawn);
            text.Append("\n御主逃脱：").Append(site.MasterEscaped).Append("；从者逃脱：").Append(site.ServantEscaped);
            Log.Message(text.ToString());
        }
    }
}
