using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MoonWorld
{
    public sealed class ServantSummoningService : IServantSummoningService
    {
        public static readonly ServantSummoningService Instance = new ServantSummoningService();
        private readonly List<ServantIdentityDef> candidates = new List<ServantIdentityDef>();

        private ServantSummoningService() { }

        public bool TrySummon(Pawn master, Map map, IntVec3 cell, out Pawn servant, out string rejection)
        {
            servant = null;
            rejection = Validate(master, map, cell);
            if (rejection != null) return false;

            candidates.Clear();
            foreach (ServantIdentityDef identity in DefDatabase<ServantIdentityDef>.AllDefsListForReading)
            {
                if (identity.summonable && identity.servantKind != null && identity.servantKind.race != null)
                    candidates.Add(identity);
            }
            if (candidates.Count == 0) { rejection = "当前没有可召唤的从者。"; return false; }

            ServantIdentityDef selected = candidates.RandomElement();
            Pawn generated = null;
            try
            {
                generated = PawnGenerator.GeneratePawn(new PawnGenerationRequest(selected.servantKind, Faction.OfPlayer, PawnGenerationContext.NonPlayer));
                GenSpawn.Spawn(generated, cell, map, WipeMode.Vanish);
                // The source mod's PostSpawnSetup owns appearance, loadout and body visibility.
                if (!ServantLifecycleService.Instance.TryBind(master, generated, out rejection))
                    throw new SummoningFailureException(rejection);
                GameComponent_MoonWorld state = Current.Game.GetComponent<GameComponent_MoonWorld>();
                state.RecordWarStartIfNeeded();
                servant = generated;
                return true;
            }
            catch (SummoningFailureException)
            {
                if (generated != null && !generated.Destroyed) generated.Destroy(DestroyMode.Vanish);
                return false;
            }
            catch (System.Exception ex)
            {
                Log.Error("[MoonWorld] 召唤失败并回滚: " + ex);
                if (generated != null && !generated.Destroyed) generated.Destroy(DestroyMode.Vanish);
                rejection = "召唤过程发生错误，已回滚。";
                return false;
            }
        }

        private static string Validate(Pawn master, Map map, IntVec3 cell)
        {
            if (master == null || !MasterCircuitUtility.HasCircuit(master) || master.Faction != Faction.OfPlayer)
                return "请选中拥有魔力回路的玩家御主。";
            if (map == null || !master.Spawned || master.Map != map)
                return "御主必须在当前地图上。";
            if (!cell.InBounds(map) || !cell.Standable(map)) return "请选择地图上的可站立位置。";
            List<Pawn> bound = new List<Pawn>();
            ServantQuery.Instance.GetBoundServants(master, bound);
            foreach (Pawn servant in bound)
                if (servant != null && !servant.Destroyed && !servant.Dead
                    && servant.TryGetComp<CompServantState>()?.PresenceState != ServantPresenceState.Annihilated)
                    return "该御主已有未湮灭的契约从者。";
            return null;
        }

        private sealed class SummoningFailureException : System.Exception
        {
            public SummoningFailureException(string message) : base(message) { }
        }
    }
}
