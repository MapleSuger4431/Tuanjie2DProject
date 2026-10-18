using System.IO;
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
    /// 关卡内「成就 / 消息小窗」生成器：重建 Assets/Resources/UIPrefabs/LevelTipPanel.prefab。
    /// <para>
    /// 皮肤 = 参考文档「旧魔导书 Grimoire」那套：做旧羊皮纸 + 褪色紫 + 暗金描边，
    /// 卡片钉在**屏幕右下角**（角锚定，520×104，距右 28 / 距底 8）——这个高度让它整块落在
    /// 「出发」按钮（底边在屏幕 y≈166）下方，5 秒遮挡期间也不压住任何可点元素。
    /// </para>
    /// <para>
    /// 面板是 **Panel 形态**（根只有 RectTransform，无 Canvas），挂 **Top 层** 压在结算面板之上；
    /// 全部 Graphic 的 raycastTarget=false——小窗不可点击，不能挡住棋盘放置。
    /// 文本节点一律留空：文案真源在 GameText 表，运行时由 <see cref="LevelTipPanel" /> 填。
    /// 重跑即覆盖产物；要改结构改这里，禁止手改预制体。
    /// </para>
    /// </summary>
    public static class LevelTipPanelBuilder
    {
        const string PrefabPath = "Assets/Resources/UIPrefabs/LevelTipPanel.prefab";

        const string ParchmentPath = "Assets/Art/Board/UI/Tip_Parchment.png";
        const string BadgePath = "Assets/Art/Board/UI/Tip_Badge.png";

        const string TableZh = "Assets/Localization/Tables/GameText_zh-CN.asset";
        const string TableEn = "Assets/Localization/Tables/GameText_en.asset";

        /// <summary>项目中文字体资产（TMPChineseText 扩展落地），不足时回退 TMP 默认字体。</summary>
        const string TmpChineseFontPath = "Assets/Codely/Fonts/NotoSansSC-Regular SDF.asset";

        // ---- 布局（1920×1080 设计分辨率，右下角角锚定）----
        const float CardWidth = 520f;
        const float CardHeight = 104f;
        const float CardMarginRight = 28f;
        const float CardMarginBottom = 8f;

        const float IconSize = 88f;
        const float IconMarginLeft = 16f;

        const float TextLeft = 120f;
        const float TextRight = 20f;
        const float TitleTop = 14f;
        const float TitleHeight = 36f;
        const float MessageTop = 52f;
        const float MessageHeight = 34f;

        // ---- 配色（纸面浅色 → 文字走深紫，紫底白字那套在这里不成立）----
        static readonly Color TitleColor = new Color(0.165f, 0.078f, 0.212f, 1f);
        static readonly Color MessageColor = new Color(0.294f, 0.200f, 0.322f, 1f);

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
                        Debug.LogWarning($"[LevelTipPanelBuilder] 未找到中文字体资产 {TmpChineseFontPath}，TMP 文本将使用默认字体（中文可能缺字）");
                }

                return _chineseFont;
            }
        }

        [MenuItem("Tools/关卡/重建成就消息小窗")]
        public static void Rebuild()
        {
            SyncTipTexts();     // 先补文案：文本节点留空，运行时从表取值
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[关卡] 成就消息小窗已重建：" + PrefabPath);
        }

        static void BuildPrefab()
        {
            var parchment = AssetDatabase.LoadAssetAtPath<Sprite>(ParchmentPath);
            var badge = AssetDatabase.LoadAssetAtPath<Sprite>(BadgePath);
            if (parchment == null) Debug.LogError($"[LevelTipPanelBuilder] 缺少便签底图 {ParchmentPath}");
            if (badge == null) Debug.LogError($"[LevelTipPanelBuilder] 缺少徽章图标 {BadgePath}");

            var root = new GameObject("LevelTipPanel", typeof(RectTransform));
            var panel = root.AddComponent<LevelTipPanel>();
            Stretch((RectTransform)root.transform);   // 根必须全屏拉伸，否则挂到层画布后缩成 100×100 小块

            var card = NewRect("TipCard", root.transform);
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(1f, 0f);
            card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.anchoredPosition = new Vector2(-CardMarginRight, CardMarginBottom);

            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.sprite = parchment;
            cardImage.type = Image.Type.Sliced;
            cardImage.raycastTarget = false;

            var group = card.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var icon = NewRect("Icon", card);
            icon.anchorMin = icon.anchorMax = new Vector2(0f, 0.5f);
            icon.pivot = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(IconSize, IconSize);
            icon.anchoredPosition = new Vector2(IconMarginLeft, 0f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = badge;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var titleRect = NewRect("TitleText", card);
            AnchorTopLeft(titleRect, TextLeft, TitleTop, CardWidth - TextLeft - TextRight, TitleHeight);
            var title = NewTmp(titleRect, 30f, TitleColor);

            var messageRect = NewRect("MessageText", card);
            AnchorTopLeft(messageRect, TextLeft, MessageTop, CardWidth - TextLeft - TextRight, MessageHeight);
            var message = NewTmp(messageRect, 24f, MessageColor);

            var so = new SerializedObject(panel);
            so.FindProperty("tipCard").objectReferenceValue = card;
            so.FindProperty("cardGroup").objectReferenceValue = group;
            so.FindProperty("icon").objectReferenceValue = iconImage;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("messageText").objectReferenceValue = message;
            so.FindProperty("iconSprite").objectReferenceValue = badge;
            // 压在结算面板（Popup）之上：通关同时解锁的成就也要看得见
            so.FindProperty("layer").enumValueIndex = (int)UILayer.Top;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);

            Debug.Log($"[LevelTipPanelBuilder] 字段回填自检：card={card != null}, group={group != null}, " +
                      $"icon={iconImage != null}, title={title != null}, message={message != null}, " +
                      $"parchment={parchment != null}, badge={badge != null}");
        }

        // ---------- 小窗文案（GameText 表） ----------

        static void SyncTipTexts()
        {
            Write(TableZh, new[]
            {
                ("TipAchievement", "成就解锁"),
                ("TipNewRecord", "刷新最短纪录：{0}"),
                ("TipElementSwitched", "元素反转"),
                ("TipElementSwitchedBody", "女巫转为 {0} 属性"),
            });

            Write(TableEn, new[]
            {
                ("TipAchievement", "Achievement Unlocked"),
                ("TipNewRecord", "New Best Time: {0}"),
                ("TipElementSwitched", "Element Switched"),
                ("TipElementSwitchedBody", "Witch is now {0}"),
            });
        }

        static void Write(string tablePath, (string key, string value)[] entries)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);
            if (table == null)
            {
                Debug.LogError($"[LevelTipPanelBuilder] 未找到字符串表 {tablePath}，小窗文案未写入");
                return;
            }

            foreach (var entry in entries) table.AddEntry(entry.key, entry.value);

            EditorUtility.SetDirty(table);

            // 键名（m_Key）存在 Shared Data 资产里：只标脏 StringTable 时新键不落盘，
            // 域重载/重进 Play 后运行时就报 "No translation found for '<key>'"
            var shared = table.SharedData;
            if (shared != null) EditorUtility.SetDirty(shared);
        }

        // ---------- 工具 ----------

        static TextMeshProUGUI NewTmp(RectTransform rect, float fontSize, Color color)
        {
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.color = color;
            tmp.raycastTarget = false;
            var font = ChineseFont;
            if (font != null) tmp.font = font;
            return tmp;
        }

        /// <summary>左上锚定 + 固定高：按「距左 / 距顶 / 宽 / 高」摆放，不随父级拉伸。</summary>
        static void AnchorTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(left, -top);
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
    }
}
