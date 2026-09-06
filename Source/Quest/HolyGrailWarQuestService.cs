using RimWorld;
using Verse;

namespace MoonWorld
{
    internal static class HolyGrailWarQuestService
    {
        internal static void Ensure(GameComponent_MoonWorld state)
        {
            HolyGrailWarEntry entry = state?.CurrentWarEntry;
            if (state == null || entry == null || state.warStartTick < 0
                || entry.PlayerIdentity == null || entry.EnemyIdentity == null) return;
            if (state.warQuest != null)
            {
                if (!Find.QuestManager.QuestsListForReading.Contains(state.warQuest))
                    Find.QuestManager.Add(state.warQuest);
                return;
            }
            foreach (Quest existing in Find.QuestManager.QuestsListForReading)
            {
                if (existing.GetFirstPartOfType<QuestPart_HolyGrailWar>() != null)
                {
                    state.warQuest = existing;
                    return;
                }
            }
            Quest quest = new Quest
            {
                name = "圣杯战争",
                description = "七席英灵围绕圣杯展开的战争。",
                // QuestManager rejects loaded quests without a root. This native root is only
                // the persistence anchor; the war state lives in QuestPart_HolyGrailWar.
                root = QuestScriptDefOf.WandererJoins,
                hidden = false,
                hiddenInUI = false
            };
            QuestPart_HolyGrailWar part = quest.AddPart<QuestPart_HolyGrailWar>();
            part.Initialize(state.warStartTick, state.CurrentWarEntry);
            quest.SetInitiallyAccepted();
            Find.QuestManager.Add(quest);
            state.warQuest = quest;
        }
    }
}
