using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    internal static class EnemyBattleService
    {
        internal const int RoundInterval = 2500, MaximumRounds = 6, BattleCooldown = 60000;
        private static bool starting, materializing;
        private static GameComponent_MoonWorld War => Current.Game?.GetComponent<GameComponent_MoonWorld>();
        internal static bool IsEngaged(Pawn pawn) => War?.enemyBattle?.Contains(pawn) == true || EnemyChallengeService.IsReserved(pawn);
        internal static bool AtSite(Site site) => site != null && War?.enemyBattle?.site == site;
        internal static bool IsMapBattleFor(Pawn pawn) => War?.enemyBattle?.Contains(pawn) == true && War.enemyBattle.onMap;

        internal static void Tick(GameComponent_MoonWorld war)
        {
            if (war.enemyBattle != null)
            {
                Advance(war);
                return;
            }
            if (war.warStartTick < 0 || Find.TickManager.TicksGame - war.warStartTick < BattleCooldown
                || GenTicks.TicksAbs < war.enemyBattleNextStartTickAbs) return;
            TryStart(war);
        }

        internal static bool TryStart(GameComponent_MoonWorld war, bool? inField = null)
        {
            if (starting) return false;
            starting = true;
            try { return TryStartCore(war, inField); }
            finally { starting = false; }
        }

        private static bool TryStartCore(GameComponent_MoonWorld war, bool? inField)
        {
            if (war == null || war.enemyBattle != null || war.CurrentWarOutcome != WarOutcome.Ongoing
                || war.warStartTick < 0 || war.CurrentWarEntry == null) return false;
            var candidates = new List<EnemyWarParticipant>();
            foreach (var participant in war.CurrentWarEntry.Participants)
                if (CanStart(participant)) candidates.Add(participant);
            candidates.Shuffle();
            bool field = inField ?? Rand.Chance(WarEncounterPolicy.FieldBattleChance);
            foreach (var defender in candidates)
            {
                Site_WarWorkshop site = WorkshopRebuildService.FindWorkshop(defender);
                if (site == null || site.Destroyed || site.HasMap || site.RetreatOrdered
                    || (defender.CurrentMaster != null && site.OwnerMaster != defender.CurrentMaster)) continue;
                foreach (var attacker in candidates)
                {
                    if (attacker == defender || !attacker.EnemyServant.HostileTo(defender.EnemyServant)) continue;
                    if (!field && !WarEncounterPolicy.CanAttackWorkshop(attacker.EnemyServant)) continue;
                    Site location = site;
                    if (field)
                    {
                        location = WarEncounterSiteUtility.TryCreate(site.Tile, defender.EnemyServant.Faction, false);
                        if (location == null) continue;
                    }
                    var battle = new EnemyBattleSession(attacker.EnemyServant, defender.EnemyServant, location);
                    if (war.enemyBattle != null || war.CurrentWarOutcome != WarOutcome.Ongoing
                        || !CanStart(attacker) || !CanStart(defender) || !attacker.EnemyServant.HostileTo(defender.EnemyServant))
                    { WarEncounterSiteUtility.Cleanup(location); return false; }
                    war.enemyBattle = battle;
                    attacker.RecordEnemyDeployment(attacker.CurrentMaster, battle.attacker);
                    defender.RecordEnemyDeployment(defender.CurrentMaster, battle.defender);
                    Messages.Message("敌方从者 " + battle.attacker.LabelShortCap + " 与 " + battle.defender.LabelShortCap
                        + (field ? " 正在野外交战。" : " 正在工坊附近交战。"), location, MessageTypeDefOf.ThreatSmall, false);
                    return true;
                }
            }
            return false;
        }

        private static bool CanStart(EnemyWarParticipant participant)
        {
            Pawn pawn = participant.EnemyServant;
            if (IsEngaged(pawn) || !CanFight(pawn) || !WorkshopRebuildService.IsFreeSurvivor(pawn)
                || WorkshopRebuildService.BlocksRaid(participant) || EnemyRestUtility.TicksRemaining(pawn) > 0
                || pawn.health.ShouldBeDead() || pawn.health.ShouldBeDowned()) return false;
            Need_Prana prana = pawn.needs?.TryGetNeed<Need_Prana>();
            return prana != null && prana.CurLevel >= prana.MaxLevel * 0.5f;
        }

        private static bool CanFight(Pawn pawn)
        {
            if (!UnboundServantService.Exists(pawn) || pawn.Faction == null || !pawn.Faction.HostileTo(Faction.OfPlayer)
                || !EnemyContractUtility.IsWarPawn(pawn) || pawn.IsPrisoner || pawn.IsSlave || pawn.Suspended
                || pawn.InMentalState || !ServantQuery.Instance.IsMaterialized(pawn)) return false;
            var state = pawn.TryGetComp<CompServantState>();
            return EnemyContractUtility.HasEnemyContract(pawn)
                || (state.Master == null && state.UnboundUntilTickAbs > GenTicks.TicksAbs);
        }

        private static bool Valid(EnemyBattleSession battle)
        {
            return battle.site != null && !battle.site.Destroyed
                && (battle.site is Site_WarEncounter || (battle.site is Site_WarWorkshop workshop
                    && !workshop.RetreatOrdered && workshop.Participant?.EnemyServant == battle.defender))
                && CanFight(battle.attacker) && CanFight(battle.defender)
                && battle.attacker.Faction == battle.attackerFaction && battle.defender.Faction == battle.defenderFaction
                && ServantQuery.Instance.GetMaster(battle.attacker) == battle.attackerMaster
                && ServantQuery.Instance.GetMaster(battle.defender) == battle.defenderMaster
                && battle.attacker.HostileTo(battle.defender);
        }

        internal static void Advance(GameComponent_MoonWorld war)
        {
            EnemyBattleSession battle = war.enemyBattle;
            if (battle == null || materializing) return;
            if (war.CurrentWarOutcome != WarOutcome.Ongoing || !Valid(battle)) { Finish(war); return; }
            if (battle.onMap)
            {
                if (battle.site.HasMap && battle.attacker.Spawned && battle.defender.Spawned
                    && battle.attacker.Map == battle.site.Map && battle.defender.Map == battle.site.Map
                    && (battle.attacker.GetLord()?.LordJob as LordJob_EnemyWarParty)?.Retreating != true
                    && (battle.defender.GetLord()?.LordJob as LordJob_EnemyWarParty)?.Retreating != true) return;
                Finish(war);
                return;
            }
            // Site placement owns retries while a map is being generated or spawn hooks fail.
            if (battle.site.HasMap) return;
            if (!WorkshopRebuildService.IsFreeSurvivor(battle.attacker)
                || !WorkshopRebuildService.IsFreeSurvivor(battle.defender)) { Finish(war); return; }
            if (GenTicks.TicksAbs < battle.nextRoundTickAbs) return;
            // Advance before damage callbacks: a partially resolved round must never be replayed.
            battle.nextRoundTickAbs = GenTicks.TicksAbs + RoundInterval;
            battle.rounds++;
            try
            {
                bool attackerFirst = battle.rounds % 2 == 1;
                Strike(attackerFirst ? battle.attacker : battle.defender, attackerFirst ? battle.defender : battle.attacker);
                if (Valid(battle)) Strike(attackerFirst ? battle.defender : battle.attacker, attackerFirst ? battle.attacker : battle.defender);
                if (!Valid(battle) || battle.rounds >= MaximumRounds || ShouldWithdraw(battle.attacker)
                    || ShouldWithdraw(battle.defender)) Finish(war);
            }
            catch (Exception ex)
            {
                Finish(war);
                Log.Error("[MoonWorld] Enemy battle stopped after round failure: " + ex);
            }
        }

        private static void Strike(Pawn attacker, Pawn defender)
        {
            Need_Prana prana = attacker.needs?.TryGetNeed<Need_Prana>();
            float spent = Math.Min(prana?.CurLevel ?? 0f, 18f);
            if (prana != null) prana.CurLevel -= spent;
            defender.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 3f + spent / 3f, 0f, -1f, attacker));
        }

        private static bool ShouldWithdraw(Pawn pawn)
        {
            Need_Prana prana = pawn.needs?.TryGetNeed<Need_Prana>();
            return prana == null || prana.CurLevel < Math.Max(prana.MaxLevel * 0.2f, ServantSustainPolicy.Threshold(pawn))
                || pawn.health.ShouldBeDowned();
        }

        // Called before normal workshop defenders are placed. Failure retains the session for retry.
        internal static bool TryMaterialize(Site site)
        {
            if (!AtSite(site)) return false;
            if (materializing) return false;
            var battle = War.enemyBattle;
            if (battle.onMap) return true;
            if (!Valid(battle)) { Finish(War); return false; }
            if (site.Map == null) return false;
            var moved = new List<Pawn>();
            materializing = true;
            try
            {
                Place(battle.attacker, site.Map, moved, battle);
                Place(battle.defender, site.Map, moved, battle);
                if (War.enemyBattle != battle || War.CurrentWarOutcome != WarOutcome.Ongoing || !Valid(battle)
                    || !battle.attacker.Spawned || battle.attacker.Map != site.Map
                    || !battle.defender.Spawned || battle.defender.Map != site.Map)
                    throw new InvalidOperationException("Battle participants changed during deployment.");
                battle.onMap = true;
                return true;
            }
            catch (Exception ex)
            {
                foreach (Pawn pawn in moved) ReturnParticipant(battle, pawn);
                Log.Error("[MoonWorld] Enemy battle deployment failed; original pawns retained: " + ex);
                return false;
            }
            finally { materializing = false; }
        }

        private static void Place(Pawn pawn, Map map, List<Pawn> moved, EnemyBattleSession battle)
        {
            if (!WorkshopRebuildService.IsFreeSurvivor(pawn)) throw new InvalidOperationException("Battle pawn is not available.");
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 18,
                c => c.Standable(map) && c.GetFirstPawn(map) == null
                    && map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)), out IntVec3 cell))
                throw new InvalidOperationException("No reachable battle cell near map center.");
            moved.Add(pawn);
            Find.WorldPawns.RemovePawn(pawn);
            // These are live world-pawn transfers, not map-load restoration. The normal
            // spawn path resets the pawn's pather to the requested cell.
            GenSpawn.Spawn(pawn, cell, map, pawn.Rotation, WipeMode.Vanish);
            if (!pawn.Spawned || pawn.Map != map || !Valid(battle) || War.enemyBattle != battle
                || War.CurrentWarOutcome != WarOutcome.Ongoing)
                throw new InvalidOperationException("Battle spawn or participant validation failed.");
            LordMaker.MakeNewLord(pawn.Faction, new LordJob_EnemyWarParty(), map, new[] { pawn });
        }

        internal static bool BeforeMapRemoval(Site site)
        {
            if (!AtSite(site)) return false;
            var war = War;
            var battle = war.enemyBattle;
            bool resume = war.CurrentWarOutcome == WarOutcome.Ongoing && Valid(battle)
                && (!battle.onMap || (battle.attacker.Spawned && battle.attacker.Map == site.Map
                    && battle.defender.Spawned && battle.defender.Map == site.Map
                    && (battle.attacker.GetLord()?.LordJob as LordJob_EnemyWarParty)?.Retreating != true
                    && (battle.defender.GetLord()?.LordJob as LordJob_EnemyWarParty)?.Retreating != true));
            ReturnParticipant(battle, battle.attacker);
            ReturnParticipant(battle, battle.defender);
            if (resume && WorkshopRebuildService.IsFreeSurvivor(battle.attacker)
                && WorkshopRebuildService.IsFreeSurvivor(battle.defender))
            {
                battle.onMap = false;
                battle.nextRoundTickAbs = GenTicks.TicksAbs + RoundInterval;
            }
            else Finish(war);
            return true;
        }

        internal static void ReturnToWorld(Pawn pawn, Map map)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.IsPrisoner || pawn.IsSlave
                || pawn.Faction == Faction.OfPlayer || !EnemyContractUtility.IsWarPawn(pawn)
                || pawn.TryGetComp<CompServantState>()?.PresenceState == ServantPresenceState.Annihilated) return;
            if (pawn.Spawned && map != null && pawn.Map == map)
            {
                Lord lord = pawn.GetLord();
                if (lord != null) map.lordManager.RemoveLord(lord);
                pawn.jobs?.StopAll(false, false);
                pawn.DeSpawn();
            }
            if (!pawn.Spawned && pawn.ParentHolder == null && !Find.WorldPawns.Contains(pawn))
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        }

        private static void Finish(GameComponent_MoonWorld war)
        {
            var battle = war.enemyBattle;
            if (battle == null) return;
            war.enemyBattle = null;
            ReturnParticipant(battle, battle.attacker);
            ReturnParticipant(battle, battle.defender);
            RetainParticipant(battle, battle.attacker);
            RetainParticipant(battle, battle.defender);
            war.enemyBattleNextStartTickAbs = GenTicks.TicksAbs + BattleCooldown;
            WarEncounterSiteUtility.Cleanup(battle.site);
        }

        private static void ReturnParticipant(EnemyBattleSession battle, Pawn pawn)
        {
            Pawn currentMaster = ServantQuery.Instance.GetMaster(pawn);
            Pawn originalMaster = pawn == battle.attacker ? battle.attackerMaster : battle.defenderMaster;
            Faction originalFaction = pawn == battle.attacker ? battle.attackerFaction : battle.defenderFaction;
            if (pawn?.Faction == originalFaction && (currentMaster == null || currentMaster == originalMaster))
                ReturnToWorld(pawn, battle.site?.Map);
        }

        private static void RetainParticipant(EnemyBattleSession battle, Pawn pawn)
        {
            Pawn originalMaster = pawn == battle.attacker ? battle.attackerMaster : battle.defenderMaster;
            Faction originalFaction = pawn == battle.attacker ? battle.attackerFaction : battle.defenderFaction;
            Pawn master = ServantQuery.Instance.GetMaster(pawn);
            if (pawn?.Faction == originalFaction && (master == null || master == originalMaster))
                EnemyWarPartyService.RetainDepartedPawn(pawn);
        }
    }
}
