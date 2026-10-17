using System;
using UnityEngine;
using Runestone.AesirArchitecture;

/// <summary>关卡定义：选关大地图节点 + 切场景所需字段。场景名为占位符，待关卡组提供后替换。</summary>
[Serializable]
public class LevelDef
{
    public int levelId;
    public string displayName;          // 界面显示名（中文字体就位后可切"第 N 关"）
    public string sceneName;            // TODO: 占位场景名，待关卡组提供并登记 Build Settings
    public Vector2 nodePos;             // 大地图节点位置（归一化 0~1，相对地图区域）
}

/// <summary>UI 资源路径常量（相对 Resources，prefab 位于 Assets/TestTF/UI/Resources/ 下）。</summary>
public static class TestTFUIPaths
{
    public const string LevelSelectPanel = "GameUI/LevelSelectPanel";
    public const string LevelDetailWindow = "GameUI/LevelDetailWindow";

    // ---- 占位符（待就位后替换）----
    /// <summary>TODO 占位：开始菜单场景完整路径，待场景组提供并登记 Build Settings。</summary>
    public const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
    /// <summary>TODO 占位：设置窗口 prefab 路径，待队友的设置窗口 prefab 就位。</summary>
    public const string SettingsWindow = "GameUI/SettingsWindow";
}

/// <summary>
/// 选关 Model（Quick 档）：关卡表 + 通关记录存档。
/// 记录规则对齐策划案第 7 节三星制（★到达 / ★金币 / ★用时），此处存总星数与最佳用时。
/// 解锁规则：线性——第 N 关需第 N-1 关至少 1 星。
/// </summary>
public sealed class LevelSelectModel : AbstractModel
{
    // 占位关卡表：节点位置按地图蜿蜒路线排布，美术地图到位后微调 nodePos 即可
    public readonly LevelDef[] Levels =
    {
        new LevelDef { levelId = 1, displayName = "LEVEL 1", sceneName = "Level_01", nodePos = new Vector2(0.12f, 0.72f) },
        new LevelDef { levelId = 2, displayName = "LEVEL 2", sceneName = "Level_02", nodePos = new Vector2(0.35f, 0.58f) },
        new LevelDef { levelId = 3, displayName = "LEVEL 3", sceneName = "Level_03", nodePos = new Vector2(0.52f, 0.76f) },
        new LevelDef { levelId = 4, displayName = "LEVEL 4", sceneName = "Level_04", nodePos = new Vector2(0.72f, 0.52f) },
        new LevelDef { levelId = 5, displayName = "LEVEL 5", sceneName = "Level_05", nodePos = new Vector2(0.88f, 0.28f) },
    };

    private const string KeyPrefix = "edge_grid_lv_";

    public LevelDef GetDef(int levelId)
    {
        foreach (var def in Levels)
            if (def.levelId == levelId) return def;
        return null;
    }

    /// <summary>最佳星数（0~3，0 表示未通关）。</summary>
    public int GetBestStars(int levelId) => PlayerPrefs.GetInt(KeyPrefix + levelId + "_stars", 0);

    /// <summary>最佳用时；-1 表示从未通关。</summary>
    public float GetBestTime(int levelId) => PlayerPrefs.GetFloat(KeyPrefix + levelId + "_time", -1f);

    public bool HasRecord(int levelId) => GetBestStars(levelId) > 0;

    public bool IsUnlocked(int levelId) => levelId <= 1 || GetBestStars(levelId - 1) >= 1;

    /// <summary>结算时由关卡侧调用：只保留历史最好成绩。</summary>
    public void SetBestRecord(int levelId, int stars, float time)
    {
        if (stars > GetBestStars(levelId)) PlayerPrefs.SetInt(KeyPrefix + levelId + "_stars", stars);
        var best = GetBestTime(levelId);
        if (best < 0f || time < best) PlayerPrefs.SetFloat(KeyPrefix + levelId + "_time", time);
        PlayerPrefs.Save();
    }
}
