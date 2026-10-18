using System.IO;
using Game.View;
using Runestone.AesirModules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 加载过场面板生成器（重跑 = 覆盖产物，禁止在产物上手改结构；要改就改这里再重建）：
/// 生成 Assets/Resources/UIPrefabs/LoadingPanel.prefab——主菜单 → 选关切换的过场加载界面。
/// 结构：封面背景铺底 + 深紫压暗（拦截全部输入）→ 右缘旋转宝石环（18 帧序列，贴右边界的加载指示）内嵌四态等距方块组 →
/// 四角星自转点缀 → 环左侧双文字条（中文「游戏加载中」+ 英文「LOADING」呼吸，左对齐文字块与环垂直对中）。
/// 素材：Assets/Arts/封面设计图/加载组件.png（2026-10-18 语义化重命名：Ring_01..18 / Ring_Static / Text_Cn / Text_En / Tiles / Star）。
/// </summary>
public static class LoadingPanelBuilder
{
    /// <summary>加载组件图集（旋转宝石环 18 帧 + 文字条 + 方块组 + 四角星）。</summary>
    const string SheetPath = "Assets/Arts/封面设计图/加载组件.png";

    /// <summary>全屏背景：AI 抠图成品封面背景（星空，与主菜单同源，过渡有延续感）。</summary>
    const string BackgroundPath = "Assets/Arts/封面设计图/封面背景.png";

    static string PrefabPath => "Assets/Resources/" + Game.Core.UIPaths.LoadingPanel + ".prefab";

    [MenuItem("Tools/加载/重建加载过场面板")]
    public static void RebuildAll()
    {
        BuildLoadingPanel();
        AssetDatabase.SaveAssets();
        SelfCheck();
    }

    /// <summary>生成加载过场面板预制体（LoadingPanel：全屏 Top 层，背景 + 旋转宝石环 + 方块组 + 双文字条）。</summary>
    public static void BuildLoadingPanel()
    {
        var root = new GameObject("LoadingPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        Stretch(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();

        // 背景：封面星空铺满（拦截射线 = 加载期间挡住一切输入）
        var bgRt = CreateRect(root, "Background");
        Stretch(bgRt);
        var bg = bgRt.gameObject.AddComponent<Image>();
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
        bg.raycastTarget = true;

        // 压暗层：深紫半透明，压住背景亮度让中央元素与文字更突出
        var darkenRt = CreateRect(root, "Darken");
        Stretch(darkenRt);
        var darken = darkenRt.gameObject.AddComponent<Image>();
        darken.color = new Color(0.05f, 0.02f, 0.12f, 0.50f);
        darken.raycastTarget = true;

        // 右下角旋转宝石环（18 帧序列，运行时按帧率换帧）——位置为用户圈定区（约 68%/83% 屏位）
        var ringRt = CreateRect(root, "Ring");
        Center(ringRt, new Vector2(737.5f, -360f), new Vector2(225f, 225f));
        var ring = ringRt.gameObject.AddComponent<Image>();
        ring.sprite = SheetSprite("Ring_01");
        ring.raycastTarget = false;

        // 环心：四态等距方块组（冰/火/魔晶/石——棋盘玩法的意象缩影）
        var tilesRt = CreateRect(root, "Tiles");
        Center(tilesRt, new Vector2(737.5f, -360f), new Vector2(133f, 140f));
        var tiles = tilesRt.gameObject.AddComponent<Image>();
        tiles.sprite = SheetSprite("Tiles");
        tiles.raycastTarget = false;

        // 四角星：压在环右上缘，运行时自转
        var starRt = CreateRect(root, "Star");
        Center(starRt, new Vector2(832f, -272f), new Vector2(37.5f, 59f));
        var star = starRt.gameObject.AddComponent<Image>();
        star.sprite = SheetSprite("Star");
        star.raycastTarget = false;

        // 中文文字条「游戏加载中」——环左侧、与环垂直对中（左对齐文字块的上行，整组等比 0.75）
        var textCnRt = CreateRect(root, "TextCn");
        Center(textCnRt, new Vector2(359.5f, -322.5f), new Vector2(421f, 101f));
        var textCn = textCnRt.gameObject.AddComponent<Image>();
        textCn.sprite = SheetSprite("Text_Cn");
        textCn.raycastTarget = false;

        // 英文文字条「LOADING」（运行时呼吸）——文字块下行，与中文条左缘对齐
        var textEnRt = CreateRect(root, "TextEn");
        Center(textEnRt, new Vector2(316f, -417.5f), new Vector2(334f, 61f));
        var textEn = textEnRt.gameObject.AddComponent<Image>();
        textEn.sprite = SheetSprite("Text_En");
        textEn.raycastTarget = false;

        var panel = root.gameObject.AddComponent<LoadingPanel>();
        var so = new SerializedObject(panel);
        so.FindProperty("layer").intValue = (int)UILayer.Top;
        so.FindProperty("destroyOnHide").boolValue = true;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.FindProperty("backgroundImage").objectReferenceValue = bg;
        so.FindProperty("ringImage").objectReferenceValue = ring;
        so.FindProperty("ringFrameRate").floatValue = 16f;
        so.FindProperty("starRect").objectReferenceValue = starRt;
        so.FindProperty("textEnImage").objectReferenceValue = textEn;
        so.FindProperty("minDurationSeconds").floatValue = 1.2f;
        so.FindProperty("fadeSeconds").floatValue = 0.35f;

        // 帧序列接线：Ring_01..Ring_18
        var frames = so.FindProperty("ringFrames");
        frames.arraySize = 18;
        for (var i = 0; i < 18; i++)
        {
            frames.GetArrayElementAtIndex(i).objectReferenceValue = SheetSprite("Ring_" + (i + 1).ToString("00"));
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        SavePrefab(root.gameObject, PrefabPath);
    }

    /// <summary>自检：产物根组件类型与关键接线逐项断言，静默失败在此变成显式错误。</summary>
    static void SelfCheck()
    {
        var failures = new System.Collections.Generic.List<string>();

        var prefab = AssetDatabase.LoadAssetAtPath<LoadingPanel>(PrefabPath);
        if (prefab == null)
        {
            failures.Add($"{PrefabPath} 根组件不是 LoadingPanel 或加载失败");
        }
        else
        {
            var so = new SerializedObject(prefab);
            foreach (var field in new[] { "canvasGroup", "backgroundImage", "ringImage", "starRect", "textEnImage" })
            {
                if (so.FindProperty(field).objectReferenceValue == null)
                {
                    failures.Add($"{PrefabPath} 字段 {field} 未接线");
                }
            }

            var frames = so.FindProperty("ringFrames");
            if (frames.arraySize != 18)
            {
                failures.Add($"{PrefabPath} ringFrames 应为 18 帧，实际 {frames.arraySize}");
            }
            else
            {
                for (var i = 0; i < 18; i++)
                {
                    if (frames.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    {
                        failures.Add($"{PrefabPath} ringFrames[{i}] 未接线");
                    }
                }
            }

            if (so.FindProperty("layer").intValue != (int)UILayer.Top)
            {
                failures.Add($"{PrefabPath} layer 应为 Top");
            }

            var bg = so.FindProperty("backgroundImage").objectReferenceValue as Image;
            if (bg != null && !bg.raycastTarget)
            {
                failures.Add($"{PrefabPath} 背景必须拦截射线（加载期间挡住一切输入）");
            }
        }

        if (failures.Count > 0)
        {
            Debug.LogError("[LoadingPanelBuilder] 自检失败：\n" + string.Join("\n", failures));
            return;
        }

        Debug.Log("[LoadingPanelBuilder] 加载过场面板重建完成，自检通过（LoadingPanel / Top 层 / 18 帧接线 / 背景拦射线）");
    }

    /// <summary>从加载组件图集按切片名取 Sprite（缺失时报错返回 null）。</summary>
    static Sprite SheetSprite(string name)
    {
        foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
        {
            var sprite = obj as Sprite;
            if (sprite != null && sprite.name == name)
            {
                return sprite;
            }
        }

        Debug.LogError($"[LoadingPanelBuilder] 图集 {SheetPath} 缺少切片 {name}");
        return null;
    }

    static void SavePrefab(GameObject root, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"[LoadingPanelBuilder] 已生成 {path}");
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

    static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
