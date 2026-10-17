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
    /// 产物：LevelClearPanel（通关成功，带用时）、LevelFailPanel（通关失败），
    /// 两者都是 **Panel 形态**：根只有 RectTransform（无 Canvas / CanvasScaler / GraphicRaycaster，
    /// 那些由 UIRoot 的层 Canvas 提供），挂 Popup 层，全屏 Mask 拦截射线。
    /// 重跑即覆盖产物；要改结构改这里，禁止在产物 prefab 上手加节点。
    /// </para>
    /// <para>
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
            var root = BuildShell<LevelClearPanel>("LevelClearPanel", new Color(0.16f, 0.24f, 0.21f),
                new Color(0.45f, 0.88f, 0.58f));

            // 成功面板独有：本局用时（空文本，运行时由 LevelClearPanel.ApplyResultText 填）
            var content = root.transform.Find("Content");
            var timeRow = NewRect("TimeLabel", content);
            Stretch(timeRow, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.45f));
            var timeLabel = NewTmp(timeRow, string.Empty, 44f);
            timeLabel.color = new Color(0.88f, 0.95f, 0.90f);

            var so = new SerializedObject(root.GetComponent<LevelClearPanel>());
            so.FindProperty("timeLabel").objectReferenceValue = timeLabel;
            so.ApplyModifiedPropertiesWithoutUndo();

            SaveAsPrefab(root, ClearPath);
            Object.DestroyImmediate(root);
        }

        // ---------- 通关失败 ----------

        static void BuildFailPanel()
        {
            var root = BuildShell<LevelFailPanel>("LevelFailPanel", new Color(0.24f, 0.17f, 0.18f),
                new Color(0.92f, 0.48f, 0.48f));

            SaveAsPrefab(root, FailPath);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// 两种结算面板共用的外壳：全屏根 → Mask（半透明黑，拦截射线）→ Content（标题 + 两个出口按钮）。
        /// 失败面板没有成绩行，标题与按钮之间留出更大的空白。
        /// </summary>
        static GameObject BuildShell<T>(string name, Color contentColor, Color titleColor) where T : LevelResultPanel
        {
            var root = new GameObject(name, typeof(RectTransform));
            var script = root.AddComponent<T>();
            Stretch((RectTransform)root.transform);   // 根必须全屏拉伸，否则挂到 Popup 层后缩成 100×100 小块

            var mask = NewRect("Mask", root.transform);
            Stretch(mask);
            mask.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

            var content = NewRect("Content", root.transform);
            Anchor(content, new Vector2(0.31f, 0.29f), new Vector2(0.69f, 0.71f));
            content.gameObject.AddComponent<Image>().color = contentColor;

            var titleRow = NewRect("Title", content);
            Stretch(titleRow, new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.93f));
            var titleLabel = NewTmp(titleRow, string.Empty, 56f);
            titleLabel.color = titleColor;

            // 胜负共用的成绩行：步数 + 已用方块（用时只有通关页有，见 BuildClearPanel）
            var stepsRow = NewRect("StepsLabel", content);
            Stretch(stepsRow, new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.67f));
            var stepsLabel = NewTmp(stepsRow, string.Empty, 38f);

            var piecesRow = NewRect("PiecesLabel", content);
            Stretch(piecesRow, new Vector2(0.05f, 0.45f), new Vector2(0.95f, 0.56f));
            var piecesLabel = NewTmp(piecesRow, string.Empty, 38f);

            var retry = NewRect("RetryButton", content);
            Anchor(retry, new Vector2(0.08f, 0.10f), new Vector2(0.48f, 0.32f));
            retry.gameObject.AddComponent<Image>().color = new Color(0.28f, 0.52f, 0.76f);
            var retryButton = retry.gameObject.AddComponent<Button>();
            var retryText = NewRect("Text", retry);
            Stretch(retryText);
            var retryLabel = NewTmp(retryText, string.Empty, 32f);

            var back = NewRect("BackToSelectButton", content);
            Anchor(back, new Vector2(0.52f, 0.10f), new Vector2(0.92f, 0.32f));
            back.gameObject.AddComponent<Image>().color = new Color(0.42f, 0.44f, 0.52f);
            var backButton = back.gameObject.AddComponent<Button>();
            var backText = NewRect("Text", back);
            Stretch(backText);
            var backLabel = NewTmp(backText, string.Empty, 32f);

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

            return root;
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
