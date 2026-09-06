using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class ServantSummoningService : IServantSummoningService
    {
        public static readonly ServantSummoningService Instance = new ServantSummoningService();
        private readonly List<ServantIdentityDef> candidates = new List<ServantIdentityDef>();
        private bool summoning;

        private ServantSummoningService() { }

        public bool TrySummon(Pawn master, Map map, IntVec3 cell, out Pawn servant, out string rejection)
        {
            servant = null;
            rejection = Validate(master, map, cell);
            if (rejection != null) return false;

            summoning = true;
            Pawn generated = null;
            try
            {
                using (var enemyPreparation = new EnemyWarPreparation())
                {
                    candidates.Clear();
                    foreach (ServantIdentityDef identity in DefDatabase<ServantIdentityDef>.AllDefsListForReading)
                    {
                        if (identity.summonable && identity.servantKind != null && identity.servantKind.race != null)
                            candidates.Add(identity);
                    }
                    if (candidates.Count == 0) { rejection = "当前没有可召唤的从者。"; return false; }

                    ServantIdentityDef selected = candidates.RandomElement();
                    ServantIdentityDef opponent = HolyGrailWarClassUtility.PickOpponent(selected);
                    if (opponent == null)
                    {
                        rejection = "当前召唤职阶缺少对应的敌方英灵配置。";
                        return false;
                    }
                    generated = PawnGenerator.GeneratePawn(new PawnGenerationRequest(selected.servantKind,
                        Faction.OfPlayer, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                        canGeneratePawnRelations: false, validatorPreGear: pawn => { generated = pawn; return true; }));
                    if (generated == null) throw new SummoningFailureException("从者生成未返回有效角色。");
                    GenSpawn.Spawn(generated, cell, map, WipeMode.Vanish);
                    // The source mod's PostSpawnSetup owns appearance, loadout and body visibility.
                    if (!ServantLifecycleService.Instance.TryBind(master, generated, out rejection))
                        throw new SummoningFailureException(rejection);
                    enemyPreparation.Prepare(map, opponent);
                    rejection = HolyGrailWarEntryService.RegularSummonRejection(master);
                    if (rejection != null || !master.Spawned || master.Map != map
                        || !generated.Spawned || generated.Map != map || generated.Dead || generated.Destroyed
                        || generated.TryGetComp<CompServantState>()?.Master != master)
                        throw new SummoningFailureException(rejection ?? "召唤完成前契约或落点状态发生变化。");
                    GameComponent_MoonWorld state = Current.Game.GetComponent<GameComponent_MoonWorld>();
                    state.CommitRegularSummon();
                    state.CurrentWarEntry.SetParticipants(selected, opponent);
                    enemyPreparation.Commit(state.CurrentWarEntry);
                    HolyGrailWarQuestService.Ensure(state);
                    servant = generated;
                    return true;
                }
            }
            catch (SummoningFailureException ex)
            {
                Rollback(generated);
                rejection = ex.Message;
                return false;
            }
            catch (System.Exception ex)
            {
                Log.Error("[MoonWorld] 召唤失败并回滚: " + ex);
                Rollback(generated);
                rejection = "召唤过程发生错误，已回滚。";
                return false;
            }
            finally
            {
                summoning = false;
            }
        }

        public string CommandRejection(Pawn master, Map map)
        {
            if (summoning) return "召唤正在进行中。";
            string rejection = HolyGrailWarEntryService.RegularSummonRejection(master);
            if (rejection != null) return rejection;
            if (map == null || !master.Spawned || master.Map != map)
                return "御主必须在当前地图上。";
            return null;
        }

        public string Validate(Pawn master, Map map, IntVec3 cell)
        {
            string rejection = CommandRejection(master, map);
            if (rejection != null) return rejection;
            if (!cell.InBounds(map) || !cell.Standable(map)) return "请选择地图上的可站立位置。";
            if (cell.Fogged(map)) return "请选择已探索的位置。";
            return null;
        }

        internal static void Rollback(Pawn pawn)
        {
            if (pawn == null) return;
            pawn.TryGetComp<CompServantState>()?.Bind(null);
            try
            {
                if (!pawn.Destroyed) pawn.Destroy(DestroyMode.Vanish);
            }
            finally
            {
                // Pawn.Destroy can retain a world pawn; failed summons must leave no such entry.
                if (Find.WorldPawns.Contains(pawn)) Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
            }
        }

        private sealed class SummoningFailureException : System.Exception
        {
            public SummoningFailureException(string message) : base(message) { }
        }
    }
}
