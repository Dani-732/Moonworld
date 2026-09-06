using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    public class MoonWorldSettingsDef : Def
    {
        public int pranaUpdateIntervalTicks = 250;
        public float enemyPranaSupplyPerDay = 240f;
        public int enemyRestDurationTicks = 180000;
        public float enemyRaidPranaFraction = 0.8f;
    }

    public static class PranaCycleService
    {
        private static readonly List<Pawn> servants = new List<Pawn>();
        private static readonly List<Pawn> masters = new List<Pawn>();
        private static readonly List<Pawn> masterServants = new List<Pawn>();

        public static void Execute(int intervalTicks)
        {
            FindBoundPawns();
            PranaLedger ledger = new PranaLedger();

            ApplyMasterNaturalRegen(ledger, intervalTicks);
            ledger.Commit();
            ApplyEnemyFixedSupply(ledger, intervalTicks);
            ledger.Commit();
            ApplyFoodConversion(ledger, intervalTicks);
            ledger.Commit();
            ApplyMasterDistribution(ledger);
            ledger.Commit();
            ApplyServantUpkeepAndShortage(ledger, intervalTicks);
            ledger.Commit();
            ApplyHealing(ledger);
            ledger.Commit();
        }

        private static void FindBoundPawns()
        {
            servants.Clear();
            masters.Clear();
            HashSet<Pawn> seenServants = new HashSet<Pawn>();
            HashSet<Pawn> seenMasters = new HashSet<Pawn>();
            foreach (Map map in Find.Maps)
            {
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                {
                    if (MasterCircuitUtility.HasCircuit(pawn) && seenMasters.Add(pawn))
                    {
                        masters.Add(pawn);
                    }

                    ServantSnapshot snapshot;
                    if (!ServantQuery.Instance.TryGetSnapshot(pawn, out snapshot) || snapshot.master == null)
                    {
                        continue;
                    }
                    if (seenServants.Add(pawn))
                    {
                        servants.Add(pawn);
                    }
                    if (seenMasters.Add(snapshot.master))
                    {
                        masters.Add(snapshot.master);
                    }
                }
            }
            // Only the current, freely resting opponent joins off-map settlement, not all world pawns.
            Pawn resting = Current.Game?.GetComponent<GameComponent_MoonWorld>()?.CurrentWarEntry?.EnemyServant;
            if (EnemyContractUtility.IsResting(resting) && seenServants.Add(resting))
                servants.Add(resting);
        }

        private static void ApplyMasterNaturalRegen(PranaLedger ledger, int intervalTicks)
        {
            foreach (Pawn master in masters)
            {
                if (EnemyContractUtility.IsWarPawn(master)) continue;
                MasterCircuitDef circuit = MasterCircuitUtility.GetCircuit(master);
                Need_MasterPrana prana = master.needs.TryGetNeed<Need_MasterPrana>();
                if (circuit != null && prana != null)
                {
                    ledger.Add(prana, circuit.naturalRegenPerDay * intervalTicks / 60000f);
                }
            }
        }

        private static void ApplyEnemyFixedSupply(PranaLedger ledger, int intervalTicks)
        {
            foreach (Pawn servant in servants)
                if (EnemyContractUtility.CanReceiveSupply(servant))
                    ledger.Add(servant.needs.TryGetNeed<Need_Prana>(),
                        Mathf.Max(0f, MW_DefOf.MW_HolyGrailWarSettings.enemyPranaSupplyPerDay) * intervalTicks / 60000f);
        }

        private static void ApplyFoodConversion(PranaLedger ledger, int intervalTicks)
        {
            foreach (Pawn servant in servants)
            {
                if (EnemyContractUtility.HasEnemyContract(servant)) continue;
                if (!ServantQuery.Instance.IsMaterialized(servant))
                {
                    continue;
                }
                ServantResourceProfileDef profile = ServantIdentityUtility.GetProfile(servant);
                Need_Food food = servant.needs.food;
                Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
                if (profile == null || food == null || prana == null || ledger.RemainingCapacity(prana) <= 0f)
                {
                    continue;
                }
                if (food.CurLevelPercentage <= profile.foodConversionThreshold)
                {
                    continue;
                }

                float foodConsumed = profile.foodToPranaPerDay * intervalTicks / 60000f;
                float allowedFood = Mathf.Max(0f, food.CurLevel - food.MaxLevel * profile.foodConversionThreshold);
                foodConsumed = Mathf.Min(foodConsumed, allowedFood);
                if (foodConsumed > 0f)
                {
                    food.CurLevel -= foodConsumed;
                    ledger.Add(prana, foodConsumed * profile.foodToPranaEfficiency);
                }
            }
        }

        private static void ApplyMasterDistribution(PranaLedger ledger)
        {
            foreach (Pawn master in masters)
            {
                if (EnemyContractUtility.IsWarPawn(master)) continue;
                Need_MasterPrana masterPrana = master.needs.TryGetNeed<Need_MasterPrana>();
                MasterCircuitDef circuit = MasterCircuitUtility.GetCircuit(master);
                if (masterPrana == null || circuit == null)
                {
                    continue;
                }

                masterServants.Clear();
                ServantQuery.Instance.GetBoundServants(master, masterServants);
                masterServants.Sort((left, right) => left.thingIDNumber.CompareTo(right.thingIDNumber));
                float threshold = MasterSupplyThresholdService.GetThreshold(master, masterPrana);
                float available = Mathf.Max(0f, ledger.LevelAfterPending(masterPrana) - threshold);
                while (available > 0.001f)
                {
                    int recipients = 0;
                    foreach (Pawn servant in masterServants)
                    {
                        Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
                        if (CanReceiveMasterSupply(servant, prana) && ledger.RemainingCapacity(prana) > 0.001f)
                        {
                            recipients++;
                        }
                    }
                    if (recipients == 0)
                    {
                        break;
                    }

                    float share = available / recipients;
                    float transferred = 0f;
                    foreach (Pawn servant in masterServants)
                    {
                        Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
                        if (!CanReceiveMasterSupply(servant, prana))
                        {
                            continue;
                        }
                        float amount = Mathf.Min(share, ledger.RemainingCapacity(prana));
                        if (amount > 0f)
                        {
                            ledger.Add(prana, amount);
                            transferred += amount;
                        }
                    }
                    if (transferred <= 0.001f)
                    {
                        break;
                    }
                    ledger.Add(masterPrana, -transferred);
                    available -= transferred;
                }
            }
        }

        private static bool CanReceiveMasterSupply(Pawn servant, Need_Prana prana)
        {
            if (servant == null || !servant.Spawned) return false;
            CompServantState state = servant?.TryGetComp<CompServantState>();
            return prana != null
                && servant != null
                && !servant.Dead
                && state != null
                && state.PresenceState != ServantPresenceState.Annihilated;
        }

        private static void ApplyServantUpkeepAndShortage(PranaLedger ledger, int intervalTicks)
        {
            foreach (Pawn servant in servants)
            {
                ServantResourceProfileDef profile = ServantIdentityUtility.GetProfile(servant);
                Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
                CompServantState state = servant.TryGetComp<CompServantState>();
                if (profile == null || prana == null || state == null || state.PresenceState == ServantPresenceState.Annihilated)
                {
                    continue;
                }

                float multiplier = state.PresenceState == ServantPresenceState.Materialized ? 1f : profile.spiritUpkeepMultiplier;
                ledger.Add(prana, -profile.materializedUpkeepPerDay * multiplier * intervalTicks / 60000f);
                float threshold = ServantSustainPolicy.Threshold(servant, state.PresenceState);
                UpdateShortageState(servant, ledger.LevelAfterPending(prana) < threshold, profile);
            }
        }

        private static void UpdateShortageState(Pawn servant, bool isShortage, ServantResourceProfileDef profile)
        {
            Hediff shortage = servant.health.hediffSet.GetFirstHediffOfDef(MW_DefOf.MW_PranaShortage);
            if (!isShortage)
            {
                if (shortage != null)
                {
                    servant.health.RemoveHediff(shortage);
                }
                return;
            }

            if (shortage == null)
            {
                servant.health.AddHediff(MW_DefOf.MW_PranaShortage);
                return;
            }
            if (shortage.ageTicks >= profile.shortageDurationTicks)
            {
                ServantLifecycleService.Instance.TryResolveDefeat(servant);
            }
        }

        private static void ApplyHealing(PranaLedger ledger)
        {
            foreach (Pawn servant in servants)
            {
                ServantResourceProfileDef profile = ServantIdentityUtility.GetProfile(servant);
                Need_Prana prana = servant.needs.TryGetNeed<Need_Prana>();
                CompServantState state = servant.TryGetComp<CompServantState>();
                if (profile == null || prana == null || state == null || state.PresenceState == ServantPresenceState.Annihilated)
                {
                    continue;
                }
                float sustainThreshold = ServantSustainPolicy.Threshold(servant, state.PresenceState);
                if (ledger.LevelAfterPending(prana) <= sustainThreshold)
                {
                    continue;
                }
                float healingPrana = Mathf.Max(0f, ledger.LevelAfterPending(prana) - sustainThreshold);
                float healingCost = Mathf.Max(0f, profile.pranaPerHealingPoint);
                float budget = healingCost > 0f
                    ? Mathf.Min(profile.healingMaxPerInterval, healingPrana / healingCost)
                    : 0f;
                foreach (Hediff hediff in new List<Hediff>(servant.health.hediffSet.hediffs))
                {
                    Hediff_Injury injury = hediff as Hediff_Injury;
                    if (injury == null || budget <= 0f)
                    {
                        continue;
                    }
                    float healed = Mathf.Min(budget, injury.Severity);
                    injury.Heal(healed);
                    ledger.Add(prana, -healed * healingCost);
                    budget -= healed;
                }

                float conditionCost = Mathf.Max(0f, profile.conditionCurePranaCost);
                if (conditionCost <= 0f
                    || ledger.LevelAfterPending(prana) - sustainThreshold < conditionCost)
                {
                    continue;
                }

                Hediff condition = ServantHealingPolicy.FindWorstCurableCondition(servant);
                if (condition != null)
                {
                    HealthUtility.Cure(condition);
                    ledger.Add(prana, -conditionCost);
                }
            }
        }
    }
}
