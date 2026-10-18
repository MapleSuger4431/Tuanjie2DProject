using System;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Model
{
    /// <summary>关卡定义：选关大地图节点 + 切场景所需字段。场景名为占位符，待关卡组提供后替换。</summary>
    [Serializable]
    public class LevelDef
    {
        public int levelId;
        public string displayName;          // 调试用内部名，界面显示走本地化 LevelName
        public string sceneName;            // TODO: 占位场景名，待关卡组提供并登记 Build Settings
        public Vector2 nodePos;             // 大地图节点位置（归一化 0~1，相对地图区域）
    }

    /// <summary>
    /// 选关 Model（Quick 档）：关卡表 + 通关记录查询。
    /// <para>
    /// 记录本身**不在本类**：唯一真源是 <see cref="LevelProgressModel" />（随存档文件持久化，
    /// 全项目已废弃 PlayerPrefs），本类只做关卡表与「按关卡查记录」的门面。
    /// </para>
    /// 解锁规则：线性——第 N 关需第 N-1 关已通关。
    /// </summary>
    public sealed class LevelSelectModel : AbstractModel
    {
        // 占位关卡表：2026-10-18 节点位置已按背景图特征点像素扫描定位（用户拍板，沿发光路排布）——
        // 1 关=左下三棵树环绕的圆形平台、2 关=中上岩浆区中心、3 关=右下紫色虚影中心。
        // 目前只设 3 个关卡（节点图标按 crystal/badge 素材三组一一对应）
        public readonly LevelDef[] Levels =
        {
            new LevelDef { levelId = 1, displayName = "LEVEL 1", sceneName = "Level_01", nodePos = new Vector2(0.111f, 0.273f) },
            new LevelDef { levelId = 2, displayName = "LEVEL 2", sceneName = "Level_02", nodePos = new Vector2(0.548f, 0.871f) },
            new LevelDef { levelId = 3, displayName = "LEVEL 3", sceneName = "Level_03", nodePos = new Vector2(0.843f, 0.311f) },
        };

        LevelProgressModel _progress;

        protected override void OnInitialize() => _progress = this.GetModel<LevelProgressModel>();

        public LevelDef GetDef(int levelId)
        {
            foreach (var def in Levels)
                if (def.levelId == levelId) return def;
            return null;
        }

        /// <summary>已通关。</summary>
        public bool HasRecord(int levelId) => _progress.HasRecord(levelId);

        /// <summary>星级（0~3）：三星制口径见 LevelProgressModel.GetBestStars——通关 1 星、捡金币 +1 星、金币且时限内通关 +1 星。</summary>
        public int GetBestStars(int levelId) => _progress.GetBestStars(levelId);

        /// <summary>最短用时（秒）；-1 表示从未通关。</summary>
        public float GetBestTime(int levelId) => _progress.GetBestTime(levelId);

        /// <summary>最近一次通关用时（秒）；从未通关为 -1。</summary>
        public float GetLastTime(int levelId) => _progress.GetLastTime(levelId);

        /// <summary>通关次数。</summary>
        public int GetClearCount(int levelId) => _progress.GetClearCount(levelId);

        public bool IsUnlocked(int levelId) => levelId <= 1 || HasRecord(levelId - 1);
    }
}
