using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    // One effective threshold for shortage, healing and UI. No duplicated travel state.
    public static class ServantSustainPolicy
    {
        public static bool IsTogether(Pawn servant)
        {
            Pawn master = ServantQuery.Instance.GetMaster(servant);
            if (master == null || servant == null || master.Dead || master.Destroyed) return false;
            Map map = servant.MapHeld;
            if (map != null) return master.MapHeld == map;
            Caravan caravan = servant.GetCaravan();
            if (caravan != null) return master.GetCaravan() == caravan;
            if (Find.WorldObjects != null)
                foreach (TravellingTransporters transport in Find.WorldObjects.TravellingTransporters)
                    if (transport.Pawns.Contains(servant)) return transport.Pawns.Contains(master);
            return false;
        }

        public static float SeparationMultiplier(Pawn servant)
        {
            if (IsTogether(servant)) return 1f;
            // Vanilla stats allow abilities to modify this through Hediffs, traits or equipment.
            float value = servant.GetStatValue(MW_DefOf.MW_SeparatedSustainMultiplier, applyPostProcess: true, cacheStaleAfterTicks: 0);
            return float.IsNaN(value) || float.IsInfinity(value) ? 2f : Mathf.Max(0f, value);
        }

        public static float Threshold(Pawn servant, ServantPresenceState presence)
        {
            ServantResourceProfileDef profile = ServantIdentityUtility.GetProfile(servant);
            if (profile == null || presence == ServantPresenceState.Annihilated) return 0f;
            float baseline = presence == ServantPresenceState.Materialized
                ? profile.materializedSustainThreshold : profile.spiritSustainThreshold;
            return Mathf.Max(0f, baseline) * SeparationMultiplier(servant);
        }

        public static float Threshold(Pawn servant)
        {
            return Threshold(servant, servant.TryGetComp<CompServantState>()?.PresenceState ?? ServantPresenceState.Materialized);
        }
    }
}
