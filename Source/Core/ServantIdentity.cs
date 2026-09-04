using System.Collections.Generic;
using Verse;

namespace MoonWorld
{
    public class ServantIdentityDef : Def
    {
        public PawnKindDef servantKind;
        public ServantResourceProfileDef resourceProfile;
        public ServantAutonomyProfileDef autonomyProfile;
        public List<NoblePhantasmDef> noblePhantasms = new List<NoblePhantasmDef>();
    }

    public class ServantResourceProfileDef : Def
    {
        public float maxPrana = 100f;
        public float materializedUpkeepPerDay = 6f;
        public float spiritUpkeepMultiplier = 0.25f;
        public float materializedSustainThreshold = 30f;
        public float spiritSustainThreshold = 10f;
        public int shortageDurationTicks = 60000;
        public float foodToPranaPerDay = 2f;
        public float foodToPranaEfficiency = 1f;
        public float foodConversionThreshold = 0.2f;
        public float healingMaxPerInterval = 1f;
        public float pranaPerHealingPoint = 4f;
        public int rematerializationCooldownTicks = 10000;
        public int maxSpiritDamageStages = 4;
    }

    public class ServantAutonomyProfileDef : Def
    {
        public string lordJobKey = "QuestLodger";
        public string targetPriorityKey = "ServantMasterNormal";
    }

    public class NoblePhantasmDef : Def
    {
        public float pranaCost;
        public ThingDef presentationDef;
    }

    public static class ServantIdentityUtility
    {
        private static readonly Dictionary<PawnKindDef, ServantIdentityDef> ByKind =
            new Dictionary<PawnKindDef, ServantIdentityDef>();

        public static ServantIdentityDef GetIdentity(Pawn pawn)
        {
            return pawn == null ? null : GetIdentity(pawn.kindDef);
        }

        public static ServantIdentityDef GetIdentity(PawnKindDef kind)
        {
            if (kind == null)
            {
                return null;
            }

            ServantIdentityDef cached;
            if (ByKind.TryGetValue(kind, out cached))
            {
                return cached;
            }

            foreach (ServantIdentityDef identity in DefDatabase<ServantIdentityDef>.AllDefsListForReading)
            {
                if (identity.servantKind == kind)
                {
                    ByKind[kind] = identity;
                    return identity;
                }
            }

            return null;
        }

        public static ServantResourceProfileDef GetProfile(Pawn pawn)
        {
            return GetIdentity(pawn)?.resourceProfile;
        }
    }
}
