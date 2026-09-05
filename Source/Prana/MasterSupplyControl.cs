using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    public sealed class CompProperties_MasterPranaControl : CompProperties
    {
        public CompProperties_MasterPranaControl()
        {
            compClass = typeof(CompMasterPranaControl);
        }
    }

    public sealed class CompMasterPranaControl : ThingComp
    {
        private const float NoOverride = -1f;
        private float supplyThresholdOverride = NoOverride;

        public bool HasThresholdOverride => supplyThresholdOverride >= 0f;

        public float GetThresholdFraction()
        {
            if (HasThresholdOverride)
            {
                return Mathf.Clamp01(supplyThresholdOverride);
            }

            Pawn pawn = parent as Pawn;
            MasterCircuitDef circuit = MasterCircuitUtility.GetCircuit(pawn);
            return Mathf.Clamp01(circuit?.supplyThresholdFraction ?? 1f);
        }

        public void SetThresholdFraction(float value)
        {
            supplyThresholdOverride = Mathf.Clamp01(value);
        }

        public void ResetThreshold()
        {
            supplyThresholdOverride = NoOverride;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Pawn pawn = parent as Pawn;
            if (pawn?.Faction == Faction.OfPlayer && MasterCircuitUtility.HasCircuit(pawn))
            {
                yield return new Gizmo_MasterPranaControl(pawn, this);
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref supplyThresholdOverride, "supplyThresholdOverride", NoOverride);
        }
    }

    [StaticConstructorOnStartup]
    public sealed class Gizmo_MasterPranaControl : Gizmo
    {
        private const float Width = 160f;
        private const float GizmoHeight = 75f;
        private static readonly List<float> ThresholdMarkers = new List<float> { 0.5f, 0.8f };
        private static readonly Texture2D FilledBarTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.62f, 0.78f));
        private static readonly Texture2D HighlightBarTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.32f, 0.75f, 0.9f));
        private static readonly Texture2D EmptyBarTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.08f, 0.09f, 0.11f));
        private static readonly Texture2D TargetTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.95f, 0.82f, 0.34f));
        private static bool draggingThreshold;

        private readonly Pawn master;
        private readonly CompMasterPranaControl control;

        public Gizmo_MasterPranaControl(Pawn master, CompMasterPranaControl control)
        {
            this.master = master;
            this.control = control;
            Order = -98f;
        }

        public override float GetWidth(float maxWidth)
        {
            return Mathf.Min(Width, maxWidth);
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect outerRect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), GizmoHeight);
            Widgets.DrawWindowBackground(outerRect);

            Rect contentRect = outerRect.ContractedBy(6f);
            DrawHeader(contentRect);

            Need_MasterPrana prana = master?.needs?.TryGetNeed<Need_MasterPrana>();
            float currentFraction = prana == null ? 0f : prana.CurLevelPercentage;
            float thresholdFraction = control.GetThresholdFraction();
            Rect barRect = new Rect(contentRect.x, contentRect.y + 27f, contentRect.width, 34f);

            float previousThreshold = thresholdFraction;
            Widgets.DraggableBar(
                barRect,
                FilledBarTexture,
                HighlightBarTexture,
                EmptyBarTexture,
                TargetTexture,
                ref draggingThreshold,
                currentFraction,
                ref thresholdFraction,
                ThresholdMarkers,
                20,
                0f,
                1f);

            if (!Mathf.Approximately(previousThreshold, thresholdFraction))
            {
                control.SetThresholdFraction(thresholdFraction);
            }

            DrawBarLabel(barRect, prana, control.GetThresholdFraction());

            if (Mouse.IsOver(outerRect))
            {
                Widgets.DrawHighlight(outerRect);
                TooltipHandler.TipRegion(
                    outerRect,
                    "显示御主当前魔力。拖动黄色指针可调整供魔安全线；高于安全线的魔力会供给所有未满魔的契约从者。");
            }

            return new GizmoResult(GizmoState.Clear);
        }

        private void DrawHeader(Rect contentRect)
        {
            Rect labelRect = new Rect(contentRect.x, contentRect.y, contentRect.width - 22f, 22f);
            Widgets.Label(labelRect, "御主魔力与供魔安全线");

            if (!control.HasThresholdOverride)
            {
                return;
            }

            Rect resetRect = new Rect(contentRect.xMax - 20f, contentRect.y, 20f, 20f);
            if (Widgets.ButtonImage(resetRect, TexButton.CurveResetTex, true, "恢复回路默认供魔安全线"))
            {
                control.ResetThreshold();
            }
        }

        private static void DrawBarLabel(Rect barRect, Need_MasterPrana prana, float thresholdFraction)
        {
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;

            string current = prana == null
                ? "--"
                : Mathf.RoundToInt(prana.CurLevel) + " / " + Mathf.RoundToInt(prana.MaxLevel);
            Widgets.Label(barRect, current + "  安全线 " + Mathf.RoundToInt(thresholdFraction * 100f) + "%");

            Text.Anchor = previousAnchor;
            Text.Font = previousFont;
        }
    }
}
