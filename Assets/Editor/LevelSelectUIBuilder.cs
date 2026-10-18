using Game.View;
using Runestone.AesirModules;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace Game.Editor
{
    /// <summary>
    /// 选关 UI 预制体生成器：产物全部落在 Assets/Resources/UIPrefabs/（项目 UI 资产约定，
    /// 运行时由 UIModule 的 ResourcesUILoader 按 path 加载）。
    /// <para>
    /// 产物：LevelSelectPanel（Panel，根不带 Canvas）、LevelDetailPanel（**也是 Panel**，根不带 Canvas、
    /// 挂 Popup 层、全屏 Mask 子物体承接蒙版点击关闭）、LevelNode（节点模板）。
    /// 全项目统一面板形态：不使用 UIModule 的窗口 Open/Close 接口，弹窗也走 UIModule.Show。
    /// 重跑即覆盖产物；要改结构改这里，禁止在产物 prefab 上手加节点。
    /// </para>
    /// </summary>
    public static class LevelSelectUIBuilder
    {
        const string Dir = "Assets/Resources/UIPrefabs";
        const string PanelPath = Dir + "/LevelSelectPanel.prefab";
        const string DetailPath = Dir + "/LevelDetailPanel.prefab";
        const string NodePath = Dir + "/LevelNode.prefab";

        const string TableZh = "Assets/Localization/Tables/GameText_zh-CN.asset";
        const string TableEn = "Assets/Localization/Tables/GameText_en.asset";

        /// <summary>项目中文字体资产（TMPChineseText 扩展落地），不足时回退 TMP 默认字体。</summary>
        const string TmpChineseFontPath = "Assets/Codely/Fonts/NotoSansSC-Regular SDF.asset";

        /// <summary>选关界面美术素材目录（2026-10-18 迁移后位置，与 TestTF 旧路径无关）。</summary>
        const string UiImgDir = "Assets/Resources/UIPrefabs/UIimg";
        const string MapSpritePath = UiImgDir + "/map_1920x1080_fit.png";   // 选关大地图背景
        const string BackSpritePath = UiImgDir + "/back.png";                // 返回图标
        const string SettingsSpritePath = UiImgDir + "/setting-transparent.png"; // 设置图标

        // 关卡节点图标（2026-10-18 用户拍板：节点 = 水晶主题图 + 编号徽章叠加的图标按钮，目前 3 关）。
        // 索引 = 关卡号-1；新增关卡在此加一行路径，生成器会把整个数组回填进 LevelSelectPanel。
        static readonly string[] CrystalSpritePaths =
        {
            UiImgDir + "/crystal_blue.png",   // 第 1 关：冰晶王座
            UiImgDir + "/crystal_fire.png",   // 第 2 关：熔岩晶蛋
            UiImgDir + "/crystal_purple.png", // 第 3 关：星辉紫晶
        };
        static readonly string[] BadgeSpritePaths =
        {
            UiImgDir + "/badge_01.png",
            UiImgDir + "/badge_02.png",
            UiImgDir + "/badge_03.png",
        };

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
                        Debug.LogWarning($"[LevelSelectUIBuilder] 未找到中文字体资产 {TmpChineseFontPath}，TMP 文本将使用默认字体（中文可能缺字）");
                }

                return _chineseFont;
            }
        }

        [MenuItem("Tools/选关/重建选关 UI 预制体")]
        public static void RebuildAll()
        {
            SyncLevelTexts();            // 先补文案：面板文本节点留空，运行时从表取值
            BuildLevelNode();            // 先建节点模板：面板回填引用时它必须已存在
            BuildLevelSelectPanel();
            BuildLevelDetailPanel();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[选关] UI 预制体已重建：" + PanelPath + " / " + DetailPath + " / " + NodePath);
        }

        // ---------- 关卡详情文案（GameText 表） ----------

        /// <summary>
        /// 往 zh-CN / en 两张表补「最短用时 / 上次用时」条目（AddEntry 是「新增或更新」，可重复执行）。
        /// 只写本生成器负责的键——LevelName / LevelNoRecord 等既有条目不在表中被这里覆写。
        /// </summary>
        static void SyncLevelTexts()
        {
            WriteTexts(TableZh, new[]
            {
                ("LevelBestTime", "最短用时：{0}"),
                ("LevelLastTime", "上次用时：{0}"),
            });

            WriteTexts(TableEn, new[]
            {
                ("LevelBestTime", "Best Time: {0}"),
                ("LevelLastTime", "Last Time: {0}"),
            });
        }

        static void WriteTexts(string tablePath, (string key, string value)[] entries)
        {
            var table = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);
            if (table == null)
            {
                Debug.LogError($"[LevelSelectUIBuilder] 未找到字符串表 {tablePath}，关卡详情文案未写入");
                return;
            }

            foreach (var entry in entries) table.AddEntry(entry.key, entry.value);

            EditorUtility.SetDirty(table);

            // 键名（m_Key）存在 Shared Data 资产里：不一起标脏则新键不落盘，
            // 域重载/重新进 Play 后运行时报 "No translation found for '<key>'"（2026-10-18 实测踩坑）
            var shared = table.SharedData;
            if (shared != null) EditorUtility.SetDirty(shared);
        }

        // ---------- 选关大地图面板（Panel 形态：根不带 Canvas） ----------

        static void BuildLevelSelectPanel()
        {
            var root = new GameObject("LevelSelectPanel", typeof(RectTransform));
            var script = root.AddComponent<LevelSelectPanel>();
            Stretch((RectTransform)root.transform);   // 根必须全屏拉伸，否则挂到 NormalLayer 后缩成 100×100 小块

            var bg = NewRect("Background", root.transform);
            Stretch(bg);
            var bgImage = bg.gameObject.AddComponent<Image>();
            var bgSprite = LoadSprite(MapSpritePath);
            if (bgSprite != null)
            {
                bgImage.sprite = bgSprite;
                bgImage.preserveAspect = false;  // 图已按 alpha 边界裁掉透明边（内容满幅），Stretch 全屏直接铺满窗口；裁后图比例贴近 16:9，拉伸变形 <3% 无感
                bgImage.color = Color.white;     // 有美术时保持原色，不再叠深色
            }
            else
            {
                bgImage.color = new Color(0.12f, 0.14f, 0.18f);
            }

            var title = NewRect("Title", root.transform);
            Stretch(title, new Vector2(0f, 0.86f), new Vector2(1f, 1f));
            var titleText = NewTmp(title, "选择关卡", 64f);

            var nodesRoot = NewRect("NodesRoot", root.transform);
            Stretch(nodesRoot, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.85f));

            // 左上：返回按钮——120×120 图标位。图标是方形透明图，配方形按钮才不被压扁；
            // 纯图标不放文字：LevelSelectPanel.ApplyLocalization 会在 label 非空时填中文，
            // 文字压在图标上会糊，故这里不建 Text 子节点、也不回填 backButtonLabel。
            var back = NewRect("BackButton", root.transform);
            back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
            back.pivot = new Vector2(0.5f, 0.5f);
            back.sizeDelta = new Vector2(120f, 120f);
            back.anchoredPosition = new Vector2(96f, -64f);
            ApplyIcon(back.gameObject, BackSpritePath, new Color(0.35f, 0.38f, 0.45f));
            var backBtn = back.gameObject.AddComponent<Button>();

            // 右上：设置按钮——与返回按钮对称的图标位
            var settings = NewRect("SettingsButton", root.transform);
            settings.anchorMin = settings.anchorMax = new Vector2(1f, 1f);
            settings.pivot = new Vector2(0.5f, 0.5f);
            settings.sizeDelta = new Vector2(120f, 120f);
            settings.anchoredPosition = new Vector2(-96f, -64f);
            ApplyIcon(settings.gameObject, SettingsSpritePath, new Color(0.35f, 0.38f, 0.45f));
            var settingsBtn = settings.gameObject.AddComponent<Button>();

            var so = new SerializedObject(script);
            so.FindProperty("nodesRoot").objectReferenceValue = nodesRoot;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("levelNodePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(NodePath);
            so.FindProperty("titleLabel").objectReferenceValue = titleText;
            FillSpriteArray(so, "crystalSprites", CrystalSpritePaths);
            FillSpriteArray(so, "badgeSprites", BadgeSpritePaths);
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAsPrefab(root, PanelPath);
            Object.DestroyImmediate(root);
        }

        // ---------- 关卡详情弹窗（Panel 形态：根不带 Canvas，挂 Popup 层，Mask 子物体承接蒙版点击） ----------

        static void BuildLevelDetailPanel()
        {
            // 面板形态：根只有 RectTransform（无 Canvas / CanvasScaler / GraphicRaycaster——
            // 那些由 UIRoot 的层 Canvas 提供）；根必须全屏拉伸，否则挂层后缩成 100×100 小块
            var root = new GameObject("LevelDetailPanel", typeof(RectTransform));
            var script = root.AddComponent<LevelDetailPanel>();
            Stretch((RectTransform)root.transform);

            var mask = NewRect("Mask", root.transform);
            Stretch(mask);
            mask.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            var maskButton = mask.gameObject.AddComponent<Button>();
            maskButton.transition = Selectable.Transition.None;

            var content = NewRect("Content", root.transform);
            Anchor(content, new Vector2(0.30f, 0.35f), new Vector2(0.70f, 0.65f));
            content.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.19f, 0.25f);

            var title = NewRect("Title", content);
            Stretch(title, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.95f));
            var titleText = NewTmp(title, string.Empty, 48f);

            var preview = NewRect("PreviewImage", content);
            Stretch(preview, new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.72f));
            var previewImage = preview.gameObject.AddComponent<Image>();
            previewImage.color = new Color(0.22f, 0.26f, 0.32f);
            previewImage.raycastTarget = false;

            // 最短用时：通关后展示该关历史最好成绩（未通关时这里显示「暂无记录」）
            var record = NewRect("RecordLabel", content);
            Stretch(record, new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.50f));
            var recordText = NewTmp(record, string.Empty, 36f);

            // 上次用时：与最短用时对照（未通关时留空）
            var last = NewRect("LastLabel", content);
            Stretch(last, new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.38f));
            var lastText = NewTmp(last, string.Empty, 30f);
            lastText.color = new Color(0.78f, 0.82f, 0.90f);

            var enter = NewRect("EnterButton", content);
            Anchor(enter, new Vector2(0.15f, 0.08f), new Vector2(0.45f, 0.24f));
            enter.gameObject.AddComponent<Image>().color = new Color(0.30f, 0.55f, 0.75f);
            var enterBtn = enter.gameObject.AddComponent<Button>();
            var enterText = NewRect("Text", enter);
            Stretch(enterText);
            var enterTmp = NewTmp(enterText, "进入", 32f);

            var cancel = NewRect("CancelButton", content);
            Anchor(cancel, new Vector2(0.55f, 0.08f), new Vector2(0.85f, 0.24f));
            cancel.gameObject.AddComponent<Image>().color = new Color(0.45f, 0.30f, 0.30f);
            var cancelBtn = cancel.gameObject.AddComponent<Button>();
            var cancelText = NewRect("Text", cancel);
            Stretch(cancelText);
            var cancelTmp = NewTmp(cancelText, "取消", 32f);

            var so = new SerializedObject(script);
            so.FindProperty("titleLabel").objectReferenceValue = titleText;
            so.FindProperty("previewImage").objectReferenceValue = previewImage;
            so.FindProperty("recordLabel").objectReferenceValue = recordText;
            so.FindProperty("lastLabel").objectReferenceValue = lastText;
            so.FindProperty("enterButton").objectReferenceValue = enterBtn;
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
            so.FindProperty("enterButtonLabel").objectReferenceValue = enterTmp;
            so.FindProperty("cancelButtonLabel").objectReferenceValue = cancelTmp;
            // 弹窗要压在选关面板之上：显式挂 Popup 层（面板默认 Normal）
            so.FindProperty("layer").enumValueIndex = (int)UILayer.Popup;
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAsPrefab(root, DetailPath);
            Object.DestroyImmediate(root);
        }

        // ---------- 关卡节点模板（图标按钮：水晶主题图 + 编号徽章叠加，2026-10-18 用户拍板形态） ----------

        static void BuildLevelNode()
        {
            var root = new GameObject("LevelNode", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(180f, 180f);

            // 根只有 Button（无 Image）：点击热区由 Crystal/Badge 的 Image 承接，事件冒泡到父级 Button。
            // 节点不再带 Name/Stars 文字——关卡号在徽章图上，解锁状态由运行时染灰表达。
            root.AddComponent<Button>();

            // 主图：关卡主题水晶。素材是 532×785 一类的竖长图，Stretch 满节点 + preserveAspect 自动保比例居中
            var crystal = NewRect("Crystal", root.transform);
            Stretch(crystal);
            var crystalImage = crystal.gameObject.AddComponent<Image>();
            crystalImage.color = Color.white;   // 有专属美术走原色（项目惯例：不叠底色染色）
            crystalImage.preserveAspect = true;

            // 编号徽章：叠在水晶之上（sibling 靠后 = 渲染层级在上，用户拍板「层级在上一点」）
            var badge = NewRect("Badge", root.transform);
            badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0.5f, 0.5f);
            badge.sizeDelta = new Vector2(100f, 100f);
            badge.anchoredPosition = Vector2.zero;
            var badgeImage = badge.gameObject.AddComponent<Image>();
            badgeImage.color = Color.white;
            badgeImage.preserveAspect = true;

            SaveAsPrefab(root, NodePath);
            Object.DestroyImmediate(root);
        }

        // ---------- 工具 ----------

        /// <summary>按路径取 Sprite；取不到打警告并返回 null，调用方负责回退成纯色占位。</summary>
        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning($"[LevelSelectUIBuilder] 未找到素材 {path}，该处回退为纯色占位");
            return sprite;
        }

        /// <summary>回填 Sprite[] 序列化字段：数组长度 = 路径表长度，缺素材留空（运行时 ApplyNodeIcon 会隐藏空图）。</summary>
        static void FillSpriteArray(SerializedObject so, string property, string[] paths)
        {
            var arr = so.FindProperty(property);
            arr.arraySize = paths.Length;
            for (var i = 0; i < paths.Length; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite(paths[i]);
        }

        /// <summary>给按钮挂图标：有图则用原色 + 保持比例，无图则退回纯色块。</summary>
        static void ApplyIcon(GameObject go, string path, Color fallbackColor)
        {
            var image = go.AddComponent<Image>();
            var sprite = LoadSprite(path);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else
            {
                image.color = fallbackColor;
            }
        }

        static TextMeshProUGUI NewTmp(RectTransform rect, string text, float fontSize)
        {
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
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

        static void Stretch(RectTransform rt) => Anchor(rt, Vector2.zero, Vector2.one);

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max) => Anchor(rt, min, max);

        static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SaveAsPrefab(GameObject root, string path)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
    }
}
