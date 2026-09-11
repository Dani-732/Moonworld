using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MoonWorld
{
    internal static class ServantLoreUi
    {
        internal static readonly Color Background = new Color(0.13f, 0.015f, 0.045f);
        internal static readonly Color Panel = new Color(0.36f, 0.025f, 0.075f);
        internal static readonly Color PanelDeep = new Color(0.22f, 0.012f, 0.045f);
        internal static readonly Color Gold = new Color(0.725f, 0.535f, 0.18f);
        internal static readonly Color GoldBright = new Color(0.95f, 0.82f, 0.48f);
        internal static readonly Color Cream = new Color(0.96f, 0.92f, 0.82f);
        internal static readonly Color Muted = new Color(0.68f, 0.60f, 0.52f);
        internal static readonly Color Teal = new Color(0.33f, 0.78f, 0.70f);
        private static Texture2D pattern;
        private static readonly Dictionary<string, Texture2D> classBacks = new Dictionary<string, Texture2D>();

        internal static Texture2D Pattern
        {
            get
            {
                if (pattern != null) return pattern;
                pattern = new Texture2D(48, 48, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                Color[] pixels = new Color[48 * 48];
                for (int y = 0; y < 48; y++)
                    for (int x = 0; x < 48; x++)
                    {
                        float diamond = Mathf.Abs((x - 23.5f) + (y - 23.5f)) + Mathf.Abs((x - 23.5f) - (y - 23.5f));
                        float glow = diamond < 18f ? 0.055f : 0f;
                        pixels[y * 48 + x] = new Color(0.18f + glow, 0.008f, 0.025f + glow * 0.4f, 0.34f);
                    }
                pattern.SetPixels(pixels);
                pattern.Apply(false, true);
                return pattern;
            }
        }

        internal static Texture2D ClassBack(ServantIdentityDef identity)
        {
            string key = identity?.warClass.ToString() ?? "Unknown";
            if (classBacks.TryGetValue(key, out Texture2D existing)) return existing;
            Color baseColor = ClassColor(key);
            Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[96 * 96];
            for (int y = 0; y < 96; y++) for (int x = 0; x < 96; x++)
            {
                float dx = x - 47.5f, dy = y - 47.5f;
                float ring = Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - 30f) < 2f ? 0.18f : 0f;
                pixels[y * 96 + x] = new Color(baseColor.r + ring, baseColor.g + ring * 0.4f, baseColor.b + ring * 0.2f, 0.72f);
            }
            texture.SetPixels(pixels); texture.Apply(false, true); classBacks[key] = texture; return texture;
        }

        private static Color ClassColor(string key)
        {
            return new Color(0.25f, 0.012f, 0.045f);
        }

        internal static GUIStyle Style(GameFont font, Color color, TextAnchor anchor, bool bold = false)
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

        internal static void Label(Rect rect, string text, GUIStyle style)
        { (style ?? GUI.skin.label).Draw(rect, new GUIContent(text ?? string.Empty), false, false, false, false); }

        internal static void Frame(Rect rect)
        { Frame(rect, null); }

        internal static void Frame(Rect rect, ServantIdentityDef identity)
        {
            GUI.color = Background;
            Widgets.DrawBoxSolid(rect, Background);
            GUI.color = Color.white;
            GUI.DrawTexture(rect.ContractedBy(2f), identity == null ? Pattern : ClassBack(identity), ScaleMode.ScaleAndCrop, true);
            GUI.color = Gold;
            Widgets.DrawBox(rect, 1);
            GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            Widgets.DrawBox(rect.ContractedBy(5f), 1);
            DrawCorner(new Vector2(rect.x + 10f, rect.y + 10f));
            DrawCorner(new Vector2(rect.xMax - 10f, rect.y + 10f));
            DrawCorner(new Vector2(rect.x + 10f, rect.yMax - 10f));
            DrawCorner(new Vector2(rect.xMax - 10f, rect.yMax - 10f));
            GUI.color = Color.white;
        }

        private static void DrawCorner(Vector2 center)
        {
            GUI.color = GoldBright;
            GUI.DrawTexture(new Rect(center.x - 4f, center.y - 4f, 8f, 8f), BaseContent.WhiteTex);
            GUI.color = Background;
            GUI.DrawTexture(new Rect(center.x - 2f, center.y - 2f, 4f, 4f), BaseContent.WhiteTex);
            GUI.color = Color.white;
        }

        internal static void Heading(Rect rect, string title, string subtitle, GUIStyle titleStyle, GUIStyle mutedStyle)
        {
            Label(new Rect(rect.x, rect.y, rect.width * 0.58f, 30f), title, titleStyle);
            Label(new Rect(rect.x + rect.width * 0.58f, rect.y + 4f, rect.width * 0.42f, 22f), subtitle, mutedStyle);
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 38f, rect.width, 1f), Gold);
        }

        internal static ServantLoreDef Lore(ServantIdentityDef identity)
        {
            if (identity == null) return null;
            foreach (ServantLoreDef lore in DefDatabase<ServantLoreDef>.AllDefsListForReading)
                if (lore.Identity == identity) return lore;
            return null;
        }

        internal static string Name(ServantIdentityDef identity, ServantLoreDef lore)
        { return lore?.DisplayName ?? identity?.fixedName ?? identity?.label ?? identity?.defName ?? "未知英灵"; }

        internal static string ClassLabel(ServantIdentityDef identity)
        { return HolyGrailWarClassDef.For(identity)?.label ?? identity?.warClass.ToString() ?? "未知职阶"; }
    }

    internal sealed class Window_ServantDetail : Window
    {
        private readonly ServantIdentityDef identity;
        private readonly Pawn pawn;
        private readonly bool ownServant;
        private Vector2 scroll;
        private GUIStyle titleStyle, headingStyle, labelStyle, mutedStyle, tinyStyle;

        public override Vector2 InitialSize => new Vector2(820f, 690f);
        public override bool IsDebug => false;

        internal Window_ServantDetail(ServantIdentityDef identity, Pawn pawn, bool ownServant)
        {
            this.identity = identity;
            this.pawn = pawn;
            this.ownServant = ownServant;
            draggable = true;
            resizeable = true;
            doCloseButton = false;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            titleStyle = ServantLoreUi.Style(GameFont.Medium, ServantLoreUi.GoldBright, TextAnchor.MiddleLeft, true);
            headingStyle = ServantLoreUi.Style(GameFont.Small, ServantLoreUi.GoldBright, TextAnchor.MiddleLeft, true);
            labelStyle = ServantLoreUi.Style(GameFont.Small, ServantLoreUi.Cream, TextAnchor.UpperLeft);
            mutedStyle = ServantLoreUi.Style(GameFont.Tiny, ServantLoreUi.Muted, TextAnchor.UpperLeft);
            tinyStyle = ServantLoreUi.Style(GameFont.Tiny, ServantLoreUi.Cream, TextAnchor.MiddleLeft);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (titleStyle == null) PreOpen();
            ServantLoreUi.Frame(inRect, identity);
            ServantLoreDef lore = ServantLoreUi.Lore(identity);
            float width = inRect.width - 28f;
            Rect header = new Rect(inRect.x + 14f, inRect.y + 13f, width, 76f);
            ServantLoreUi.Heading(header, ServantLoreUi.Name(identity, lore), "SERVANT RECORD", titleStyle, mutedStyle);
            ServantLoreUi.Label(new Rect(header.x, header.y + 48f, width, 22f), ServantLoreUi.ClassLabel(identity) + (string.IsNullOrEmpty(lore?.epithet) ? "" : "　·　" + lore.epithet), headingStyle);

            Rect scrollRect = new Rect(inRect.x + 14f, header.yMax + 10f, width, inRect.height - header.height - 24f);
            float contentWidth = width - 26f;
            float contentHeight = ContentHeight(lore, contentWidth);
            Widgets.BeginScrollView(scrollRect, ref scroll, new Rect(0f, 0f, contentWidth, contentHeight));
            Rect content = new Rect(0f, 0f, contentWidth, contentHeight);
            DrawContent(content, lore);
            Widgets.EndScrollView();
        }

        private float ContentHeight(ServantLoreDef lore, float contentWidth)
        {
            if (lore == null) return 400f;
            float valueWidth = Mathf.Max(80f, contentWidth - 153f);
            float height = 264f + 30f + 3f * 43f + 4f + 16f;
            height += SectionHeight(valueWidth, lore.epithet, lore.origin, lore.gender, lore.alignment, lore.heightWeight) + 12f;
            height += SectionHeight(valueWidth, lore.parameters) + 12f;
            height += SectionHeight(valueWidth, lore.classSkills) + 12f;
            height += SectionHeight(valueWidth, lore.noblePhantasm) + 12f;
            height += SectionHeight(valueWidth, lore.summary) + 14f;
            height += 30f + FactHeight(valueWidth, "实体化") + FactHeight(valueWidth, "已契约");
            if (ownServant && pawn?.needs?.TryGetNeed<Need_Prana>() != null)
                height += FactHeight(valueWidth, "100 / 100");
            return Mathf.Max(400f, height + 55f);
        }

        private static float SectionHeight(float valueWidth, params string[] values)
        {
            float height = 28f;
            foreach (string value in values)
                if (!string.IsNullOrEmpty(value)) height += FactHeight(valueWidth, value);
            return height;
        }

        private static float FactHeight(float valueWidth, string value)
        {
            return Mathf.Max(28f, Text.CalcHeight(value ?? "未知", valueWidth)) + 7f;
        }

        private void DrawContent(Rect rect, ServantLoreDef lore)
        {
            if (lore == null)
            {
                DrawFallback(rect);
                return;
            }
            float y = 0f;
            DrawOriginalProfile(rect, ref y, lore);
            y += 16f;
            DrawSection(rect, ref y, "基本资料", new[] {
                MakePair("称号", lore.epithet), MakePair("出典", lore.origin), MakePair("性别", lore.gender), MakePair("属性", lore.alignment), MakePair("身高·体重", lore.heightWeight)
            });
            y += 12f;
            DrawSection(rect, ref y, "参数", new[] { MakePair("能力参数", lore.parameters) });
            y += 12f;
            DrawSection(rect, ref y, "职阶能力", new[] { MakePair("Class Skills", lore.classSkills) });
            y += 12f;
            DrawSection(rect, ref y, "宝具", new[] { MakePair("Noble Phantasm", lore.noblePhantasm) });
            y += 12f;
            DrawSection(rect, ref y, "人物记录", new[] { MakePair("简介", lore.summary) });
            y += 14f;
            DrawLiveStatus(rect, ref y);
            ServantLoreUi.Label(new Rect(rect.x, y + 13f, rect.width, 35f), ownServant ? "动态状态来自当前 Pawn，可查看精确魔力与存在状态。" : "静态设定与当前战争情报分开；敌方精确生命、伤势和魔力仍由原版检查器提供。", mutedStyle);
        }

        private void DrawOriginalProfile(Rect rect, ref float y, ServantLoreDef lore)
        {
            float portraitWidth = Mathf.Min(235f, rect.width * 0.32f);
            Rect portrait = new Rect(rect.xMax - portraitWidth, y, portraitWidth, 250f);
            GUI.color = new Color(0.10f, 0.008f, 0.018f, 1f);
            Widgets.DrawBoxSolid(portrait, GUI.color);
            GUI.color = ServantLoreUi.Gold;
            Widgets.DrawBox(portrait, 2);
            GUI.color = Color.white;
            if (pawn != null)
                Widgets.ThingIcon(new Rect(portrait.x + 18f, portrait.y + 18f, portrait.width - 36f, portrait.height - 36f), pawn, 1f);
            else
                ServantLoreUi.Label(new Rect(portrait.x, portrait.y + 88f, portrait.width, 70f),
                    ServantLoreUi.ClassLabel(identity).ToUpperInvariant(),
                    ServantLoreUi.Style(GameFont.Medium, ServantLoreUi.GoldBright, TextAnchor.MiddleCenter, true));

            float leftWidth = rect.width - portraitWidth - 18f;
            ServantLoreUi.Label(new Rect(rect.x, y, leftWidth, 26f), "CLASS", ServantLoreUi.Style(GameFont.Small, ServantLoreUi.GoldBright, TextAnchor.MiddleLeft, true));
            ServantLoreUi.Label(new Rect(rect.x, y + 30f, leftWidth, 42f), ServantLoreUi.ClassLabel(identity), ServantLoreUi.Style(GameFont.Medium, ServantLoreUi.Cream, TextAnchor.MiddleLeft, true));
            DrawProfileRow(new Rect(rect.x, y + 82f, leftWidth, 28f), "御主", pawn == null ? "未确认" : ServantQuery.Instance.GetMaster(pawn)?.LabelShortCap ?? "失契");
            DrawProfileRow(new Rect(rect.x, y + 114f, leftWidth, 28f), "真名", ServantLoreUi.Name(identity, lore));
            DrawProfileRow(new Rect(rect.x, y + 146f, leftWidth, 28f), "属性", lore.alignment);
            DrawProfileRow(new Rect(rect.x, y + 178f, leftWidth, 28f), "性别", lore.gender);
            DrawProfileRow(new Rect(rect.x, y + 210f, leftWidth, 28f), "身高·体重", lore.heightWeight);
            y += 264f;

            ServantLoreUi.Label(new Rect(rect.x, y, rect.width, 24f), "能力参数", headingStyle);
            y += 30f;
            string[] names = { "筋力", "耐久", "敏捷", "魔力", "幸运", "宝具" };
            string[] values = ParseRanks(lore.parameters);
            float gap = 10f;
            float cellWidth = (rect.width - gap) / 2f;
            for (int i = 0; i < names.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                DrawParameterBox(new Rect(rect.x + col * (cellWidth + gap), y + row * 43f, cellWidth, 36f), names[i], values[i]);
            }
            y += 3 * 43f + 4f;
        }

        private void DrawProfileRow(Rect rect, string key, string value)
        {
            GUI.color = new Color(0.29f, 0.055f, 0.08f, 1f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = ServantLoreUi.Gold;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            ServantLoreUi.Label(new Rect(rect.x + 8f, rect.y, 82f, rect.height), key, mutedStyle);
            ServantLoreUi.Label(new Rect(rect.x + 92f, rect.y, rect.width - 100f, rect.height), value ?? "未知", tinyStyle);
        }

        private void DrawParameterBox(Rect rect, string name, string value)
        {
            GUI.color = new Color(0.34f, 0.045f, 0.08f, 1f);
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = ServantLoreUi.GoldBright;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            ServantLoreUi.Label(new Rect(rect.x + 8f, rect.y, 46f, rect.height), name, tinyStyle);
            int rank = RankValue(value);
            for (int i = 0; i < 5; i++)
            {
                Rect bar = new Rect(rect.x + 58f + i * 19f, rect.y + 10f, 15f, 16f);
                GUI.color = i < rank ? new Color(0.94f, 0.22f, 0.15f) : new Color(0.20f, 0.03f, 0.04f);
                Widgets.DrawBoxSolid(bar, GUI.color);
                GUI.color = ServantLoreUi.Gold;
                Widgets.DrawBox(bar, 1);
            }
            GUI.color = Color.white;
            ServantLoreUi.Label(new Rect(rect.xMax - 48f, rect.y, 40f, rect.height), value ?? "—", headingStyle);
        }

        private static string[] ParseRanks(string text)
        {
            string[] result = { "—", "—", "—", "—", "—", "—" };
            if (string.IsNullOrEmpty(text)) return result;
            string[] parts = text.Split(new[] { '　', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int slot = 0;
            for (int i = 1; i < parts.Length && slot < result.Length; i += 2) result[slot++] = parts[i];
            return result;
        }

        private static int RankValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (value.StartsWith("EX", StringComparison.OrdinalIgnoreCase)) return 5;
            switch (value[0]) { case 'E': return 1; case 'D': return 2; case 'C': return 3; case 'B': return 4; case 'A': return 5; default: return 0; }
        }

        private void DrawFallback(Rect rect)
        {
            ServantLoreUi.Label(new Rect(rect.x, 20f, rect.width, 28f), "该英灵尚未配置静态设定卡。", headingStyle);
            ServantLoreUi.Label(new Rect(rect.x, 61f, rect.width, 70f), "当前可显示的动态身份来自已安装内容依赖；不会根据缺失字段猜测身世、参数或宝具。", labelStyle);
            float y = 155f;
            DrawLiveStatus(rect, ref y);
        }

        private void DrawLiveStatus(Rect rect, ref float y)
        {
            ServantLoreUi.Label(new Rect(rect.x, y, rect.width, 24f), "当前状态", headingStyle);
            y += 30f;
            string presence = "未生成";
            string contract = "未知";
            if (pawn != null)
            {
                presence = Presence(pawn);
                contract = ServantQuery.Instance.GetMaster(pawn)?.LabelShortCap ?? "失契/无御主";
            }
            DrawFact(rect, ref y, "存在", presence);
            DrawFact(rect, ref y, "契约", contract);
            if (ownServant && pawn?.needs?.TryGetNeed<Need_Prana>() != null)
            {
                Need_Prana prana = pawn.needs.TryGetNeed<Need_Prana>();
                DrawFact(rect, ref y, "魔力", Mathf.RoundToInt(prana.CurLevel) + " / " + Mathf.RoundToInt(prana.MaxLevel));
            }
        }

        private void DrawSection(Rect rect, ref float y, string title, Pair[] pairs)
        {
            ServantLoreUi.Label(new Rect(rect.x, y, rect.width, 24f), title, headingStyle);
            y += 28f;
            foreach (Pair pair in pairs)
            {
                if (string.IsNullOrEmpty(pair.Value)) continue;
                DrawFact(rect, ref y, pair.Key, pair.Value);
            }
        }

        private void DrawFact(Rect rect, ref float y, string key, string value)
        {
            float height = Mathf.Max(28f, Text.CalcHeight(value ?? "未知", rect.width - 150f));
            ServantLoreUi.Label(new Rect(rect.x + 8f, y, 130f, height), key, mutedStyle);
            ServantLoreUi.Label(new Rect(rect.x + 145f, y, rect.width - 153f, height), value ?? "未知", labelStyle);
            y += height + 7f;
        }

        private static string Presence(Pawn pawn)
        {
            CompServantState state = pawn?.TryGetComp<CompServantState>();
            if (state == null) return pawn == null ? "未生成" : "状态不可用";
            switch (state.PresenceState)
            {
                case ServantPresenceState.VoluntarySpirit: return "主动灵体化";
                case ServantPresenceState.DefeatedSpirit: return "战败灵体化";
                case ServantPresenceState.Annihilated: return "已湮灭";
                default: return "实体化";
            }
        }

        private struct Pair
        {
            internal string Key, Value;
            internal Pair(string key, string value) { Key = key; Value = value; }
        }

        private static Pair MakePair(string key, string value) { return new Pair(key, value); }
    }

    internal sealed class Window_ServantCatalogue : Window
    {
        private HolyGrailWarClassDef selectedClass;
        private Vector2 scroll;
        private GUIStyle titleStyle, headingStyle, labelStyle, mutedStyle, tinyStyle;

        public override Vector2 InitialSize => new Vector2(940f, 700f);
        public override bool IsDebug => false;

        internal Window_ServantCatalogue()
        {
            draggable = true;
            resizeable = true;
            doCloseButton = false;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            closeOnCancel = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            titleStyle = ServantLoreUi.Style(GameFont.Medium, ServantLoreUi.GoldBright, TextAnchor.MiddleLeft, true);
            headingStyle = ServantLoreUi.Style(GameFont.Small, ServantLoreUi.GoldBright, TextAnchor.MiddleLeft, true);
            labelStyle = ServantLoreUi.Style(GameFont.Small, ServantLoreUi.Cream, TextAnchor.UpperLeft);
            mutedStyle = ServantLoreUi.Style(GameFont.Tiny, ServantLoreUi.Muted, TextAnchor.UpperLeft);
            tinyStyle = ServantLoreUi.Style(GameFont.Tiny, ServantLoreUi.Cream, TextAnchor.MiddleLeft);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (titleStyle == null) PreOpen();
            ServantLoreUi.Frame(inRect);
            ServantLoreUi.Heading(new Rect(inRect.x + 14f, inRect.y + 13f, inRect.width - 28f, 58f), "英灵图鉴", "SERVANT CATALOGUE", titleStyle, mutedStyle);
            Rect filter = new Rect(inRect.x + 14f, inRect.y + 82f, inRect.width - 28f, 42f);
            DrawClassFilter(filter);
            Rect listRect = new Rect(inRect.x + 14f, filter.yMax + 10f, inRect.width - 28f, inRect.height - 128f);
            List<ServantIdentityDef> identities = Identities();
            float height = Mathf.Max(listRect.height, identities.Count * 94f + 10f);
            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, listRect.width - 26f, height));
            Rect content = new Rect(0f, 0f, listRect.width - 26f, height);
            float y = 0f;
            foreach (ServantIdentityDef identity in identities)
            {
                ServantLoreDef lore = ServantLoreUi.Lore(identity);
                Rect card = new Rect(0f, y, content.width, 82f);
                DrawIdentityCard(card, identity, lore);
                y += 91f;
            }
            if (identities.Count == 0) ServantLoreUi.Label(new Rect(0f, 25f, content.width, 40f), "当前没有可浏览的英灵资料。", mutedStyle);
            Widgets.EndScrollView();
        }

        private void DrawClassFilter(Rect rect)
        {
            float width = rect.width / 8f;
            DrawClassButton(new Rect(rect.x, rect.y, width - 4f, rect.height), null, "全部");
            List<HolyGrailWarClassDef> classes = new List<HolyGrailWarClassDef>(DefDatabase<HolyGrailWarClassDef>.AllDefsListForReading);
            classes.Sort((a, b) => string.Compare(a.label, b.label, StringComparison.Ordinal));
            for (int i = 0; i < classes.Count && i < 7; i++)
                DrawClassButton(new Rect(rect.x + width * (i + 1), rect.y, width - 4f, rect.height), classes[i], classes[i].label);
        }

        private void DrawClassButton(Rect rect, HolyGrailWarClassDef classDef, string label)
        {
            bool active = selectedClass == classDef;
            GUI.color = active ? ServantLoreUi.Panel : ServantLoreUi.PanelDeep;
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = active ? ServantLoreUi.GoldBright : ServantLoreUi.Gold;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            if (Widgets.ButtonInvisible(rect)) selectedClass = classDef;
            ServantLoreUi.Label(rect.ContractedBy(3f), label, active ? headingStyle : tinyStyle);
        }

        private List<ServantIdentityDef> Identities()
        {
            List<ServantIdentityDef> result = new List<ServantIdentityDef>();
            foreach (ServantIdentityDef identity in DefDatabase<ServantIdentityDef>.AllDefsListForReading)
            {
                if (!identity.summonable || identity.servantKind == null) continue;
                if (selectedClass != null && HolyGrailWarClassDef.For(identity) != selectedClass) continue;
                result.Add(identity);
            }
            result.Sort((a, b) => string.Compare(ServantLoreUi.Name(a, ServantLoreUi.Lore(a)), ServantLoreUi.Name(b, ServantLoreUi.Lore(b)), StringComparison.Ordinal));
            return result;
        }

        private void DrawIdentityCard(Rect rect, ServantIdentityDef identity, ServantLoreDef lore)
        {
            GUI.color = ServantLoreUi.PanelDeep;
            Widgets.DrawBoxSolid(rect, GUI.color);
            GUI.color = ServantLoreUi.Gold;
            Widgets.DrawBox(rect, 1);
            GUI.color = Color.white;
            ServantLoreUi.Label(new Rect(rect.x + 12f, rect.y + 10f, rect.width - 150f, 24f), ServantLoreUi.Name(identity, lore), headingStyle);
            ServantLoreUi.Label(new Rect(rect.x + 12f, rect.y + 38f, rect.width - 170f, 22f), ServantLoreUi.ClassLabel(identity) + (string.IsNullOrEmpty(lore?.epithet) ? "" : "　·　" + lore.epithet), mutedStyle);
            string summary = lore?.summary ?? "尚未配置静态设定卡。";
            ServantLoreUi.Label(new Rect(rect.x + 12f, rect.y + 59f, rect.width - 170f, 18f), summary, tinyStyle);
            Rect button = new Rect(rect.xMax - 145f, rect.y + 22f, 130f, 34f);
            if (Widgets.ButtonText(button, "查看详情"))
                Find.WindowStack.Add(new Window_ServantDetail(identity, null, false));
        }
    }
}
