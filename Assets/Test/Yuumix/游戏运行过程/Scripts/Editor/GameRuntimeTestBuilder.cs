using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GameRuntimeTest.EditorTools
{
    /// <summary>
    /// 游戏运行过程测试场景生成器：全部资产经代码生成（禁手写 YAML）。
    /// 产物 = RuntimeHudPanel.prefab + RuntimePauseWindow.prefab + GameRuntimeTest.unity + Build Settings 登记。
    /// 重跑 = 覆盖产物，要改结构请改本生成器。
    /// </summary>
    public static class GameRuntimeTestBuilder
    {
        const string RootDir = "Assets/Test/Yuumix/游戏运行过程";
        const string ScenesDir = RootDir + "/Scenes";
        const string PrefabsDir = RootDir + "/Prefabs";
        const string HudPrefabPath = PrefabsDir + "/RuntimeHudPanel.prefab";
        const string PausePrefabPath = PrefabsDir + "/RuntimePauseWindow.prefab";
        const string ScenePath = ScenesDir + "/GameRuntimeTest.unity";

        [MenuItem("Tools/Yuumix/游戏运行过程/Rebuild All")]
        public static void RebuildAll()
        {
            EnsureFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var hudRoot = BuildHudHierarchy();
            PrefabUtility.SaveAsPrefabAsset(hudRoot, HudPrefabPath);
            Object.DestroyImmediate(hudRoot);

            var windowRoot = BuildPauseWindowHierarchy();
            PrefabUtility.SaveAsPrefabAsset(windowRoot, PausePrefabPath);
            Object.DestroyImmediate(windowRoot);

            BuildSceneContent();
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterSceneInBuildSettings(ScenePath);

            AssetDatabase.SaveAssets();
            Debug.Log($"[GameRuntimeTestBuilder] 生成完成：\n{HudPrefabPath}\n{PausePrefabPath}\n{ScenePath}");
        }

        static void EnsureFolders()
        {
            var segments = new[]
            {
                "Assets/Test", "Assets/Test/Yuumix", RootDir, ScenesDir, PrefabsDir,
                RootDir + "/Scripts", RootDir + "/Scripts/Editor"
            };
            foreach (var dir in segments)
            {
                if (AssetDatabase.IsValidFolder(dir)) continue;
                var parent = dir.Substring(0, dir.LastIndexOf('/'));
                var leaf = dir.Substring(dir.LastIndexOf('/') + 1);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        static void RegisterSceneInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ---------- HUD 面板 ----------

        static GameObject BuildHudHierarchy()
        {
            var root = CreateUIRoot("RuntimeHudPanel");
            root.AddComponent<RuntimeHudPanel>();

            var scoreText = CreateText(root.transform, "ScoreText", "得分：0",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(320f, 44f),
                26, TextAnchor.MiddleLeft, Color.white);
            var timeText = CreateText(root.transform, "TimeText", "时间：60.0",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(320f, 44f),
                26, TextAnchor.MiddleCenter, Color.white);
            var stateText = CreateText(root.transform, "StateText", string.Empty,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 90f),
                48, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
            var hintText = CreateText(root.transform, "HintText", string.Empty,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1000f, 30f),
                18, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f));
            var pauseButton = CreateButton(root.transform, "PauseButton", "暂停",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-90f, -40f), new Vector2(120f, 52f));

            var panel = root.GetComponent<RuntimeHudPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("timeText").objectReferenceValue = timeText;
            so.FindProperty("stateText").objectReferenceValue = stateText;
            so.FindProperty("hintText").objectReferenceValue = hintText;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ---------- 暂停窗口（根自带 Canvas） ----------

        static GameObject BuildPauseWindowHierarchy()
        {
            var root = CreateUIRoot("RuntimePauseWindow");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<RuntimePauseWindow>();

            // UIModule 按名字查找 Mask 子节点，蒙版点击的 Button 必须在 Mask 上
            var mask = CreateUIRoot("Mask");
            mask.transform.SetParent(root.transform, false);
            Stretch(mask);
            var maskImage = mask.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.6f);
            mask.AddComponent<Button>();

            var content = CreateUIRoot("Content");
            content.transform.SetParent(root.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.sizeDelta = new Vector2(520f, 340f);
            var contentImage = content.AddComponent<Image>();
            contentImage.color = new Color(0.12f, 0.13f, 0.18f, 0.95f);

            var titleText = CreateText(content.transform, "TitleText", "暂停",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(400f, 60f),
                36, TextAnchor.MiddleCenter, Color.white);
            var resumeButton = CreateButton(content.transform, "ResumeButton", "继续",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(220f, 60f));
            var restartButton = CreateButton(content.transform, "RestartButton", "重开",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(220f, 60f));

            var window = root.GetComponent<RuntimePauseWindow>();
            var so = new SerializedObject(window);
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            so.FindProperty("restartButton").objectReferenceValue = restartButton;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ---------- 场景内容 ----------

        static void BuildSceneContent()
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.13f, 0.15f, 0.2f);
            // 排除 UI(5) 与 TransparentFX(1)，防止与 UICamera 重复渲染
            camera.cullingMask = -1 & ~(1 << 5) & ~(1 << 1);
            cameraGo.AddComponent<AudioListener>();

            var playerGo = new GameObject("Player");
            var renderer = playerGo.AddComponent<SpriteRenderer>();
            renderer.color = new Color(0.3f, 0.9f, 1f);
            playerGo.AddComponent<RuntimePlayerMover>();

            var driverGo = new GameObject("GameRuntimeTestDriver");
            var driver = driverGo.AddComponent<GameRuntimeTestDriver>();
            var so = new SerializedObject(driver);
            so.FindProperty("hudPanelPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            so.FindProperty("pauseWindowPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PausePrefabPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- uGUI 构建小工具 ----------

        static GameObject CreateUIRoot(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            return go;
        }

        static void Stretch(GameObject go)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Text CreateText(Transform parent, string name, string content,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size,
            int fontSize, TextAnchor alignment, Color color)
        {
            var go = CreateUIRoot(name);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) text.font = font;
            return text;
        }

        static Button CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var go = CreateUIRoot(name);
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.25f, 0.45f, 0.75f, 0.95f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;

            var text = CreateText(go.transform, "Text", label,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                24, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.gameObject);
            return button;
        }
    }
}
