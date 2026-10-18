using System.IO;
using Game.View;
using Runestone.AesirModules;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 成就图鉴面板生成器：重建 Assets/Resources/UIPrefabs/AchievementPanel.prefab。
    /// <para>
    /// 皮肤沿用关卡 HUD 的深紫 fantasy（<c>Panel_DarkPurple</c> 九宫格）+ 图鉴自己的品红强调色，
    /// 与参考文档「暗紫秘仪 / 旧魔导书」一致；内容卡 1000×720 居中，下方一枚关闭按钮，
    /// 遮罩点击也关闭。成就行由面板按配置条数**克隆模板**生成（模板停在停用例，不参与布局）。
    /// 文本节点一律留空：文案真源在 GameText 表。
    /// 重跑即覆盖产物；要改结构改这里，禁止手改预制体。
    /// </para>
    /// </summary>
    public static class AchievementPanelBuilder
    {
        const string PrefabPath = "Assets/Resources/UIPrefabs/AchievementPanel.prefab";
        const string PanelSpritePath = "Assets/Art/Board/UI/Panel_DarkPurple.png";
        const string CloseSpritePath = "Assets/Arts/UI组件/按钮底图_退出_无字.png";
        const string TmpChineseFontPath = "Assets/Codely/Fonts/NotoSansSC-Regular SDF.asset";

        // ---- 布局（1920×1080）----
        const float ContentWidth = 1000f;
        const float ContentHeight = 720f;
        const float HeaderHeight = 104f;
        const float ListPadding = 24f;
        const float RowHeight = 120f;
        const float RowSpacing = 12f;
        const float CloseWidth = 220f;
        const float CloseHeight = 68f;
        const float CloseBottom = 24f;

        static readonly Color TitleColor = new Color(1f, 0.42f, 0.85f, 1f);
        static readonly Color TextColor = new Color(0.79f, 0.77f, 0.90f, 1f);
        static readonly Color DimTextColor = new Color(0.55f, 0.53f, 0.68f, 1f);

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
                        Debug.LogWarning($"[成就] 未找到中文字体资产 {TmpChineseFontPath}，TMP 文本将使用默认字体（中文可能缺字）");
                }

                return _chineseFont;
            }
        }

        [MenuItem("Tools/成就/重建成就图鉴面板")]
        public static void Rebuild()
        {
            AchievementLibraryBuilder.SyncTexts();   // 先补文案：文本节点留空，运行时从表取值
            BuildPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[成就] 成就图鉴面板已重建：" + PrefabPath);
        }

        static void BuildPrefab()
        {
            var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSpritePath);
            var closeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CloseSpritePath);
            if (panelSprite == null) Debug.LogError($"[成就] 缺少面板底图 {PanelSpritePath}");

            var root = new GameObject("AchievementPanel", typeof(RectTransform));
            var panel = root.AddComponent<AchievementPanel>();
            Stretch((RectTransform)root.transform);

            var mask = NewRect("Mask", root.transform);
            Stretch(mask);
            var maskImage = mask.gameObject.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.62f);
            var maskButton = mask.gameObject.AddComponent<Button>();

            var content = NewRect("Content", root.transform);
            Center(content, ContentWidth, ContentHeight);
            var contentImage = content.gameObject.AddComponent<Image>();
            contentImage.sprite = panelSprite;
            contentImage.type = Image.Type.Sliced;

            var title = NewRect("TitleText", content);
            TopStretch(title, HeaderHeight, 0f);
            var titleText = NewTmp(title, 46f, TitleColor, TextAlignmentOptions.Center);

            var completed = NewRect("CompletedText", content);
            TopRight(completed, 280f, 34f, 28f, HeaderHeight - 12f);
            var completedText = NewTmp(completed, 28f, TextColor, TextAlignmentOptions.MidlineRight);

            var listRoot = NewRect("ListRoot", content);
            SetAnchors(listRoot, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(ListPadding, ListPadding + CloseHeight + 16f),
                new Vector2(-ListPadding, -(HeaderHeight + 8f)));
            var layout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = RowSpacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var rowTemplate = BuildRowTemplate(listRoot);
            rowTemplate.SetActive(false);   // 模板不显示：运行时按需克隆

            var close = NewRect("CloseButton", content);
            BottomCenter(close, CloseWidth, CloseHeight, CloseBottom);
            var closeImage = close.gameObject.AddComponent<Image>();
            closeImage.sprite = closeSprite;
            closeImage.type = Image.Type.Sliced;
            var closeButton = close.gameObject.AddComponent<Button>();
            var closeTextRect = NewRect("Text", close);
            Stretch(closeTextRect);
            var closeLabel = NewTmp(closeTextRect, 30f, new Color(0.13f, 0.12f, 0.18f, 1f),
                TextAlignmentOptions.Center);

            var so = new SerializedObject(panel);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("completedText").objectReferenceValue = completedText;
            so.FindProperty("listRoot").objectReferenceValue = listRoot;
            so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("closeButtonLabel").objectReferenceValue = closeLabel;
            so.FindProperty("maskButton").objectReferenceValue = maskButton;
            so.FindProperty("layer").enumValueIndex = (int)UILayer.Popup;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        /// <summary>一行成就：左侧图标 + 右侧名称 / 描述 / 状态三行文本。</summary>
        static GameObject BuildRowTemplate(RectTransform parent)
        {
            var row = NewRect("Row", parent);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.minHeight = RowHeight;
            element.preferredHeight = RowHeight;

            var rowImage = row.gameObject.AddComponent<Image>();
            rowImage.color = new Color(0.157f, 0.102f, 0.255f, 0.55f);

            var icon = NewRect("Icon", row);
            LeftCenter(icon, 88f, 88f, 16f);
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var name = NewRect("NameText", row);
            TopStretchOffset(name, 120f, 36f, 14f);
            NewTmp(name, 32f, Color.white, TextAlignmentOptions.MidlineLeft);

            var desc = NewRect("DescText", row);
            TopStretchOffset(desc, 120f, 30f, 52f);
            NewTmp(desc, 24f, TextColor, TextAlignmentOptions.MidlineLeft);

            var status = NewRect("StatusText", row);
            TopStretchOffset(status, 120f, 28f, 86f);
            NewTmp(status, 22f, DimTextColor, TextAlignmentOptions.MidlineLeft);

            return row.gameObject;
        }

        // ---------- 工具 ----------

        static TextMeshProUGUI NewTmp(RectTransform rect, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = string.Empty;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
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

        static void Stretch(RectTransform rt) => SetAnchors(rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        static void Center(RectTransform rt, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>顶部整宽条带：高度 height，距顶 top。</summary>
        static void TopStretch(RectTransform rt, float height, float top)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);
            rt.anchoredPosition = new Vector2(0f, -top);
        }

        /// <summary>右上固定框：宽 width、高 height，距右/距顶各 28。</summary>
        static void TopRight(RectTransform rt, float width, float height, float right, float top)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-right, -top);
        }

        /// <summary>底部居中固定框。</summary>
        static void BottomCenter(RectTransform rt, float width, float height, float bottom)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, bottom);
        }

        /// <summary>左侧垂直居中固定框。</summary>
        static void LeftCenter(RectTransform rt, float width, float height, float left)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(left, 0f);
        }

        /// <summary>顶部锚定 + 左侧留白：名称 / 描述 / 状态三行。</summary>
        static void TopStretchOffset(RectTransform rt, float left, float height, float top)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-(left + 24f), height);
            rt.anchoredPosition = new Vector2(left, -top);
        }

        static void SetAnchors(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
