using RimWorld;
using Verse;

namespace MoonWorld
{
    internal static class HolyGrailWarQuestService
    {
        private const string WarDescription = "英灵与御主围绕圣杯展开战争。"
            + "\n\n本届敌对从者全部死亡或消散后获得胜利，场外和落单从者同样计入。"
            + "御主死亡或失去令咒使从者失契，默认一天后消散；战败灵体、撤退和工坊失守不代表退场。"
            + "有令咒但无从者的御主保留资格，等待重契约；己方无存续从者且无合格御主时失败。";
        internal static void Ensure(GameComponent_MoonWorld state)
        {
            HolyGrailWarEntry entry = state?.CurrentWarEntry;
            if (state == null || entry == null || state.warStartTick < 0
                || entry.PlayerIdentity == null || entry.EnemyIdentity == null) return;
            if (state.warQuest != null)
            {
                state.warQuest.root = MW_DefOf.MW_HolyGrailWarQuest;
                if (!state.warQuest.Historical)
                {
                    state.warQuest.description = WarDescription;
                    state.warQuest.GetFirstPartOfType<QuestPart_HolyGrailWar>()?.Initialize(state.warStartTick, entry);
                }
                if (!Find.QuestManager.QuestsListForReading.Contains(state.warQuest))
                    Find.QuestManager.Add(state.warQuest);
                return;
            }
            foreach (Quest existing in Find.QuestManager.QuestsListForReading)
            {
                if (existing.root == MW_DefOf.MW_HolyGrailWarQuest
                    || existing.GetFirstPartOfType<QuestPart_HolyGrailWar>() != null)
                {
                    existing.root = MW_DefOf.MW_HolyGrailWarQuest;
                    state.warQuest = existing;
                    if (!existing.Historical)
                    {
                        existing.description = WarDescription;
                        existing.GetFirstPartOfType<QuestPart_HolyGrailWar>()?.Initialize(state.warStartTick, entry);
                    }
                    return;
                }
            }
            // MakeRaw allocates the native unique quest ID and appearance tick.
            Quest quest = Quest.MakeRaw();
            quest.name = "圣杯战争";
            quest.description = WarDescription;
            quest.root = MW_DefOf.MW_HolyGrailWarQuest;
            quest.hidden = false;
            quest.hiddenInUI = false;
            QuestPart_HolyGrailWar part = quest.AddPart<QuestPart_HolyGrailWar>();
            part.Initialize(state.warStartTick, state.CurrentWarEntry);
            quest.SetInitiallyAccepted();
            Find.QuestManager.Add(quest);
            state.warQuest = quest;
        }

        // GameComponent remains the authoritative result; Quest owns native UI/history only.
        internal static void SyncOutcome(GameComponent_MoonWorld state, bool notify)
        {
            Quest quest = state?.warQuest;
            if (quest == null || quest.Historical || state.CurrentWarOutcome == WarOutcome.Ongoing) return;
            quest.GetFirstPartOfType<QuestPart_HolyGrailWar>()?.Initialize(state.warStartTick, state.CurrentWarEntry);
            quest.End(state.CurrentWarOutcome == WarOutcome.PlayerVictory
                ? QuestEndOutcome.Success : QuestEndOutcome.Fail, sendLetter: notify, playSound: notify);
        }
    }
}
