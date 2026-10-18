using Game.Board;
using Runestone.AesirModules;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 关卡结算 UI 生成器：产物落在 Assets/Resources/UIPrefabs/（项目 UI 资产约定，
    /// 运行时由 UIModule 的 ResourcesUILoader 按 <see cref="Game.Core.UIPaths" /> 的 path 加载）。
    /// <para>
    /// 产物：LevelClearPanel（通关成功，带星级与用时）、LevelFailPanel（通关失败），
    /// 两者都是 **Panel 形态**：根只有 RectTransform（无 Canvas / CanvasScaler / GraphicRaycaster，
    /// 那些由 UIRoot 的层 Canvas 提供），挂 Popup 层，全屏 Mask 拦截射线。
    /// 重跑即覆盖产物；要改结构改这里，禁止在产物 prefab 上手加节点。
    /// </para>
    /// <para>
    /// 皮肤 = 参考文档「旧魔导书 Grimoire」那套：全屏暗紫蒙版 + 深紫描边背板（<see cref="UiSpritePainter" />）
    /// + 做旧羊皮纸内页（Tip_Parchment）+ 古铜金分隔线 + 暗紫描金按钮牌匾，文字走深紫（纸面浅色，
    /// 与棋盘运行时面板那套「深底浅字」相反）。失败页复用同一副壳，只是把内页染成灰紫红、标题转暗红。
    /// 同时同步结算文案到 GameText 表（zh-CN / en）——文案真源在表里，预制体文本节点一律留空。
    /// </para>
    /// </summary>
    public static class BoardResultPanelBuilder
    {
        const string Dir = "Assets/Resources/UIPrefabs";
        const string ClearPath = Dir + "/LevelClearPanel.prefab";
        const string FailPath = Dir + "/LevelFailPanel.prefab";

        const string TableZh = "Assets/Localization/Tables/GameText_zh-CN.asset";
        const string TableEn = "Assets/Localization/Tables/GameText_en.asset";

        /// <summary>项目中文字体资产（TMPChineseText 扩展落地），不足时回退 TMP 默认字体。</summary>
        const string TmpChineseFontPath = "Assets/Codely/Fonts/NotoSansSC-Regular SDF.asset";

        /// <summary>星级图标（与选关节点同一套美术）。</summary>
        const string StarSpritePath = "Assets/Resources/UIPrefabs/UIimg/star.png";

        /// <summary>羊皮纸卷轴内页（与关卡成就小窗同一张，1024×576、9-slice 边 40）。</summary>
        const string ParchmentPath = "Assets/Art/Board/UI/Tip_Parchment.png";

        // ---- 布局（1920×1080 设计分辨率；卡片按像素固定尺寸居中，内部按「距左/距顶/宽/高」摆放）----
        const float FrameWidth = 1000f;
        const float FrameHeight = 760f;
        const float CardInset = 28f;

        const float TitleLeft = 60f;
        const float TitleTop = 30f;
        const float TitleHeight = 96f;
        const float TitleFontSize = 64f;

        const float DividerLeft = 150f;
        const float DividerTop = 134f;
        const float DividerWidth = CardWidth - DividerLeft * 2f;
        const float DividerHeight = 4f;

        const float CardWidth = FrameWidth - CardInset * 2f;

        const float StarRowLeft = 120f;
        const float StarRowTop = 156f;
        const float StarRowWidth = 700f;
        const float StarRowHeight = 150f;
        const float StarSize = 130f;
        const float StarGap = 200f;

        const float RowLeft = 90f;
        const float RowWidth = 760f;
        const float RowHeight = 46f;
        const float RowFontSize = 36f;

        const float StepsTop = 330f;
        const float PiecesTop = 384f;
        const float TimeTop = 438f;
        const float FailStepsTop = 300f;
        const float FailPiecesTop = 354f;

        const float ButtonBottom = 46f;
        const float ButtonWidth = 330f;
        const float ButtonHeight = 88f;
        const float ButtonLeftX = 105f;
        const float ButtonRightX = 505f;
        const float ButtonFontSize = 34f;

        // ---- 配色（旧魔导书：羊皮纸浅面 → 文字走深紫）----
        static readonly Color MaskColor = new Color(0.043f, 0f, 0.086f, 0.80f);
        static readonly Color GoldColor = new Color(0.78f, 0.63f, 0.33f, 0.9f);

        static readonly Color ClearCardTint = Color.white;
        static readonly Color ClearTitleColor = new Color(0.165f, 0.078f, 0.212f, 1f);
        static readonly Color ClearTextColor = new Color(0.294f, 0.200f, 0.322f, 1f);

        static readonly Color FailCardTint = new Color(0.86f, 0.78f, 0.80f, 1f);
        static readonly Color FailTitleColor = new Color(0.369f, 0.106f, 0.129f, 1f);
        static readonly Color FailTextColor = new Color(0.353f, 0.216f, 0.235f, 1f);

        /// <summary>牌匾是深底，文字反过来走浅金。</summary>
        static readonly Color ButtonLabelColor = new Color(0.98f, 0.94f, 0.86f, 1f);

        static TMP_FontAsset _chineseFont;
        static bool _fontLookedUp;

        static TMP_FontAsset ChineseFont
        {
            get
            {
                if (!_fontLookedUp)
                {
                    _fontLookedUp = true;
                    _chineseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpChineseFontPath);
                    if (_chineseFont == null)
                        Debug.LogWarning($"[BoardResultPanelBuilder] 未找到中文字体资产 {TmpChineseFontPath}，TMP 文本将使用默认字体（中文可能缺字）");
                }

                return _chineseFont;
            }
        }

        [MenuItem("Tools/关卡/重建关卡结算面板")]
        public static void RebuildAll()
        {
            SyncResultTexts();          // 先补文案：面板文本节点留空，运行时从表取值
            BuildClearPanel();
            BuildFailPanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[关卡] 结算面板已重建：" + ClearPath + " / " + FailPath);
        }

        // ---------- 通关成功 ----------

        static void BuildClearPanel()
        {
            var (root, card) = BuildShell<LevelClearPanel>("LevelClearPanel",
                ClearCardTint, ClearTitleColor, ClearTextColor, withStars: true);

            // 成功面板独有 1：星级行（三颗星，运行时逐颗点亮，这里只建空位）
            var starRow = BuildStars(card);

            // 成功面板独有 2：本局用时（空文本，运行时由 LevelClearPanel.ApplyResultText 填）
            var timeRow = NewRect("TimeLabel", card);
            AnchorTopLeft(timeRow, RowLeft, TimeTop, RowWidth, RowHeight);
            var timeLabel = NewTmp(timeRow, string.Empty, RowFontSize, ClearTextColor);

            var so = new SerializedObject(root.GetComponent<LevelClearPanel>());
            so.FindProperty("timeLabel").objectReferenceValue = timeLabel;
            so.FindProperty("starRow").objectReferenceValue = starRow;
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAsPrefab(root, ClearPath);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// 星级行：卡片上部横向三等分，每格一枚固定 130×130 的星（PreserveAspect 保住星星比例），
        /// 图标走项目美术 star.png；点亮动画在 <see cref="LevelStarRow" /> 里。
        /// </summary>
        static LevelStarRow BuildStars(RectTransform card)
        {
            var row = NewRect("Stars", card);
            AnchorTopLeft(row, StarRowLeft, StarRowTop, StarRowWidth, StarRowHeight);

            var component = row.gameObject.AddComponent<LevelStarRow>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(StarSpritePath);
            if (sprite == null)
                Debug.LogWarning($"[BoardResultPanelBuilder] 未找到星级图标 {StarSpritePath}，星级行退化为纯色块");

            var count = LevelStarRules.MaxStars;
            var images = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var slot = NewRect("Star" + (i + 1), row);
                slot.anchorMin = slot.anchorMax = slot.pivot = new Vector2(0.5f, 0.5f);
                slot.sizeDelta = new Vector2(StarSize, StarSize);
                slot.anchoredPosition = new Vector2((i - (count - 1) / 2f) * StarGap, 0f);

                var image = slot.gameObject.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;
                if (sprite != null) image.sprite = sprite;
                images[i] = image;
            }

            var so = new SerializedObject(component);
            var starsProperty = so.FindProperty("stars");
            starsProperty.arraySize = count;
            for (int i = 0; i < count; i++)
                starsProperty.GetArrayElementAtIndex(i).objectReferenceValue = images[i];

            so.FindProperty("starSprite").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();

            component.Reset();      // 落盘前先归位到「全部熄灭」，避免预制体存着中间态
            return component;
        }

        // ---------- 通关失败 ----------

        static void BuildFailPanel()
        {
            var (root, _) = BuildShell<LevelFailPanel>("LevelFailPanel",
                FailCardTint, FailTitleColor, FailTextColor, withStars: false);

            SaveAsPrefab(root, FailPath);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// 两种结算面板共用的壳：全屏根 → Mask（暗紫半透，拦截射线）→ 深紫描边背板 → 羊皮纸内页
        /// （标题 + 古铜金分隔线 + 成绩行 + 两个牌匾按钮）。失败页没有星级与用时，成绩行上移。
        /// </summary>
        static (GameObject root, RectTransform card) BuildShell<T>(
            string name, Color cardTint, Color titleColor, Color textColor, bool withStars)
            where T : LevelResultPanel
        {
            var parchment = AssetDatabase.LoadAssetAtPath<Sprite>(ParchmentPath);
            if (parchment == null) Debug.LogError($"[BoardResultPanelBuilder] 缺少羊皮纸内页 {ParchmentPath}");
            var plaque = UiSpritePainter.EnsurePlaqueSprite();
            var frameSprite = UiSpritePainter.EnsurePanelSprite();

            var root = new GameObject(name, typeof(RectTransform));
            var script = root.AddComponent<T>();
            Stretch((RectTransform)root.transform);   // 根必须全屏拉伸，否则挂到 Popup 层后缩成 100×100 小块

            var mask = NewRect("Mask", root.transform);
            Stretch(mask);
            mask.gameObject.AddComponent<Image>().color = MaskColor;

            // 背板：比内页大一圈，露出深紫描边当卷轴的封皮
            var frame = NewRect("Frame", root.transform);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(FrameWidth, FrameHeight);
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.sprite = frameSprite;
            frameImage.type = Image.Type.Sliced;

            var card = NewRect("Card", frame);
            Stretch(card, CardInset);
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = parchment;
            cardImage.type = Image.Type.Sliced;
            cardImage.color = cardTint;

            var titleRow = NewRect("Title", card);
            AnchorTopLeft(titleRow, TitleLeft, TitleTop, CardWidth - TitleLeft * 2f, TitleHeight);
            var titleLabel = NewTmp(titleRow, string.Empty, TitleFontSize, titleColor);

            var divider = NewRect("Divider", card);
            AnchorTopLeft(divider, DividerLeft, DividerTop, DividerWidth, DividerHeight);
            divider.gameObject.AddComponent<Image>().color = GoldColor;

            // 胜负共用的成绩行：步数 + 已用方块（用时只有通关页有，见 BuildClearPanel）
            var stepsTop = withStars ? StepsTop : FailStepsTop;
            var piecesTop = withStars ? PiecesTop : FailPiecesTop;

            var stepsRow = NewRect("StepsLabel", card);
            AnchorTopLeft(stepsRow, RowLeft, stepsTop, RowWidth, RowHeight);
            var stepsLabel = NewTmp(stepsRow, string.Empty, RowFontSize, textColor);

            var piecesRow = NewRect("PiecesLabel", card);
            AnchorTopLeft(piecesRow, RowLeft, piecesTop, RowWidth, RowHeight);
            var piecesLabel = NewTmp(piecesRow, string.Empty, RowFontSize, textColor);

            var retryButton = BuildPlaqueButton(card, "RetryButton", plaque, ButtonLeftX, out var retryLabel);
            var backButton = BuildPlaqueButton(card, "BackToSelectButton", plaque, ButtonRightX, out var backLabel);

            var so = new SerializedObject(script);
            so.FindProperty("titleLabel").objectReferenceValue = titleLabel;
            so.FindProperty("stepsLabel").objectReferenceValue = stepsLabel;
            so.FindProperty("piecesLabel").objectReferenceValue = piecesLabel;
            so.FindProperty("retryButton").objectReferenceValue = retryButton;
            so.FindProperty("retryButtonLabel").objectReferenceValue = retryLabel;
            so.FindProperty("backToSelectButton").objectReferenceValue = backButton;
            so.FindProperty("backToSelectButtonLabel").objectReferenceValue = backLabel;
            // 结算要压在关卡 HUD 之上：显式挂 Popup 层（面板默认 Normal）
            so.FindProperty("layer").enumValueIndex = (int)UILayer.Popup;
            so.ApplyModifiedPropertiesWithoutUndo();

            return (root, card);
        }

        /// <summary>按钮：暗紫描金牌匾（9-slice）+ 浅金文字，左下角锚定按「距左 / 距底 / 宽 / 高」摆放。</summary>
        static Button BuildPlaqueButton(RectTransform parent, string name, Sprite plaque, float leftX, out TMP_Text label)
        {
            var rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = new Vector2(leftX, ButtonBottom);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = plaque;
            image.type = Image.Type.Sliced;

            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;

            var textRect = NewRect("Text", rect);
            Stretch(textRect);
            label = NewTmp(textRect, string.Empty, ButtonFontSize, ButtonLabelColor);
            return button;
        }

        // ---------- 结算文案（GameText 表） ----------

        /// <summary>往 zh-CN / en 两张表补结算相关条目（AddEntry 是「新增或更新」，可重复执行）。</summary>
        static void SyncResultTexts()
        {
            Write(TableZh, new[]
            {
                ("LevelClearTitle", "通关成功"),
                ("LevelClearTime", "用时：{0}"),
                ("LevelClearTimeNewRecord", "用时：{0}　★ 新纪录！"),
                ("LevelFailTitle", "通关失败"),
                ("LevelResultRetry", "重玩此关"),
                ("LevelResultBackToSelect", "回到选关界面"),
                ("LevelResultSteps", "步数：{0}"),
                ("LevelResultPieces", "已用方块：{0}/{1}"),
            });

            Write(TableEn, new[]
            {
                ("LevelClearTitle", "Level Clear"),
                ("LevelClearTime", "Time: {0}"),
                ("LevelClearTimeNewRecord", "Time: {0}  ★ New Record!"),
                ("LevelFailTitle", "Level Failed"),
                ("LevelResultRetry", "Retry Level"),
                ("LevelResultBackToSelect", "Back to Levels"),
                ("LevelResultSteps", "Steps: {0}"),
                ("LevelResultPieces", "Pieces Used: {0}/{1}"),
            });
        }

        static void Write(string tablePath, (string key, string value)[] entries)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);
            if (table == null)
            {
                Debug.LogError($"[BoardResultPanelBuilder] 未找到字符串表 {tablePath}，结算文案未写入");
                return;
            }

            foreach (var entry in entries)
            {
                table.AddEntry(entry.key, entry.value);
            }

            EditorUtility.SetDirty(table);

            // 键名（m_Key）存在 Shared Data 资产里：只标脏 StringTable 时新键不会真正落盘，
            // 域重载/重新进 Play 后运行时就报 "No translation found for '<key>'"
            var shared = table.SharedData;
            if (shared != null) EditorUtility.SetDirty(shared);
        }

        // ---------- 工具 ----------

        static TextMeshProUGUI NewTmp(RectTransform rect, string text, float fontSize, Color color)
        {
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            var font = ChineseFont;
            if (font != null) tmp.font = font;
            return tmp;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>父级内缩 <paramref name="inset" /> 像素后铺满（羊皮纸内页相对背板）。</summary>
        static void Stretch(RectTransform rt, float inset)
        {
            Stretch(rt);
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>左上锚定 + 固定尺寸：按「距左 / 距顶 / 宽 / 高」摆放，不随父级拉伸。</summary>
        static void AnchorTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
        }

        static void SaveAsPrefab(GameObject root, string path)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
    }
}
