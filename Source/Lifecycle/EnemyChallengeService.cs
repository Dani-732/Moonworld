using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace MoonWorld
{
    internal static class EnemyChallengeService
    {
        private static GameComponent_MoonWorld War => Current.Game?.GetComponent<GameComponent_MoonWorld>();
        private static bool starting, deploying;
        internal static bool IsReserved(Pawn pawn) => pawn != null && War?.enemyChallenge?.servant == pawn;
        internal static bool AtSite(Site site) => site != null && War?.enemyChallenge?.site == site;

        internal static bool TryStart(Map home, out string rejection)
        {
            var war = War;
            rejection = EnemyWarPartyService.ValidateRaid(home);
            if (rejection != null) return false;
            if (starting || war.enemyChallenge != null) { rejection = "已有一场约战等待处理。"; return false; }
            var ready = new List<EnemyWarParticipant>();
            foreach (var enemy in war.CurrentWarEntry.Participants)
                if (!enemy.EnemyEliminated && !WorkshopRebuildService.BlocksRaid(enemy)
                    && EnemyRestUtility.ReadinessRejection(enemy.EnemyServant) == null) ready.Add(enemy);
            if (ready.Count == 0) { rejection = "没有可约战的从者。"; return false; }
            var chosen = ready.RandomElement();
            var offer = new EnemyChallengeSession(chosen.EnemyServant, null);
            starting = true;
            war.enemyChallenge = offer; // Reserve before world-object hooks can re-enter any deployment path.
            try
            {
                offer.site = WarEncounterSiteUtility.TryCreate(home.Tile, offer.faction, true);
                if (offer.site == null) throw new InvalidOperationException("基地附近没有可到达的空闲约战地点。");
                if (war.enemyChallenge != offer || war.CurrentWarOutcome != WarOutcome.Ongoing || !Valid(offer)
                    || EnemyRestUtility.ReadinessRejection(offer.servant, false, true) != null)
                    throw new InvalidOperationException("约战从者在建立地点期间失效。");
                Find.LetterStack.ReceiveLetter("敌方从者约战",
                    chosen.EnemyServant.LabelShortCap + " 在基地附近等候交锋。请在一天内派本届御主或从者到场。\n\n"
                    + "到场后正常交战；逾期则本次约战结束，不会因此固定触发基地突袭。敌方御主仍留守原据点。",
                    LetterDefOf.ThreatSmall, offer.site);
                if (war.enemyChallenge != offer || war.CurrentWarOutcome != WarOutcome.Ongoing || !Valid(offer)
                    || EnemyRestUtility.ReadinessRejection(offer.servant, false, true) != null)
                    throw new InvalidOperationException("约战通知期间参与者已改变。");
                chosen.RecordEnemyDeployment(offer.master, offer.servant);
                return true;
            }
            catch (Exception ex)
            {
                if (war.enemyChallenge == offer) war.enemyChallenge = null;
                WarEncounterSiteUtility.Cleanup(offer.site);
                rejection = "约战未能建立：" + ex.Message;
                Log.Warning("[MoonWorld] " + rejection);
                return false;
            }
            finally { starting = false; }
        }

        private static bool Valid(EnemyChallengeSession offer)
        {
            Pawn pawn = offer.servant;
            return offer.site != null && !offer.site.Destroyed && UnboundServantService.Exists(pawn)
                && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer) && EnemyContractUtility.HasEnemyContract(pawn)
                && pawn.Faction == offer.faction && ServantQuery.Instance.GetMaster(pawn) == offer.master
                && offer.master != null && !offer.master.Dead && !offer.master.Destroyed
                && !offer.master.IsPrisoner && !offer.master.IsSlave
                && !pawn.IsPrisoner && !pawn.IsSlave && !pawn.Suspended && !pawn.InMentalState;
        }

        internal static bool PlayerAttended(GameComponent_MoonWorld war, Map map)
        {
            if (map == null || war?.CurrentWarEntry == null) return false;
            var entry = war.CurrentWarEntry;
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            {
                if (pawn.Dead || pawn.Destroyed || pawn.IsPrisoner || pawn.IsSlave || pawn.Faction != Faction.OfPlayer) continue;
                if (pawn == entry.DesignatedMaster || entry.Participants.Exists(p => p.CurrentMaster == pawn || p.EnemyServant == pawn))
                    return true;
            }
            return false;
        }

        internal static void Tick(GameComponent_MoonWorld war)
        {
            var offer = war.enemyChallenge;
            if (offer == null || starting || deploying) return;
            if (war.CurrentWarOutcome != WarOutcome.Ongoing || !Valid(offer)) { Finish(war); return; }
            Pawn pawn = offer.servant;
            if (offer.onMap)
            {
                if (!pawn.Spawned || pawn.Map != offer.site.Map || ServantQuery.Instance.IsSpirit(pawn)
                    || !PlayerAttended(war, offer.site.Map)
                    || (pawn.GetLord()?.LordJob as LordJob_EnemyWarParty)?.Retreating == true) Finish(war);
                return;
            }
            // Expiration is absolute and is not extended by entering with unrelated colonists or failed deployment.
            if (GenTicks.TicksAbs >= offer.expiresAtTickAbs)
            {
                Messages.Message("约战期限已过，敌方从者结束等待。", offer.site, MessageTypeDefOf.NeutralEvent, false);
                Finish(war);
                return;
            }
            if (!WorkshopRebuildService.IsFreeSurvivor(pawn)) { Finish(war); return; }
            if (!PlayerAttended(war, offer.site.Map)) return;
            if (EnemyRestUtility.ReadinessRejection(pawn, true, true) != null) { Finish(war); return; }
            if (!CellFinder.TryFindRandomCellNear(offer.site.Map.Center, offer.site.Map, 18,
                c => c.Standable(offer.site.Map) && c.GetFirstPawn(offer.site.Map) == null
                    && offer.site.Map.reachability.CanReachMapEdge(c, TraverseParms.For(TraverseMode.PassDoors)), out IntVec3 cell)) return;
            var participant = war.CurrentWarEntry.FindEnemy(pawn);
            Map encounterMap = offer.site.Map;
            deploying = true;
            try
            {
                if (participant != null && EnemyWarPartyService.TryRedeployExisting(participant, encounterMap, cell, out _,
                    () => war.enemyChallenge == offer && war.CurrentWarOutcome == WarOutcome.Ongoing && Valid(offer)
                        && offer.site.Map == encounterMap && GenTicks.TicksAbs < offer.expiresAtTickAbs
                        && PlayerAttended(war, encounterMap))) offer.onMap = true;
            }
            finally { deploying = false; }
            if (war.enemyChallenge == offer && (!Valid(offer) || war.CurrentWarOutcome != WarOutcome.Ongoing)) Finish(war);
        }

        internal static void BeforeMapRemoval(Site site)
        {
            if (AtSite(site) && War.enemyChallenge.onMap) Finish(War);
            // An unrelated caravan visiting the empty location does not cancel or refresh the offer.
        }

        private static void Finish(GameComponent_MoonWorld war)
        {
            var offer = war.enemyChallenge;
            if (offer == null) return;
            war.enemyChallenge = null;
            Pawn pawn = offer.servant;
            Pawn master = ServantQuery.Instance.GetMaster(pawn);
            if (pawn?.Faction == offer.faction && (master == null || master == offer.master))
            {
                EnemyBattleService.ReturnToWorld(pawn, offer.site?.Map);
                EnemyWarPartyService.RetainDepartedPawn(pawn);
            }
            WarEncounterSiteUtility.Cleanup(offer.site);
        }
    }
}
