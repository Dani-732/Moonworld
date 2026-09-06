using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MoonWorld
{
    public sealed class CompProperties_ServantCommands : CompProperties
    {
        public CompProperties_ServantCommands() { compClass = typeof(CompServantCommands); }
    }

    public sealed class CompServantCommands : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Pawn servant = parent as Pawn;
            if (!ServantDepartureService.IsContractServant(servant)) yield break;
            yield return new Gizmo_ServantPrana(servant);
            Command_Action presence = CompMasterServantCommands.CreatePresenceCommand(
                ServantQuery.Instance.GetMaster(servant), servant);
            if (presence != null) yield return presence;
            if (ServantQuery.Instance.IsSpirit(servant) && ServantTravelAutonomy.CanExitAsPlayer(servant))
            {
                yield return new Command_Action
                {
                    defaultLabel = "撤离地图",
                    defaultDesc = "灵体自行前往原版地图出口，加入附近可汇合的远行队或建立远行队。无需御主同行。",
                    icon = RimWorld.Planet.FormCaravanComp.FormCaravanCommand,
                    action = () =>
                    {
                        Job exit = ServantTravelAutonomy.GetPlayerExitJob(servant);
                        if (exit == null)
                        {
                            Messages.Message("没有可用的撤离出口。", servant, MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        servant.jobs.StartJob(exit, JobCondition.InterruptForced);
                    }
                };
            }
        }
    }

    [StaticConstructorOnStartup]
    public sealed class Gizmo_ServantPrana : Gizmo
    {
        private static readonly Texture2D Filled = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.62f, 0.78f));
        private static readonly Texture2D Empty = SolidColorMaterials.NewSolidColorTexture(new Color(0.08f, 0.09f, 0.11f));
        private readonly Pawn servant;

        public Gizmo_ServantPrana(Pawn servant) { this.servant = servant; Order = -98f; }
        public override float GetWidth(float maxWidth) { return Mathf.Min(160f, maxWidth); }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Widgets.DrawWindowBackground(rect);
            Rect inner = rect.ContractedBy(6f);
            Need_Prana prana = servant.needs?.TryGetNeed<Need_Prana>();
            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            Color color = GUI.color;
            try
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f), "从者魔力");
                Rect bar = new Rect(inner.x, inner.y + 27f, inner.width, 34f);
                Widgets.FillableBar(bar, prana?.CurLevelPercentage ?? 0f, Filled, Empty, false);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(bar, prana == null ? "--" : Mathf.RoundToInt(prana.CurLevel) + " / " + Mathf.RoundToInt(prana.MaxLevel));
                TooltipHandler.TipRegion(rect, servant.LabelShortCap + "\n" + servant.TryGetComp<CompServantState>().CompInspectStringExtra());
            }
            finally { Text.Font = font; Text.Anchor = anchor; GUI.color = color; }
            return new GizmoResult(GizmoState.Clear);
        }
    }
}
