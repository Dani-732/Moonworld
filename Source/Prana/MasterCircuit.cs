using RimWorld;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    public class MasterCircuitDef : Def
    {
        public float maxPrana = 100f;
        public float naturalRegenPerDay = 240f;
        public float supplyThresholdFraction = 0.8f;
    }

    public interface IMasterSupplyThresholdPolicy
    {
        float GetThresholdFraction(Pawn master);
    }

    public sealed class DefMasterSupplyThresholdPolicy : IMasterSupplyThresholdPolicy
    {
        public float GetThresholdFraction(Pawn master)
        {
            CompMasterPranaControl control = master?.TryGetComp<CompMasterPranaControl>();
            if (control != null)
            {
                return control.GetThresholdFraction();
            }

            MasterCircuitDef circuit = MasterCircuitUtility.GetCircuit(master);
            return Mathf.Clamp01(circuit?.supplyThresholdFraction ?? 1f);
        }
    }

    public static class MasterSupplyThresholdService
    {
        public static IMasterSupplyThresholdPolicy Policy { get; set; }
            = new DefMasterSupplyThresholdPolicy();

        public static float GetThreshold(Pawn master, Need_MasterPrana masterPrana)
        {
            if (masterPrana == null)
            {
                return 0f;
            }

            IMasterSupplyThresholdPolicy policy = Policy ?? new DefMasterSupplyThresholdPolicy();
            return masterPrana.MaxLevel * Mathf.Clamp01(policy.GetThresholdFraction(master));
        }
    }

    public class MasterCircuitExtension : DefModExtension
    {
        public MasterCircuitDef circuitDef;
    }

    public static class MasterCircuitUtility
    {
        public static bool HasCircuit(Pawn pawn)
        {
            return GetCircuit(pawn) != null;
        }

        public static MasterCircuitDef GetCircuit(Pawn pawn)
        {
            if (pawn?.story?.traits == null)
            {
                return null;
            }

            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                MasterCircuitExtension extension = trait.def.GetModExtension<MasterCircuitExtension>();
                if (extension?.circuitDef != null)
                {
                    return extension.circuitDef;
                }
            }
            return null;
        }

        public static void EnsureMasterPranaNeed(Pawn master)
        {
            if (master == null || master.needs == null)
            {
                return;
            }

            master.needs.AddOrRemoveNeedsAsAppropriate();
            PawnNeedAccess.EnsureNeed(master, MW_DefOf.MW_MasterPrana);
        }
    }
}
