using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.View;
using Runestone.AesirModules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单资产生成器（重跑 = 覆盖产物，禁止在产物上手改结构；要改就改这里再重建）：
/// 1. 拷贝主视觉占位图到 Assets/Art/MainMenu/（主菜单资产不得引用沙盒 Assets/Test）
/// 2. 生成 Assets/Resources/UIPrefabs/MainMenuPanel.prefab、MenuHelpPanel.prefab 与 MenuSettingsPanel.prefab
///    （版式复刻测试沙盒 LoginPanel / HelpPanel / SettingsPanel：五按钮右下锚点 x=-300、y=492/410/328/246/164，
///    左侧占位图 560×640，操作说明弹窗 760×560 + 全屏蒙版，设置面板 860×680：分辨率档位/音量滑条/中英文）
/// 3. 生成 Assets/Scenes/MainMenu.scene（主相机 + MainMenuBoot，Additive 方式不扰动当前打开场景）
/// 4. 把 MainMenu.scene 登记进 Build Settings（Bootstrap 保持 index 0）
/// </summary>
public static class MainMenuBuilder
{
    /// <summary>沙盒占位图源路径（仅拷贝一次，不引用）。</summary>
    const string PlaceholderSource = "Assets/Test/Sunset/Art/PlaceholderFigure.png";

    /// <summary>主视觉占位图在主工程的落位。</summary>
    const string PlaceholderDest = "Assets/Art/MainMenu/PlaceholderFigure.png";

    static string PanelPrefabPath => "Assets/Resources/" + UIPaths.MainMenuPanel + ".prefab";

    static string HelpPrefabPath => "Assets/Resources/" + UIPaths.MenuHelpPanel + ".prefab";

    static string SettingsPrefabPath => "Assets/Resources/" + UIPaths.MenuSettingsPanel + ".prefab";

    [MenuItem("Tools/主菜单/重建主菜单资产")]
    public static void RebuildAll()
    {
        EnsurePlaceholderArt();
        BuildMainMenuPanel();
        BuildMenuHelpPanel();
        BuildMenuSettingsPanel();
        BuildMainMenuScene();
        AssetDatabase.SaveAssets();
        SelfCheck();
    }

    /// <summary>把沙盒的主视觉占位图拷贝到主工程 Art 目录（不复制 .meta，避免 GUID 冲突；导入为 Sprite）。</summary>
    public static void EnsurePlaceholderArt()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderDest) != null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(PlaceholderDest));
        File.Copy(PlaceholderSource, PlaceholderDest);
        AssetDatabase.ImportAsset(PlaceholderDest);

        var importer = (TextureImporter)AssetImporter.GetAtPath(PlaceholderDest);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        Debug.Log($"[MainMenuBuilder] 已拷贝主视觉占位图到 {PlaceholderDest}");
    }

    /// <summary>生成主菜单面板预制体（MainMenuPanel：背景占位 + 左侧主视觉 + 右侧五按钮，设置在说明内容与退出之间）。</summary>
    public static void BuildMainMenuPanel()
    {
        var root = new GameObject("MainMenuPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root);

        // Background：占位纯色，后续直接挂背景图
        var bgRt = CreateRect(root, "Background");
        Stretch(bgRt);
        var bgImage = bgRt.gameObject.AddComponent<Image>();
        bgImage.color = new Color(0.14f, 0.15f, 0.18f, 1f);
        bgImage.raycastTarget = false;

        // VisualPlaceholder：左侧主视觉占位图 + 底部提示
        var vpRt = CreateRect(root, "VisualPlaceholder");
        vpRt.anchorMin = new Vector2(0f, 0.5f);
        vpRt.anchorMax = new Vector2(0f, 0.5f);
        vpRt.anchoredPosition = new Vector2(420f, 60f);
        vpRt.sizeDelta = new Vector2(560f, 640f);
        var vpImage = vpRt.gameObject.AddComponent<Image>();
        vpImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderDest);
        vpImage.preserveAspect = true;
        vpImage.raycastTarget = false;

        var hintRt = CreateRect(vpRt, "PlaceholderHint");
        hintRt.anchorMin = new Vector2(0.5f, 0f);
        hintRt.anchorMax = new Vector2(0.5f, 0f);
        hintRt.anchoredPosition = new Vector2(0f, -55f);
        hintRt.sizeDelta = new Vector2(620f, 44f);
        var hintText = hintRt.gameObject.AddComponent<Text>();
        hintText.text = "主视觉占位（后续在此放素材）";
        hintText.fontSize = 28;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = new Color(0.72f, 0.72f, 0.76f, 1f);
        hintText.raycastTarget = false;
        hintText.font = GetLegacyFont();

        // Buttons 容器 + 五按钮（右下锚点 x=-300，y=492/410/328/246/164，设置在说明内容与退出之间）
        var buttonsRt = CreateRect(root, "Buttons");
        Stretch(buttonsRt);
        var newGame = CreateButton(buttonsRt, "BtnNewGame", "新游戏", new Vector2(-300f, 492f));
        var continueGame = CreateButton(buttonsRt, "BtnContinue", "继续游戏", new Vector2(-300f, 410f));
        var help = CreateButton(buttonsRt, "BtnHelp", "说明内容", new Vector2(-300f, 328f));
        var settings = CreateButton(buttonsRt, "BtnSettings", "设置", new Vector2(-300f, 246f));
        var quit = CreateButton(buttonsRt, "BtnQuit", "退出", new Vector2(-300f, 164f));

        var panel = root.gameObject.AddComponent<MainMenuPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("layer").intValue = (int)UILayer.Normal;
        so.FindProperty("destroyOnHide").boolValue = true;
        so.FindProperty("backgroundImage").objectReferenceValue = bgImage;
        so.FindProperty("newGameButton").objectReferenceValue = newGame;
        so.FindProperty("continueGameButton").objectReferenceValue = continueGame;
        so.FindProperty("helpButton").objectReferenceValue = help;
        so.FindProperty("settingsButton").objectReferenceValue = settings;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root.gameObject, PanelPrefabPath);
    }

    /// <summary>生成操作说明弹窗预制体（MenuHelpPanel：全屏蒙版 + 760×560 内容卡 + × 关闭）。</summary>
    public static void BuildMenuHelpPanel()
    {
        var root = new GameObject("MenuHelpPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root);

        // Mask：全屏半透明蒙版，点击即关闭（Image 拦截射线 + Button 承接点击）
        var maskRt = CreateRect(root, "Mask");
        Stretch(maskRt);
        var maskImage = maskRt.gameObject.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, 0.55f);
        var maskButton = maskRt.gameObject.AddComponent<Button>();
        maskButton.targetGraphic = maskImage;

        // Content：内容卡
        var contentRt = CreateRect(root, "Content");
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(760f, 560f);

        var cardBgRt = CreateRect(contentRt, "Background");
        Stretch(cardBgRt);
        var cardBg = cardBgRt.gameObject.AddComponent<Image>();
        cardBg.color = new Color(0.93f, 0.94f, 0.96f, 1f);
        cardBg.raycastTarget = false;

        var titleRt = CreateRect(contentRt, "Title");
        titleRt.anchorMin = new Vector2(0.5f, 0.5f);
        titleRt.anchorMax = new Vector2(0.5f, 0.5f);
        titleRt.anchoredPosition = new Vector2(0f, 226f);
        titleRt.sizeDelta = new Vector2(600f, 56f);
        var title = titleRt.gameObject.AddComponent<Text>();
        title.text = "操作说明";
        title.fontSize = 44;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color32(50, 50, 50, 255);
        title.font = GetLegacyFont();

        // BtnClose：右上角 ×
        var closeRt = CreateRect(contentRt, "BtnClose");
        closeRt.anchorMin = new Vector2(0.5f, 0.5f);
        closeRt.anchorMax = new Vector2(0.5f, 0.5f);
        closeRt.anchoredPosition = new Vector2(330f, 226f);
        closeRt.sizeDelta = new Vector2(52f, 52f);
        var closeImage = closeRt.gameObject.AddComponent<Image>();
        closeImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        closeImage.type = Image.Type.Sliced;
        var closeButton = closeRt.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;

        var closeTextRt = CreateRect(closeRt, "Text");
        Stretch(closeTextRt);
        var closeText = closeTextRt.gameObject.AddComponent<Text>();
        closeText.text = "×";
        closeText.fontSize = 30;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = new Color32(50, 50, 50, 255);
        closeText.font = GetLegacyFont();

        // Body：操作说明正文
        var bodyRt = CreateRect(contentRt, "Body");
        bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRt.anchoredPosition = new Vector2(0f, -50f);
        bodyRt.sizeDelta = new Vector2(660f, 420f);
        var body = bodyRt.gameObject.AddComponent<Text>();
        body.text = "鼠标移动：预览拼块落点\n鼠标左键：放置当前拼块\nR：旋转拼块\nZ：撤销上一次铺设\nSpace：出发，AI 开始寻路\n\n铺得越快、路线越巧，通关评价越高。";
        body.fontSize = 30;
        body.alignment = TextAnchor.UpperLeft;
        body.color = new Color32(50, 50, 50, 255);
        body.font = GetLegacyFont();

        var panel = root.gameObject.AddComponent<MenuHelpPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("layer").intValue = (int)UILayer.Popup;
        so.FindProperty("destroyOnHide").boolValue = true;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("contentText").objectReferenceValue = body;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root.gameObject, HelpPrefabPath);
    }

    /// <summary>生成设置面板预制体（MenuSettingsPanel：全屏蒙版 + 860×680 内容卡：分辨率档位 / 音效音乐滑条 / 中英文语言）。</summary>
    public static void BuildMenuSettingsPanel()
    {
        var root = new GameObject("MenuSettingsPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root);

        // Mask：全屏半透明蒙版，点击即关闭
        var maskRt = CreateRect(root, "Mask");
        Stretch(maskRt);
        var maskImage = maskRt.gameObject.AddComponent<Image>();
        maskImage.color = new Color(0f, 0f, 0f, 0.55f);
        maskRt.gameObject.AddComponent<Button>();

        // Content：内容卡
        var contentRt = CreateRect(root, "Content");
        contentRt.anchorMin = new Vector2(0.5f, 0.5f);
        contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(860f, 680f);

        var cardBgRt = CreateRect(contentRt, "Background");
        Stretch(cardBgRt);
        var cardBg = cardBgRt.gameObject.AddComponent<Image>();
        cardBg.color = new Color(0.93f, 0.94f, 0.96f, 1f);
        cardBg.raycastTarget = false;

        var title = MakeText(contentRt, "Title", "设置", new Vector2(0f, 296f), new Vector2(420f, 56f), 44, TextAnchor.MiddleCenter);
        var closeButton = MakeCenterButton(contentRt, "BtnClose", "×", new Vector2(392f, 298f), new Vector2(52f, 52f), 30, false);

        // 分辨率行：◀ 档位值 ▶
        var resRow = MakeRow(contentRt, "ResolutionRow", 170f);
        var resRowLabel = MakeText(resRow, "RowLabel", "分辨率", new Vector2(-230f, 0f), new Vector2(250f, 48f), 32, TextAnchor.MiddleRight);
        var resPrev = MakeCenterButton(resRow, "BtnPrev", "<", new Vector2(-70f, 0f), new Vector2(52f, 48f), 30, false);
        var resValue = MakeText(resRow, "Value", "1920 × 1080", new Vector2(60f, 0f), new Vector2(240f, 48f), 30, TextAnchor.MiddleCenter);
        var resNext = MakeCenterButton(resRow, "BtnNext", ">", new Vector2(195f, 0f), new Vector2(52f, 48f), 30, false);

        // 音效行（滑条 x=115，与行标签保持间隙不压字）
        var sfxRow = MakeRow(contentRt, "SfxRow", 70f);
        var sfxLabel = MakeText(sfxRow, "RowLabel", "音效", new Vector2(-230f, 0f), new Vector2(250f, 48f), 32, TextAnchor.MiddleRight);
        var sfxSlider = MakeVolumeSlider(sfxRow);

        // 音乐行
        var musicRow = MakeRow(contentRt, "MusicRow", -30f);
        var musicLabel = MakeText(musicRow, "RowLabel", "音乐", new Vector2(-230f, 0f), new Vector2(250f, 48f), 32, TextAnchor.MiddleRight);
        var musicSlider = MakeVolumeSlider(musicRow);

        // 语言行（Transition.None，选中色由 MenuSettingsPanel 运行时写 Image.color）
        var langRow = MakeRow(contentRt, "LanguageRow", -150f);
        var langLabel = MakeText(langRow, "RowLabel", "语言", new Vector2(-230f, 0f), new Vector2(250f, 48f), 32, TextAnchor.MiddleRight);
        var chineseButton = MakeCenterButton(langRow, "BtnChinese", "中文", new Vector2(-10f, 0f), new Vector2(160f, 52f), 30, true);
        var englishButton = MakeCenterButton(langRow, "BtnEnglish", "English", new Vector2(190f, 0f), new Vector2(160f, 52f), 26, true);

        var panel = root.gameObject.AddComponent<MenuSettingsPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("layer").intValue = (int)UILayer.Popup;
        so.FindProperty("destroyOnHide").boolValue = true;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("resolutionRowLabel").objectReferenceValue = resRowLabel;
        so.FindProperty("resolutionValueText").objectReferenceValue = resValue;
        so.FindProperty("sfxLabel").objectReferenceValue = sfxLabel;
        so.FindProperty("musicLabel").objectReferenceValue = musicLabel;
        so.FindProperty("languageLabel").objectReferenceValue = langLabel;
        so.FindProperty("resolutionPrevButton").objectReferenceValue = resPrev;
        so.FindProperty("resolutionNextButton").objectReferenceValue = resNext;
        so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
        so.FindProperty("chineseButton").objectReferenceValue = chineseButton;
        so.FindProperty("englishButton").objectReferenceValue = englishButton;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root.gameObject, SettingsPrefabPath);
    }

    /// <summary>
    /// 生成主菜单场景（主相机 + MainMenuBoot）。
    /// Additive 新建场景后再还原激活场景，全程不关闭、不改动用户当前打开的场景。
    /// </summary>
    public static void BuildMainMenuScene()
    {
        var original = EditorSceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
        try
        {
            // 显式激活新场景，保证后续 new GameObject 落在新场景内
            EditorSceneManager.SetActiveScene(scene);

            var cam = Camera.main;
            if (cam == null)
            {
                throw new System.InvalidOperationException("新建场景缺少主相机（DefaultGameObjects 应自带 Main Camera）");
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.14f, 0.15f, 0.18f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            // 主相机排除 UI(5) 与 TransparentFX(1)：UI 只由 UIRoot 的 UICamera 渲染，避免重复渲染加深蒙版
            cam.cullingMask = ~((1 << 5) | (1 << 1));
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var boot = new GameObject("MainMenuBoot");
            boot.AddComponent<MainMenuBoot>();

            EditorSceneManager.SaveScene(scene, ScenePaths.MainMenu);
        }
        finally
        {
            if (EditorSceneManager.GetActiveScene() == scene && original.IsValid())
            {
                EditorSceneManager.SetActiveScene(original);
            }

            EditorSceneManager.CloseScene(scene, true);
        }

        RegisterInBuildSettings();
        Debug.Log($"[MainMenuBuilder] 已生成 {ScenePaths.MainMenu} 并登记 Build Settings");
    }

    /// <summary>把主菜单场景登记进 Build Settings（Bootstrap 保持 index 0，MainMenu 追加）。</summary>
    static void RegisterInBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (list.Exists(s => s.path == ScenePaths.MainMenu))
        {
            return;
        }

        list.Add(new EditorBuildSettingsScene(ScenePaths.MainMenu, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    /// <summary>自检：产物根组件类型与关键接线逐项断言，静默失败在此变成显式错误。</summary>
    static void SelfCheck()
    {
        var failures = new List<string>();

        var panelPrefab = AssetDatabase.LoadAssetAtPath<MainMenuPanel>(PanelPrefabPath);
        if (panelPrefab == null)
        {
            failures.Add($"{PanelPrefabPath} 根组件不是 MainMenuPanel 或加载失败");
        }
        else
        {
            var so = new SerializedObject(panelPrefab);
            foreach (var field in new[] { "backgroundImage", "newGameButton", "continueGameButton", "helpButton", "settingsButton", "quitButton" })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                {
                    failures.Add($"{PanelPrefabPath} 字段 {field} 未接线");
                }
            }

            if (so.FindProperty("layer").intValue != (int)UILayer.Normal)
            {
                failures.Add($"{PanelPrefabPath} layer 应为 Normal");
            }
        }

        var helpPrefab = AssetDatabase.LoadAssetAtPath<MenuHelpPanel>(HelpPrefabPath);
        if (helpPrefab == null)
        {
            failures.Add($"{HelpPrefabPath} 根组件不是 MenuHelpPanel 或加载失败");
        }
        else
        {
            var so = new SerializedObject(helpPrefab);
            foreach (var field in new[] { "titleText", "contentText", "closeButton" })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                {
                    failures.Add($"{HelpPrefabPath} 字段 {field} 未接线");
                }
            }

            if (so.FindProperty("layer").intValue != (int)UILayer.Popup)
            {
                failures.Add($"{HelpPrefabPath} layer 应为 Popup");
            }
        }

        var settingsPrefab = AssetDatabase.LoadAssetAtPath<MenuSettingsPanel>(SettingsPrefabPath);
        if (settingsPrefab == null)
        {
            failures.Add($"{SettingsPrefabPath} 根组件不是 MenuSettingsPanel 或加载失败");
        }
        else
        {
            var so = new SerializedObject(settingsPrefab);
            foreach (var field in new[]
                     {
                         "titleText", "resolutionRowLabel", "resolutionValueText", "sfxLabel", "musicLabel", "languageLabel",
                         "resolutionPrevButton", "resolutionNextButton", "sfxSlider", "musicSlider",
                         "chineseButton", "englishButton", "closeButton"
                     })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                {
                    failures.Add($"{SettingsPrefabPath} 字段 {field} 未接线");
                }
            }

            if (so.FindProperty("layer").intValue != (int)UILayer.Popup)
            {
                failures.Add($"{SettingsPrefabPath} layer 应为 Popup");
            }
        }

        if (!System.Array.Exists(EditorBuildSettings.scenes, s => s.path == ScenePaths.MainMenu))
        {
            failures.Add($"Build Settings 未登记 {ScenePaths.MainMenu}");
        }

        if (EditorBuildSettings.scenes.Length == 0 || EditorBuildSettings.scenes[0].path != "Assets/Scenes/Bootstrap.unity")
        {
            failures.Add("Build Settings index 0 不是 Bootstrap.unity");
        }

        if (failures.Count > 0)
        {
            Debug.LogError("[MainMenuBuilder] 自检失败：\n" + string.Join("\n", failures));
            return;
        }

        Debug.Log("[MainMenuBuilder] 主菜单资产重建完成，自检通过（MainMenuPanel / MenuHelpPanel / MenuSettingsPanel / MainMenu.scene / Build Settings）");
    }

    static void SavePrefab(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[MainMenuBuilder] 已生成 {path}");
    }

    static RectTransform CreateRect(Transform parent, string name)
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
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>主菜单按钮（白色九宫格底 + 深灰居中文字），右下锚点 320×64。</summary>
    static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        var rt = CreateRect(parent, name);
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(320f, 64f);

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var labelRt = CreateRect(rt, "Text");
        Stretch(labelRt);
        var text = labelRt.gameObject.AddComponent<Text>();
        text.text = label;
        text.fontSize = 34;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color32(50, 50, 50, 255);
        text.font = GetLegacyFont();
        return button;
    }

    /// <summary>设置面板行容器：居中锚点，760×64。</summary>
    static RectTransform MakeRow(Transform parent, string name, float y)
    {
        var rt = CreateRect(parent, name);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(760f, 64f);
        return rt;
    }

    /// <summary>设置面板文本：居中锚点，深灰，不拦截射线。</summary>
    static Text MakeText(Transform parent, string name, string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
    {
        var rt = CreateRect(parent, name);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var t = rt.gameObject.AddComponent<Text>();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = align;
        t.color = new Color32(50, 50, 50, 255);
        t.raycastTarget = false;
        t.font = GetLegacyFont();
        return t;
    }

    /// <summary>设置面板居中锚点按钮（transitionNone 时取消 ColorTint，由 Image.color 承载选中色）。</summary>
    static Button MakeCenterButton(Transform parent, string name, string labelText, Vector2 pos, Vector2 size, int fontSize, bool transitionNone)
    {
        var rt = CreateRect(parent, name);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (transitionNone)
        {
            button.transition = Selectable.Transition.None;
        }

        var labelRt = CreateRect(rt, "Text");
        Stretch(labelRt);
        var label = labelRt.gameObject.AddComponent<Text>();
        label.text = labelText;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color32(50, 50, 50, 255);
        label.font = GetLegacyFont();
        return button;
    }

    /// <summary>音量滑条：居中锚点 x=115、420×28，LeftToRight 0~1。</summary>
    static Slider MakeVolumeSlider(Transform row)
    {
        var sliderRt = CreateRect(row, "Slider");
        sliderRt.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRt.anchoredPosition = new Vector2(115f, 0f);
        sliderRt.sizeDelta = new Vector2(420f, 28f);

        var bgRt = CreateRect(sliderRt, "Background");
        Stretch(bgRt);
        var bg = bgRt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.78f, 0.78f, 0.80f, 1f);

        var fillAreaRt = CreateRect(sliderRt, "Fill Area");
        fillAreaRt.anchorMin = Vector2.zero;
        fillAreaRt.anchorMax = Vector2.one;
        fillAreaRt.offsetMin = new Vector2(6f, 0f);
        fillAreaRt.offsetMax = new Vector2(-6f, 0f);
        var fillRt = CreateRect(fillAreaRt, "Fill");
        Stretch(fillRt);
        var fill = fillRt.gameObject.AddComponent<Image>();
        fill.color = new Color(0.35f, 0.55f, 0.85f, 1f);
        fill.raycastTarget = false;

        var handleAreaRt = CreateRect(sliderRt, "Handle Slide Area");
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(6f, -14f);
        handleAreaRt.offsetMax = new Vector2(-6f, 14f);
        var handleRt = CreateRect(handleAreaRt, "Handle");
        handleRt.sizeDelta = new Vector2(26f, 40f);
        var handleImage = handleRt.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        var slider = sliderRt.gameObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImage;
        return slider;
    }

    static Font GetLegacyFont()
    {
        try
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch (System.Exception)
        {
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
