using System;
using System.Collections.Generic;
using Verse;

namespace MoonWorld
{
    internal sealed class WarRosterPreparation : IDisposable
    {
        private readonly List<EnemyWarPreparation> preparations = new List<EnemyWarPreparation>();
        internal List<EnemyWarParticipant> Participants { get; } = new List<EnemyWarParticipant>();
        internal void Prepare(Map map, ServantIdentityDef player)
        {
            var pools = ServantSummonPoolDef.Candidates();
            var playerSeat = HolyGrailWarClassDef.For(player);
            var factions = new HashSet<RimWorld.FactionDef>();
            foreach (var seat in DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading)
                if (seat.participatesInWar && (seat.oppositionFaction == null || !seat.oppositionFaction.hidden
                    || !seat.oppositionFaction.permanentEnemy || !factions.Add(seat.oppositionFaction)))
                    throw new InvalidOperationException("参战职阶必须使用独立、隐藏且永久敌对的派系：" + seat.defName);
            foreach (var seat in DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading)
            {
                if (!seat.participatesInWar || seat == playerSeat) continue;
                if (!pools.TryGetValue(seat, out var candidates) || candidates.Count == 0)
                    throw new InvalidOperationException("参战职阶缺少有效召唤池：" + seat.defName);
                var preparation = new EnemyWarPreparation();
                preparations.Add(preparation);
                preparation.Prepare(map, candidates.RandomElement());
                Participants.Add(preparation.Participant);
            }
            if (Participants.Count == 0) throw new InvalidOperationException("至少需要一个敌方参战职阶。");
            ValidatePrepared();
        }
        internal void ValidatePrepared()
        { foreach (var preparation in preparations) preparation.ValidatePrepared(); }
        internal void Commit()
        { foreach (var preparation in preparations) preparation.Commit(); }
        public void Dispose()
        {
            // Reverse order also removes cross-faction relations before older factions disappear.
            for (int i = preparations.Count - 1; i >= 0; i--)
                try { preparations[i].Dispose(); }
                catch (Exception ex) { Log.Error("[MoonWorld] 阵营准备回滚异常：" + ex); }
        }
    }
}
