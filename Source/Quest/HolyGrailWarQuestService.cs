using RimWorld;
using Verse;

namespace MoonWorld
{
    internal static class HolyGrailWarQuestService
    {
        internal static void Ensure(GameComponent_MoonWorld state)
        {
            HolyGrailWarEntry entry = state?.CurrentWarEntry;
            if (state == null || entry == null || state.warStartTick < 0 || state.warQuest != null
                || entry.PlayerIdentity == null || entry.EnemyIdentity == null) return;
            Quest quest = new Quest
            {
                name = "圣杯战争",
                description = "七席英灵围绕圣杯展开的战争。",
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
