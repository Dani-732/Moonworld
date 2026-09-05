using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    [HarmonyPatch(typeof(Pawn), "get_CanTakeOrder")]
    public static class Harmony_SpiritForm_PlayerOrders
    {
        public static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__result && ServantQuery.Instance.IsSpirit(__instance)) __result = false;
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), "set_Drafted")]
    public static class Harmony_SpiritForm_Drafting
    {
        public static bool Prefix(Pawn_DraftController __instance, bool value)
        {
            return !value || !ServantQuery.Instance.IsSpirit(__instance.pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), "get_ShowDraftGizmo")]
    public static class Harmony_SpiritForm_DraftGizmo
    {
        public static void Postfix(Pawn_DraftController __instance, ref bool __result)
        {
            if (ServantQuery.Instance.IsSpirit(__instance.pawn)) __result = false;
        }
    }

    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawColonist))]
    public static class Harmony_SpiritForm_ColonistPortrait
    {
        public static void Postfix(Rect rect, Pawn colonist)
        {
            if (colonist.Dead || !ServantQuery.Instance.IsSpirit(colonist)) return;
            Color color = GUI.color;
            try
            {
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(rect.ContractedBy(2f), new Color(0.2f, 0.2f, 0.2f, 0.5f));
                TooltipHandler.TipRegion(rect, colonist.LabelShortCap + "：灵体状态");
            }
            finally { GUI.color = color; }
        }
    }
}
