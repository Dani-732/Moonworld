using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    internal enum HolyGrailWarPage
    {
        Overview,
        Intelligence,
        Battles,
        Contracts,
        Sites
    }

    internal static class HolyGrailWarUi
    {
        internal static bool CanOpen
        {
            get
            {
                GameComponent_MoonWorld war = Current.Game?.GetComponent<GameComponent_MoonWorld>();
                return war?.CurrentWarEntry != null;
            }
        }

        internal static void Open()
        {
            if (!CanOpen)
            {
                Messages.Message("本届圣杯战争尚未建立。", MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new Window_HolyGrailWar());
        }

        internal static Command_Action CreateCommand()
        {
            if (!CanOpen) return null;
            return new Command_Action
            {
                defaultLabel = "圣杯战争",
                defaultDesc = "打开圣杯战争记录板，查看阶段、战况、主从和侦察情报。",
                icon = TexButton.Info,
                action = Open,
                Order = -110f
            };
        }
    }

    internal sealed class Window_HolyGrailWar : Window
    {
        // Crimson values are sampled toward the scarlet cloak accents in the reference art,
        // then darkened for readable text and gold borders in RimWorld's IMGUI.
        private static readonly Color Void = new Color(0.055f, 0.008f, 0.025f);
        private static readonly Color Ink = new Color(0.13f, 0.015f, 0.045f);
        private static readonly Color Wine = new Color(0.36f, 0.025f, 0.075f);
        private static readonly Color WineDeep = new Color(0.22f, 0.012f, 0.045f);
        private static readonly Color Gold = new Color(0.725f, 0.535f, 0.18f);
        private static readonly Color GoldBright = new Color(0.95f, 0.82f, 0.48f);
        private static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
        private static readonly Color Muted = new Color(0.68f, 0.60f, 0.52f);
        private static readonly Color Danger = new Color(0.96f, 0.08f, 0.13f);
        private static readonly Color Amber = new Color(0.78f, 0.60f, 0.27f);
        private static readonly Color Teal = new Color(0.33f, 0.78f, 0.70f);
        private static readonly Color Dead = new Color(0.45f, 0.42f, 0.46f);
        private static readonly Texture2D GoldTex = SolidColorMaterials.NewSolidColorTexture(Gold);
        private static readonly Texture2D WineTex = SolidColorMaterials.NewSolidColorTexture(Wine);
        private static readonly Texture2D WineDeepTex = SolidColorMaterials.NewSolidColorTexture(WineDeep);
        private static readonly Texture2D DangerTex = SolidColorMaterials.NewSolidColorTexture(Danger);
        private static readonly Texture2D TealTex = SolidColorMaterials.NewSolidColorTexture(Teal);
        private static readonly Texture2D EmptyTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.08f, 0.035f, 0.06f));

        private HolyGrailWarPage page;
        private Vector2 scroll;
        private EnemyWarParticipant selected;
        private GUIStyle titleStyle, headingStyle, labelStyle, mutedStyle, tinyStyle, cardStyle, buttonStyle;

        public override Vector2 InitialSize => new Vector2(1120f, 740f);
        public override bool IsDebug => false;
        public Window_HolyGrailWar()
        {
            draggable = true;
            resizeable = true;
            doCloseButton = false;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;
            closeOnAccept = false;
            closeOnCancel = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            BuildStyles();
            GameComponent_MoonWorld war = GetWar();
            selected = war?.CurrentWarEntry?.Participants.Count > 0
                ? war.CurrentWarEntry.Participants[0] : null;
        }

        private void BuildStyles()
        {
            titleStyle = MakeStyle(GameFont.Medium, GoldBright, TextAnchor.MiddleLeft, true);
            headingStyle = MakeStyle(GameFont.Small, GoldBright, TextAnchor.MiddleLeft, true);
            labelStyle = MakeStyle(GameFont.Small, Cream, TextAnchor.UpperLeft, false);
            mutedStyle = MakeStyle(GameFont.Tiny, Muted, TextAnchor.UpperLeft, false);
            tinyStyle = MakeStyle(GameFont.Tiny, Cream, TextAnchor.MiddleLeft, false);
            cardStyle = MakeStyle(GameFont.Small, Cream, TextAnchor.UpperLeft, false);
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 8, 5, 5),
                normal = { textColor = Muted },
                hover = { textColor = GoldBright },
                active = { textColor = Cream }
            };
        }

        private static GUIStyle MakeStyle(GameFont font, Color color, TextAnchor anchor, bool bold)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = anchor,
                wordWrap = true,
                richText = true,
                normal = { textColor = color }
            };
            if (bold) style.fontStyle = FontStyle.Bold;
            return style;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (titleStyle == null) BuildStyles();
            GameComponent_MoonWorld war = GetWar();
            if (war?.CurrentWarEntry == null)
            {
                UiLabel(inRect, "圣杯战争记录已失效。", labelStyle);
                return;
            }

            GUI.color = Void;
            Widgets.DrawBoxSolid(inRect, Void);
            GUI.color = Color.white;
            DrawFrame(inRect);

            Rect header = new Rect(inRect.x + 18f, inRect.y + 14f, inRect.width - 36f, 66f);
            DrawHeader(header, war);
            Rect body = new Rect(inRect.x + 18f, header.yMax + 12f, inRect.width - 36f, inRect.height - header.height - 26f);
            DrawBody(body, war);
        }

        private void DrawFrame(Rect rect)
        {
            GUI.color = Gold;
            Widgets.DrawBox(rect, 1);
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.28f);
            Widgets.DrawBox(rect.ContractedBy(5f), 1);
            GUI.color = Color.white;
            Rect line = new Rect(rect.x + 18f, rect.y + 79f, rect.width - 36f, 1f);
            Widgets.DrawBoxSolid(line, Gold);
            DrawDiamond(new Vector2(line.x, line.y + 0.5f), 5f, GoldBright);
            DrawDiamond(new Vector2(line.xMax, line.y + 0.5f), 5f, GoldBright);
        }

        private void DrawHeader(Rect rect, GameComponent_MoonWorld war)
        {
            Rect title = new Rect(rect.x, rect.y, rect.width * 0.43f, 28f);
            UiLabel(title, "圣杯战争记录板", titleStyle);
            UiLabel(new Rect(title.x, title.y + 29f, title.width, 24f), "MOONWORLD  /  HOLY GRAIL WAR", mutedStyle);

            string phase = WarRhythmPolicy.PhaseLabel(war);
            string countdown = WarRhythmPolicy.FinalBattleDue(war) ? "圣杯决战已到期" : "距第十天决战 " + DaysUntilFinalBattle(war).ToString("0.0") + " 天";
            Rect meta = new Rect(rect.x + rect.width * 0.43f, rect.y + 2f, rect.width * 0.57f, 38f);
            DrawPill(new Rect(meta.x, meta.y, 92f, 28f), phase, GoldBright, Wine);
            DrawPill(new Rect(meta.x + 102f, meta.y, 128f, 28f), WarDay(war), Cream, WineDeep);
            DrawPill(new Rect(meta.x + 238f, meta.y, 148f, 28f), countdown, Muted, WineDeep);
            DrawPill(new Rect(meta.x + 394f, meta.y, 100f, 28f), AliveCount(war) + " / " + war.CurrentWarEntry.Participants.Count, Teal, WineDeep);
        }

        private void DrawBody(Rect rect, GameComponent_MoonWorld war)
        {
            float navWidth = 145f;
            float detailWidth = Mathf.Clamp(rect.width * 0.245f, 225f, 275f);
            Rect nav = new Rect(rect.x, rect.y, navWidth, rect.height);
            Rect detail = new Rect(rect.xMax - detailWidth, rect.y, detailWidth, rect.height);
            Rect main = new Rect(nav.xMax + 12f, rect.y, rect.width - navWidth - detailWidth - 24f, rect.height);
            DrawNavigation(nav);
            DrawPage(main, war);
            DrawDetail(detail, war);
        }

        private void DrawNavigation(Rect rect)
        {
            GUI.color = WineDeep;
            Widgets.DrawBoxSolid(rect, WineDeep);
            GUI.color = Gold;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            UiLabel(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, 20f), "ARCHIVE", mutedStyle);
            DrawTab(new Rect(rect.x + 8f, rect.y + 38f, rect.width - 16f, 40f), HolyGrailWarPage.Overview, "◆  总览");
            DrawTab(new Rect(rect.x + 8f, rect.y + 82f, rect.width - 16f, 40f), HolyGrailWarPage.Intelligence, "◇  情报");
            DrawTab(new Rect(rect.x + 8f, rect.y + 126f, rect.width - 16f, 40f), HolyGrailWarPage.Battles, "⚔  战况");
            DrawTab(new Rect(rect.x + 8f, rect.y + 170f, rect.width - 16f, 40f), HolyGrailWarPage.Contracts, "☽  主从");
            DrawTab(new Rect(rect.x + 8f, rect.y + 214f, rect.width - 16f, 40f), HolyGrailWarPage.Sites, "⌂  据点");
            Rect catalogue = new Rect(rect.x + 8f, rect.y + 270f, rect.width - 16f, 40f);
            GUI.color = WineDeep;
            Widgets.DrawBoxSolid(catalogue, GUI.color);
            GUI.color = Gold;
            Widgets.DrawBox(catalogue, 1);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(catalogue)) Find.WindowStack.Add(new Window_ServantCatalogue());
            UiLabel(catalogue.ContractedBy(5f), "◇  图鉴", buttonStyle);
        }

        private void DrawTab(Rect rect, HolyGrailWarPage target, string label)
        {
            bool active = page == target;
            GUI.color = active ? Wine : new Color(0f, 0f, 0f, 0f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = active ? Gold : new Color(Gold.r, Gold.g, Gold.b, 0.28f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(rect)) page = target;
            UiLabel(rect.ContractedBy(5f), active ? "<color=#F1D27A>" + label + "</color>" : label, buttonStyle);
        }

        private void DrawPage(Rect rect, GameComponent_MoonWorld war)
        {
            GUI.color = new Color(Wine.r, Wine.g, Wine.b, 0.72f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = Gold;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;

            Rect content = rect.ContractedBy(12f);
            Rect view = new Rect(content.x, content.y, content.width, content.height);
            float height = PageHeight(war);
            Rect scrollRect = new Rect(content.x, content.y, content.width, content.height);
            float viewWidth = Mathf.Max(80f, content.width - 32f);
            Widgets.BeginScrollView(scrollRect, ref scroll, new Rect(0f, 0f, viewWidth, height));
            Rect inner = new Rect(0f, 0f, viewWidth, height);
            switch (page)
            {
                case HolyGrailWarPage.Intelligence: DrawIntelligence(inner, war); break;
                case HolyGrailWarPage.Battles: DrawBattles(inner, war); break;
                case HolyGrailWarPage.Contracts: DrawContracts(inner, war); break;
                case HolyGrailWarPage.Sites: DrawSites(inner, war); break;
                default: DrawOverview(inner, war); break;
            }
            Widgets.EndScrollView();
        }

        private float PageHeight(GameComponent_MoonWorld war)
        {
            switch (page)
            {
                case HolyGrailWarPage.Intelligence: return Mathf.Max(480f, war.CurrentWarEntry.Enemies.Count * 125f + 95f);
                case HolyGrailWarPage.Contracts: return Mathf.Max(480f, war.CurrentWarEntry.Participants.Count * 100f + 100f);
                case HolyGrailWarPage.Sites: return 520f;
                case HolyGrailWarPage.Battles: return Mathf.Max(510f, (war.reports?.Count ?? 0) * 102f + 180f);
                default: return Mathf.Max(480f, (war.CurrentWarEntry.Participants.Count + 1) * 134f + 150f);
            }
        }

        private void DrawOverview(Rect rect, GameComponent_MoonWorld war)
        {
            DrawPageTitle(rect, "战争总览", "CURRENT WAR STATUS");
            float y = 53f;
            if (war.enemyBattle != null || war.enemyChallenge != null || war.finalBattleTriggered)
            {
                DrawEventStrip(new Rect(rect.x, y, rect.width, 42f), CurrentEventSummary(war), Danger);
                y += 54f;
            }
            UiLabel(new Rect(rect.x, y, rect.width, 25f), "参战席位", headingStyle);
            UiLabel(new Rect(rect.x, y + 25f, rect.width, 19f), "点击席位查看主从详情", mutedStyle);
            y += 52f;
            List<EnemyWarParticipant> participants = war.CurrentWarEntry.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                float rowHeight = 112f;
                Rect card = new Rect(rect.x, y, rect.width, rowHeight);
                DrawParticipantCard(card, war, participants[i], i == 0, i % 2 == 1);
                y += rowHeight + 9f;
            }
        }

        private void DrawIntelligence(Rect rect, GameComponent_MoonWorld war)
        {
            DrawPageTitle(rect, "情报档案", "RECONNAISSANCE LEDGER");
            DrawEventStrip(new Rect(rect.x, 53f, rect.width, 42f), "从者目击与御主目击分别解锁；未知字段不会被推测填充。", Amber);
            float y = 107f;
            foreach (EnemyWarParticipant participant in war.CurrentWarEntry.Enemies)
            {
                WarReconnaissanceRecord record = WarReconnaissanceService.Find(war, participant);
                Rect row = new Rect(rect.x, y, rect.width, 104f);
                DrawCardBackground(row, false);
                string seat = WarReconnaissanceService.KnowsServant(war, participant) ? participant.Seat?.label ?? "未知席位" : "未知席位";
                string servant = WarReconnaissanceService.KnowsServant(war, participant) ? participant.EnemyIdentity?.label ?? "已确认" : "未知从者";
                string master = WarReconnaissanceService.KnowsMaster(war, participant) ? participant.EnemyMaster?.LabelShortCap ?? "无御主" : "未知御主";
                UiLabel(new Rect(row.x + 12f, row.y + 10f, row.width - 100f, 24f), seat, headingStyle);
                UiLabel(new Rect(row.xMax - 92f, row.y + 10f, 80f, 24f), record.Progress + " / 100", tinyStyle);
                UiLabel(new Rect(row.x + 12f, row.y + 37f, row.width - 24f, 22f), "从者：" + servant + "　御主：" + master, labelStyle);
                DrawUnlockBar(new Rect(row.x + 12f, row.y + 70f, row.width - 24f, 12f), record, war, participant);
                y += 113f;
            }
        }

        private void DrawBattles(Rect rect, GameComponent_MoonWorld war)
        {
            DrawPageTitle(rect, "当前战况与战报", "LIVE ENCOUNTERS / WAR REPORTS");
            float y = 57f;
            bool any = false;
            if (war.enemyBattle != null)
            {
                any = true;
                DrawBattleCard(new Rect(rect.x, y, rect.width, 116f), war, war.enemyBattle);
                y += 126f;
            }
            if (war.enemyChallenge != null)
            {
                any = true;
                DrawChallengeCard(new Rect(rect.x, y, rect.width, 116f), war, war.enemyChallenge);
                y += 126f;
            }
            if (war.finalBattleTriggered)
            {
                any = true;
                DrawEventStrip(new Rect(rect.x, y, rect.width, 46f), "圣杯决战已触发：敌方席位同时进入基地，彼此仍保持敌对。", Danger);
            }
            if (!any) DrawEmpty(new Rect(rect.x, y, rect.width, 72f), "当前没有进行中的交战或约战。");
            y += any ? 64f : 92f;
            UiLabel(new Rect(rect.x, y, rect.width, 25f), "战争记录", headingStyle);
            y += 32f;
            if (war.reports == null || war.reports.Count == 0)
            {
                DrawEmpty(new Rect(rect.x, y, rect.width, 72f), "尚无已记录的战争事件。");
                return;
            }
            for (int i = war.reports.Count - 1; i >= 0; i--)
            {
                WarReportRecord report = war.reports[i];
                if (report == null) continue;
                DrawReportCard(new Rect(rect.x, y, rect.width, 88f), war, report);
                y += 98f;
            }
        }

        private void DrawReportCard(Rect rect, GameComponent_MoonWorld war, WarReportRecord report)
        {
            DrawCardBackground(rect, report.IsActive);
            string left = report.kind == WarReportKind.FinalBattle ? "所有敌方从者" : WarReportService.ParticipantLabel(war, report.actorA);
            string right = report.actorB == null ? "玩家基地" : WarReportService.ParticipantLabel(war, report.actorB);
            string location = report.site == null ? "位置未记录" : "地点 " + report.site.Tile;
            string when = "第 " + Math.Max(0f, (report.startedAtTickAbs - war.warStartTick) / (float)GenDate.TicksPerDay).ToString("0.0") + " 天";
            UiLabel(new Rect(rect.x + 12f, rect.y + 9f, rect.width - 150f, 22f),
                WarReportService.KindLabel(report.kind) + "　" + left + (report.kind == WarReportKind.FinalBattle ? " VS " + right : report.actorB == null ? "" : " VS " + right), headingStyle);
            GUI.color = report.IsActive ? Teal : Muted;
            UiLabel(new Rect(rect.xMax - 136f, rect.y + 10f, 124f, 20f), WarReportService.StateLabel(report), tinyStyle);
            GUI.color = Color.white;
            UiLabel(new Rect(rect.x + 12f, rect.y + 37f, rect.width - 24f, 20f), location + "　" + when, mutedStyle);
            UiLabel(new Rect(rect.x + 12f, rect.y + 61f, rect.width - 24f, 19f),
                (report.rounds > 0 ? "已结算 " + report.rounds + " 回合　" : "") + (report.resultKey ?? "事件已登记"), labelStyle);
        }

        private void DrawContracts(Rect rect, GameComponent_MoonWorld war)
        {
            DrawPageTitle(rect, "主从档案", "MASTER / SERVANT CONTRACT");
            float y = 57f;
            foreach (EnemyWarParticipant participant in war.CurrentWarEntry.Participants)
            {
                bool player = participant == war.CurrentWarEntry.Participants[0];
                bool knowsServant = player || WarReconnaissanceService.KnowsServant(war, participant);
                bool knowsMaster = player || WarReconnaissanceService.KnowsMaster(war, participant);
                string seat = knowsServant ? participant.Seat?.label ?? "未知席位" : "未知席位";
                string servant = knowsServant ? participant.EnemyServant?.LabelShortCap ?? "未知从者" : "未知从者";
                string master = knowsMaster ? participant.EnemyMaster?.LabelShortCap ?? "无御主" : "未知御主";
                string status = participant.EnemyEliminated ? "已退场" : participant.EnemyServant == null ? "未建立契约" : "契约状态读取中";
                Rect row = new Rect(rect.x, y, rect.width, 82f);
                DrawCardBackground(row, player);
                UiLabel(new Rect(row.x + 12f, row.y + 10f, row.width - 100f, 23f), seat, headingStyle);
                UiLabel(new Rect(row.xMax - 90f, row.y + 10f, 78f, 23f), status, tinyStyle);
                UiLabel(new Rect(row.x + 12f, row.y + 38f, row.width - 24f, 25f), "从者：" + servant + "　御主：" + master, labelStyle);
                y += 91f;
            }
        }

        private void DrawSites(Rect rect, GameComponent_MoonWorld war)
        {
            DrawPageTitle(rect, "据点记录", "WORKSHOPS / ENCOUNTER SITES");
            float y = 57f;
            foreach (EnemyWarParticipant participant in war.CurrentWarEntry.Participants)
            {
                bool player = participant == war.CurrentWarEntry.Participants[0];
                if (!player && !WarReconnaissanceService.KnowsSite(war, participant)) continue;
                foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
                {
                    Site_WarWorkshop workshop = worldObject as Site_WarWorkshop;
                    if (workshop == null || workshop.Participant != participant) continue;
                    Rect row = new Rect(rect.x, y, rect.width, 72f);
                    DrawCardBackground(row, player);
                    UiLabel(new Rect(row.x + 12f, row.y + 10f, row.width - 110f, 22f), player ? "玩家工坊" : participant.Seat?.label ?? "已知工坊", headingStyle);
                    UiLabel(new Rect(row.x + 12f, row.y + 37f, row.width - 24f, 22f), "位置：" + workshop.Tile + "　状态：" + WorkshopStatus(participant), labelStyle);
                    y += 81f;
                }
            }
            if (war.enemyChallenge?.site != null)
            {
                Rect row = new Rect(rect.x, y, rect.width, 72f);
                DrawCardBackground(row, false);
                UiLabel(new Rect(row.x + 12f, row.y + 10f, row.width - 110f, 22f), "临时约战地点", headingStyle);
                UiLabel(new Rect(row.x + 12f, row.y + 37f, row.width - 24f, 22f), "位置：" + war.enemyChallenge.site.Tile + "　无工坊所有权、守军或战利品", labelStyle);
            }
        }

        private void DrawDetail(Rect rect, GameComponent_MoonWorld war)
        {
            GUI.color = WineDeep;
            Widgets.DrawBoxSolid(rect, WineDeep);
            GUI.color = Gold;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            UiLabel(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, 18f), "SELECTED RECORD", mutedStyle);
            EnemyWarParticipant participant = selected ?? war.CurrentWarEntry.Participants[0];
            bool player = participant == war.CurrentWarEntry.Participants[0];
            bool knowsServant = player || WarReconnaissanceService.KnowsServant(war, participant);
            bool knowsMaster = player || WarReconnaissanceService.KnowsMaster(war, participant);
            string servant = knowsServant ? participant.EnemyServant?.LabelShortCap ?? "未知从者" : "未知从者";
            string master = knowsMaster ? participant.EnemyMaster?.LabelShortCap ?? "无御主" : "未知御主";
            string seat = knowsServant ? participant.Seat?.label ?? "未知席位" : "未知席位";
            UiLabel(new Rect(rect.x + 12f, rect.y + 37f, rect.width - 24f, 30f), servant, titleStyle);
            UiLabel(new Rect(rect.x + 12f, rect.y + 69f, rect.width - 24f, 22f), seat, headingStyle);
            Widgets.DrawBoxSolid(new Rect(rect.x + 12f, rect.y + 98f, rect.width - 24f, 1f), Gold);
            DrawFact(rect, 117f, "契约", master);
            DrawFact(rect, 155f, "存在", PresenceLabel(participant.EnemyServant));
            DrawFact(rect, 193f, "活动", ActivityLabel(war, participant));
            DrawFact(rect, 231f, "据点", SiteLabel(war, participant, player));
            DrawTag(rect, 278f, "身份" + (knowsServant ? "已确认" : "未知"), knowsServant ? Teal : Amber);
            DrawTag(rect, 309f, "御主" + (knowsMaster ? "已确认" : "未知"), knowsMaster ? Teal : Amber);
            if (player)
            {
                Pawn masterPawn = participant.EnemyMaster;
                int charges = masterPawn?.TryGetComp<CompMasterCommandSpells>()?.Charges ?? 0;
                DrawTag(rect, 340f, "令咒 " + charges + " / " + CommandSpellService.MaximumCharges, GoldBright);
            }
            if (participant.EnemyServant != null && participant.EnemyServant.Spawned && participant.EnemyServant.MapHeld != null)
            {
                Rect button = new Rect(rect.x + 12f, rect.y + rect.height - 124f, rect.width - 24f, 30f);
                if (Widgets.ButtonText(button, "选择地图中的从者"))
                {
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(participant.EnemyServant);
                    Find.CameraDriver.JumpToCurrentMapLoc(participant.EnemyServant.Position);
                }
            }
            ServantIdentityDef knownIdentity = knowsServant ? participant.EnemyIdentity : null;
            if (knownIdentity != null)
            {
                Rect detailButton = new Rect(rect.x + 12f, rect.y + rect.height - 86f, rect.width - 24f, 30f);
                if (Widgets.ButtonText(detailButton, "查看英灵档案"))
                    Find.WindowStack.Add(new Window_ServantDetail(knownIdentity, participant.EnemyServant, player));
            }
            Rect catalogueButton = new Rect(rect.x + 12f, rect.y + rect.height - 48f, rect.width - 24f, 30f);
            if (Widgets.ButtonText(catalogueButton, "打开英灵图鉴"))
                Find.WindowStack.Add(new Window_ServantCatalogue());
        }

        private void DrawFact(Rect rect, float y, string key, string value)
        {
            UiLabel(new Rect(rect.x + 12f, rect.y + y, 48f, 22f), key, mutedStyle);
            UiLabel(new Rect(rect.x + 62f, rect.y + y, rect.width - 74f, 32f), value ?? "未知", tinyStyle);
        }

        private void DrawTag(Rect rect, float y, string text, Color color)
        {
            GUI.color = new Color(color.r, color.g, color.b, 0.35f);
            Widgets.DrawBoxSolid(new Rect(rect.x + 12f, rect.y + y, rect.width - 24f, 24f), GUI.color);
            GUI.color = color;
            Widgets.DrawBox(new Rect(rect.x + 12f, rect.y + y, rect.width - 24f, 24f), 1);
            GUI.color = Color.white;
            UiLabel(new Rect(rect.x + 18f, rect.y + y + 2f, rect.width - 36f, 20f), text, tinyStyle);
        }

        private void DrawPageTitle(Rect rect, string title, string subtitle)
        {
            UiLabel(new Rect(rect.x, rect.y, rect.width * 0.65f, 28f), title, titleStyle);
            UiLabel(new Rect(rect.x + rect.width * 0.65f, rect.y + 3f, rect.width * 0.35f, 22f), subtitle, mutedStyle);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 35f, rect.width, 1f), Gold);
        }

        private void DrawEventStrip(Rect rect, string text, Color accent)
        {
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.18f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = accent;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 3f, rect.height), accent);
            GUI.color = Color.white;
            UiLabel(new Rect(rect.x + 12f, rect.y + 4f, rect.width - 24f, rect.height - 8f), text, tinyStyle);
        }

        private void DrawParticipantCard(Rect rect, GameComponent_MoonWorld war, EnemyWarParticipant participant, bool player, bool alternate)
        {
            bool knowsServant = player || WarReconnaissanceService.KnowsServant(war, participant);
            bool knowsMaster = player || WarReconnaissanceService.KnowsMaster(war, participant);
            bool selectedCard = selected == participant;
            DrawCardBackground(rect, player || selectedCard);
            string seat = knowsServant ? participant.Seat?.label ?? "未知席位" : "未知席位";
            string servant = knowsServant ? participant.EnemyServant?.LabelShortCap ?? "未知从者" : "未知从者";
            string master = knowsMaster ? participant.EnemyMaster?.LabelShortCap ?? "无御主" : "未知御主";
            string state = participant.EnemyEliminated ? "已退场" : ActivityLabel(war, participant);
            Color stateColor = participant.EnemyEliminated ? Dead : player ? Teal : Danger;
            UiLabel(new Rect(rect.x + 12f, rect.y + 9f, rect.width - 130f, 23f), seat + (player ? "  · 玩家" : ""), headingStyle);
            GUI.color = stateColor;
            UiLabel(new Rect(rect.xMax - 116f, rect.y + 10f, 104f, 20f), state, tinyStyle);
            GUI.color = Color.white;
            UiLabel(new Rect(rect.x + 12f, rect.y + 36f, rect.width - 24f, 22f), servant, cardStyle);
            UiLabel(new Rect(rect.x + 12f, rect.y + 61f, rect.width - 24f, 19f), "御主：" + master, mutedStyle);
            string footer = "据点：" + SiteLabel(war, participant, player) + "　" + (player ? "情报完整" : "进度 " + WarReconnaissanceService.Progress(war, participant) + "%");
            UiLabel(new Rect(rect.x + 12f, rect.y + 86f, rect.width - 24f, 19f), footer, mutedStyle);
            if (Widgets.ButtonInvisible(rect)) selected = participant;
        }

        private void DrawCardBackground(Rect rect, bool highlight)
        {
            GUI.color = highlight ? Wine : WineDeep;
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = highlight ? GoldBright : new Color(Gold.r, Gold.g, Gold.b, 0.48f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
        }

        private void DrawUnlockBar(Rect rect, WarReconnaissanceRecord record, GameComponent_MoonWorld war, EnemyWarParticipant participant)
        {
            float width = (rect.width - 8f) / 3f;
            bool servant = WarReconnaissanceService.KnowsServant(war, participant);
            bool master = WarReconnaissanceService.KnowsMaster(war, participant);
            bool site = WarReconnaissanceService.KnowsSite(war, participant);
            DrawUnlockSegment(new Rect(rect.x, rect.y, width, rect.height), servant);
            DrawUnlockSegment(new Rect(rect.x + width + 4f, rect.y, width, rect.height), master);
            DrawUnlockSegment(new Rect(rect.x + (width + 4f) * 2f, rect.y, width, rect.height), site);
        }

        private void DrawUnlockSegment(Rect rect, bool active)
        {
            GUI.color = active ? Teal : EmptyTex == null ? Color.black : new Color(0.08f, 0.035f, 0.06f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = active ? Teal : new Color(Gold.r, Gold.g, Gold.b, 0.45f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
        }

        private void DrawBattleCard(Rect rect, GameComponent_MoonWorld war, EnemyBattleSession battle)
        {
            DrawCardBackground(rect, false);
            string attacker = DescribeServant(war, battle.attacker);
            string defender = DescribeServant(war, battle.defender);
            UiLabel(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 100f, 22f), attacker + "  VS  " + defender, headingStyle);
            UiLabel(new Rect(rect.xMax - 88f, rect.y + 10f, 76f, 22f), "第 " + battle.rounds + " 回合", tinyStyle);
            UiLabel(new Rect(rect.x + 12f, rect.y + 39f, rect.width - 24f, 22f), "地点：" + (battle.site == null ? "未确认" : battle.site.Tile.ToString()) + "　地图：" + (battle.onMap ? "已生成" : "场外回合"), labelStyle);
            UiLabel(new Rect(rect.x + 12f, rect.y + 68f, rect.width - 24f, 22f), "双方真实伤势、装备和魔力继续由从者状态与原版检查器提供。", mutedStyle);
        }

        private void DrawChallengeCard(Rect rect, GameComponent_MoonWorld war, EnemyChallengeSession challenge)
        {
            DrawCardBackground(rect, false);
            UiLabel(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 120f, 22f), "玩家约战", headingStyle);
            GUI.color = Amber;
            UiLabel(new Rect(rect.xMax - 108f, rect.y + 10f, 96f, 22f), "等待赴约", tinyStyle);
            GUI.color = Color.white;
            UiLabel(new Rect(rect.x + 12f, rect.y + 39f, rect.width - 24f, 22f), "发起者：" + challenge.servant?.LabelShortCap + "　地点：" + challenge.site?.Tile, labelStyle);
            float days = Math.Max(0f, (challenge.expiresAtTickAbs - GenTicks.TicksAbs) / (float)GenDate.TicksPerDay);
            UiLabel(new Rect(rect.x + 12f, rect.y + 68f, rect.width - 24f, 22f), "剩余期限：" + days.ToString("0.0") + " 天　普通殖民者不计入到场。", mutedStyle);
        }

        private void DrawEmpty(Rect rect, string text)
        {
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.32f);
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.MiddleCenter;
            UiLabel(rect, text, mutedStyle);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static void DrawPill(Rect rect, string text, Color color, Color background)
        {
            GUI.color = background;
            Widgets.DrawBoxSolid(rect, background);
            GUI.color = color;
            Widgets.DrawBox(rect, 1);
            Text.Anchor = TextAnchor.MiddleCenter;
            UiLabel(rect, text, new GUIStyle(GUI.skin.label)
            { alignment = TextAnchor.MiddleCenter,
                normal = { textColor = color }
            });
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private static void DrawDiamond(Vector2 center, float radius, Color color)
        {
            GUI.color = color;
            Vector3[] points = { new Vector3(center.x, center.y - radius), new Vector3(center.x + radius, center.y), new Vector3(center.x, center.y + radius), new Vector3(center.x - radius, center.y) };
            for (int i = 0; i < points.Length; i++) Widgets.DrawLine(points[i], points[(i + 1) % points.Length], color, 1f);
            GUI.color = Color.white;
        }
        private static void UiLabel(Rect rect, string text, GUIStyle style)
        {
            if (style == null) style = GUI.skin.label;
            style.Draw(rect, new GUIContent(text ?? string.Empty), false, false, false, false);
        }

        private static GameComponent_MoonWorld GetWar()
        { return Current.Game?.GetComponent<GameComponent_MoonWorld>(); }

        private static string WarDay(GameComponent_MoonWorld war)
        {
            if (war.warStartTick < 0) return "战争未开幕";
            return "第 " + (Math.Max(0, Find.TickManager.TicksGame - war.warStartTick) / (float)GenDate.TicksPerDay).ToString("0.0") + " 天";
        }

        private static float DaysUntilFinalBattle(GameComponent_MoonWorld war)
        {
            if (war.warStartTick < 0) return 10f;
            return Math.Max(0f, (war.warStartTick + GenDate.TicksPerDay * 10f - Find.TickManager.TicksGame) / GenDate.TicksPerDay);
        }

        private static int AliveCount(GameComponent_MoonWorld war)
        {
            int count = 0;
            foreach (EnemyWarParticipant participant in war.CurrentWarEntry.Participants)
                if (!participant.EnemyEliminated) count++;
            return count;
        }

        private static string CurrentEventSummary(GameComponent_MoonWorld war)
        {
            if (war.finalBattleTriggered) return "圣杯决战已触发：敌方席位同时进入基地，彼此仍保持敌对。";
            if (war.enemyBattle != null) return "野外交战正在进行：" + DescribeServant(war, war.enemyBattle.attacker) + " × " + DescribeServant(war, war.enemyBattle.defender);
            if (war.enemyChallenge != null) return "玩家约战等待赴约：期限为一天，不会在超时后自动转为突袭。";
            return "当前没有进行中的交战。";
        }

        private static string DescribeServant(GameComponent_MoonWorld war, Pawn pawn)
        {
            EnemyWarParticipant participant = war.CurrentWarEntry.FindEnemy(pawn);
            bool player = participant != null && war.CurrentWarEntry.Participants.IndexOf(participant) == 0;
            return participant == null || player || WarReconnaissanceService.KnowsServant(war, participant) ? pawn?.LabelShortCap ?? "未知从者" : "未知从者";
        }

        private static string PresenceLabel(Pawn pawn)
        {
            if (pawn == null) return "未知";
            CompServantState state = pawn.TryGetComp<CompServantState>();
            if (state == null) return "状态不可用";
            switch (state.PresenceState)
            {
                case ServantPresenceState.VoluntarySpirit: return "主动灵体化";
                case ServantPresenceState.DefeatedSpirit: return "战败灵体化";
                case ServantPresenceState.Annihilated: return "已湮灭";
                default: return "实体化";
            }
        }

        private static string ActivityLabel(GameComponent_MoonWorld war, EnemyWarParticipant participant)
        {
            if (participant == null || participant.EnemyEliminated) return "已退场";
            if (war.enemyBattle != null && war.enemyBattle.Contains(participant.EnemyServant)) return "交战中";
            if (war.enemyChallenge?.servant == participant.EnemyServant) return "约战中";
            if (participant.WorkshopRebuildPending) return "工坊重建中";
            if (participant.EnemyDeployed) return "出击中";
            return "休整/待命";
        }

        private static string SiteLabel(GameComponent_MoonWorld war, EnemyWarParticipant participant, bool player)
        {
            if (!player && !WarReconnaissanceService.KnowsSite(war, participant)) return "未确认";
            foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
                if (worldObject is Site_WarWorkshop workshop && workshop.Participant == participant) return workshop.Tile.ToString();
            return "未建立";
        }

        private static string WorkshopStatus(EnemyWarParticipant participant)
        { return participant.WorkshopRebuildPending ? "等待重建" : participant.EnemyDeployed ? "从者出击" : "正常"; }
    }
}
