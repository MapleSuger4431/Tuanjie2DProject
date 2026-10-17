using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// 选关 UI 预制体生成器（框架工作流：禁止手写 prefab YAML，一切资产经生成器）。
/// 产物：Assets/TestTF/UI/Resources/GameUI/ 下的 LevelSelectPanel（Panel，根不带 Canvas）
/// 与 LevelDetailWindow（Window，根带 Canvas，Mask 按名字约定 + closeOnMaskClick=true）。
/// 重跑即覆盖产物；要改结构改这里，禁止在产物 prefab 上手加节点。
/// </summary>
public static class UiPrefabBuilder
{
    private const string PanelPath = "Assets/TestTF/UI/Resources/GameUI/LevelSelectPanel.prefab";
    private const string WindowPath = "Assets/TestTF/UI/Resources/GameUI/LevelDetailWindow.prefab";
    private const string NodePath = "Assets/TestTF/UI/Resources/GameUI/LevelNode.prefab";

    [MenuItem("TestTF/重建选关 UI 预制体")]
    public static void RebuildAll()
    {
        BuildLevelNode();            // 先建节点模板：面板回填引用时它必须已存在
        BuildLevelSelectPanel();
        BuildLevelDetailWindow();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TestTF] 选关 UI 预制体已重建：" + PanelPath + " / " + WindowPath + " / " + NodePath);
    }

    // ---------- 选关大地图面板（Panel 形态：根不带 Canvas） ----------

    private static void BuildLevelSelectPanel()
    {
        var root = new GameObject("LevelSelectPanel", typeof(RectTransform));
        var script = root.AddComponent<LevelSelectPanel>();
        Stretch((RectTransform)root.transform);   // 根必须全屏拉伸，否则挂到 NormalLayer 后缩成 100×100 小块

        // 背景层（占位深色，美术地图后续替换）
        var bg = NewRect("Background", root.transform);
        Stretch(bg);
        bg.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f);

        // 标题
        var title = NewRect("Title", root.transform);
        Stretch(title, new Vector2(0f, 0.86f), new Vector2(1f, 1f));
        var titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "EDGE GRID";
        titleText.fontSize = 64f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // 节点容器（节点在运行时由 LevelSelectPanel 按关卡表生成）
        var nodesRoot = NewRect("NodesRoot", root.transform);
        Stretch(nodesRoot, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.85f));

        // 左上：返回主菜单按钮（圆形图标位，AI 生成的返回图标到位后自动绑 sprite）
        var back = NewRect("BackButton", root.transform);
        back.anchorMin = back.anchorMax = new Vector2(0f, 1f);
        back.pivot = new Vector2(0.5f, 0.5f);
        back.sizeDelta = new Vector2(120f, 120f);
        back.anchoredPosition = new Vector2(96f, -64f);
        var backImg = back.gameObject.AddComponent<Image>();
        var backSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TestTF/UI/Resources/BackIcon.png");
        if (backSprite != null) { backImg.sprite = backSprite; backImg.preserveAspect = true; }
        else backImg.color = new Color(0.24f, 0.46f, 0.62f);
        var backBtn = back.gameObject.AddComponent<Button>();
        var backText = NewRect("Text", back);
        Stretch(backText);
        var backTmp = backText.gameObject.AddComponent<TextMeshProUGUI>();
        backTmp.text = backSprite != null ? "" : "BACK";
        backTmp.fontSize = 28f;
        backTmp.alignment = TextAlignmentOptions.Center;
        backTmp.color = Color.white;
        backTmp.raycastTarget = false;

        // 右上：设置按钮（美术图标 SettingsIcon，圆形贴图）
        var settings = NewRect("SettingsButton", root.transform);
        settings.anchorMin = settings.anchorMax = new Vector2(1f, 1f);
        settings.pivot = new Vector2(0.5f, 0.5f);
        settings.sizeDelta = new Vector2(120f, 120f);
        settings.anchoredPosition = new Vector2(-96f, -64f);
        var settingsImg = settings.gameObject.AddComponent<Image>();
        var settingsSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TestTF/UI/Resources/SettingsIcon.png");
        if (settingsSprite != null) { settingsImg.sprite = settingsSprite; settingsImg.preserveAspect = true; }
        else settingsImg.color = new Color(0.35f, 0.38f, 0.45f);
        var settingsBtn = settings.gameObject.AddComponent<Button>();
        var settingsText = NewRect("Text", settings);
        Stretch(settingsText);
        var settingsTmp = settingsText.gameObject.AddComponent<TextMeshProUGUI>();
        settingsTmp.text = settingsSprite != null ? "" : "SETTINGS";
        settingsTmp.fontSize = 28f;
        settingsTmp.alignment = TextAlignmentOptions.Center;
        settingsTmp.color = Color.white;
        settingsTmp.raycastTarget = false;

        // 回填 [SerializeField]（私有字段必须走 SerializedObject）
        var so = new SerializedObject(script);
        so.FindProperty("nodesRoot").objectReferenceValue = nodesRoot;
        so.FindProperty("backButton").objectReferenceValue = backBtn;
        so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
        so.FindProperty("levelNodePrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(NodePath);
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAsPrefab(root, PanelPath);
        Object.DestroyImmediate(root);
    }

    // ---------- 关卡详情弹窗（Window 形态：根自带 Canvas，Mask 按名约定） ----------

    private static void BuildLevelDetailWindow()
    {
        var root = new GameObject("LevelDetailWindow",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
        var script = root.AddComponent<LevelDetailWindow>();

        // Mask 子节点（名字必须为 Mask，框架按名查找；蒙版点击关闭走 closeOnMaskClick）
        var mask = NewRect("Mask", root.transform);
        Stretch(mask);
        mask.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        var maskButton = mask.gameObject.AddComponent<Button>();
        maskButton.transition = Selectable.Transition.None;

        // Content 内容区
        var content = NewRect("Content", root.transform);
        Anchor(content, new Vector2(0.30f, 0.35f), new Vector2(0.70f, 0.65f));
        content.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.19f, 0.25f);

        var title = NewRect("Title", content);
        Stretch(title, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.95f));
        var titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.fontSize = 48f;
        titleText.alignment = TextAlignmentOptions.Center;

        // 关卡预览图（Title 与 RecordLabel 之间；占位纯色，美术图到位后替换）
        var preview = NewRect("PreviewImage", content);
        Stretch(preview, new Vector2(0.10f, 0.50f), new Vector2(0.90f, 0.72f));
        var previewImage = preview.gameObject.AddComponent<Image>();
        previewImage.color = new Color(0.22f, 0.26f, 0.32f);
        previewImage.raycastTarget = false;

        var record = NewRect("RecordLabel", content);
        Stretch(record, new Vector2(0.05f, 0.34f), new Vector2(0.95f, 0.48f));
        var recordText = record.gameObject.AddComponent<TextMeshProUGUI>();
        recordText.fontSize = 36f;
        recordText.alignment = TextAlignmentOptions.Center;

        var enter = NewRect("EnterButton", content);
        Anchor(enter, new Vector2(0.15f, 0.10f), new Vector2(0.45f, 0.30f));
        enter.gameObject.AddComponent<Image>().color = new Color(0.30f, 0.55f, 0.75f);
        var enterBtn = enter.gameObject.AddComponent<Button>();
        var enterText = NewRect("Text", enter);
        Stretch(enterText);
        var enterTmp = enterText.gameObject.AddComponent<TextMeshProUGUI>();
        enterTmp.text = "ENTER";
        enterTmp.fontSize = 32f;
        enterTmp.alignment = TextAlignmentOptions.Center;
        enterTmp.color = Color.white;
        enterTmp.raycastTarget = false;

        var cancel = NewRect("CancelButton", content);
        Anchor(cancel, new Vector2(0.55f, 0.10f), new Vector2(0.85f, 0.30f));
        cancel.gameObject.AddComponent<Image>().color = new Color(0.45f, 0.30f, 0.30f);
        var cancelBtn = cancel.gameObject.AddComponent<Button>();
        var cancelText = NewRect("Text", cancel);
        Stretch(cancelText);
        var cancelTmp = cancelText.gameObject.AddComponent<TextMeshProUGUI>();
        cancelTmp.text = "CANCEL";
        cancelTmp.fontSize = 32f;
        cancelTmp.alignment = TextAlignmentOptions.Center;
        cancelTmp.color = Color.white;
        cancelTmp.raycastTarget = false;

        // 回填 [SerializeField]（ SerializedObject + WithoutUndo）+ 打开蒙版点击关闭
        var so = new SerializedObject(script);
        so.FindProperty("titleLabel").objectReferenceValue = titleText;
        so.FindProperty("previewImage").objectReferenceValue = previewImage;
        so.FindProperty("recordLabel").objectReferenceValue = recordText;
        so.FindProperty("enterButton").objectReferenceValue = enterBtn;
        so.FindProperty("cancelButton").objectReferenceValue = cancelBtn;
        var maskProp = so.FindProperty("closeOnMaskClick");
        if (maskProp != null) maskProp.boolValue = true;   // AesirBaseWindow 内置字段
        so.ApplyModifiedPropertiesWithoutUndo();

        SaveAsPrefab(root, WindowPath);
        Object.DestroyImmediate(root);
    }

    // ---------- 关卡节点模板（运行时按关卡表 Instantiate N 份，位置由 LevelDef.nodePos 决定） ----------

    private static void BuildLevelNode()
    {
        var root = new GameObject("LevelNode", typeof(RectTransform));
        var rt = (RectTransform)root.transform;
        rt.sizeDelta = new Vector2(140f, 140f);

        // 底图（无 sprite = 白色方块，运行时按解锁/锁定态改 color）
        root.AddComponent<Image>().color = Color.white;
        root.AddComponent<Button>();

        // 关卡名
        var nameRow = NewRect("Name", root.transform);
        Stretch(nameRow, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.92f));
        var nameText = nameRow.gameObject.AddComponent<TextMeshProUGUI>();
        nameText.fontSize = 30f;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        nameText.raycastTarget = false;

        // 星级行
        var starsRow = NewRect("Stars", root.transform);
        Stretch(starsRow, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.42f));
        var starsText = starsRow.gameObject.AddComponent<TextMeshProUGUI>();
        starsText.fontSize = 24f;
        starsText.alignment = TextAlignmentOptions.Center;
        starsText.color = Color.white;
        starsText.raycastTarget = false;

        SaveAsPrefab(root, NodePath);
        Object.DestroyImmediate(root);
    }

    // ---------- 工具 ----------

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private static void Stretch(RectTransform rt) => Anchor(rt, Vector2.zero, Vector2.one);

    private static void Stretch(RectTransform rt, Vector2 min, Vector2 max) => Anchor(rt, min, max);

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SaveAsPrefab(GameObject root, string path)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        PrefabUtility.SaveAsPrefabAsset(root, path);
    }
}
