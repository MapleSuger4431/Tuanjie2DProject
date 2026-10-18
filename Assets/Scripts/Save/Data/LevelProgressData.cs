using System;
using System.Collections.Generic;

namespace Game.Save
{
    /// <summary>
    /// 单关通关记录：最短用时 / 上次用时 / 最少步数 / 通关次数。
    /// 用时为负表示从未通关（<see cref="LevelRecordData.NoTime" />）。
    /// </summary>
    [Serializable]
    public sealed class LevelRecordData
    {
        /// <summary>「没有成绩」的哨兵值：用时与步数在未通关时都取它。</summary>
        public const float NoTime = -1f;

        public int levelId;
        public float bestTime = NoTime;
        public float lastTime = NoTime;
        public int bestSteps = -1;
        public int clearCount;

        /// <summary>
        /// 历史最好星级（0~<see cref="Game.Board.LevelStarRules.MaxStars" />）：通关 1 星、捡金币 +1 星、
        /// 金币 + 时限内通关再 +1 星，取历史最大值（旧存档没有这个字段时按 1 星兜底）。
        /// </summary>
        public int bestStars;

        public LevelRecordData() { }

        public LevelRecordData(int levelId) => this.levelId = levelId;

        public bool HasRecord => bestTime >= 0f;

        /// <summary>存档读写按值拷贝，避免运行时实例与存档容器共享同一对象。</summary>
        public LevelRecordData Clone() => new LevelRecordData
        {
            levelId = levelId,
            bestTime = bestTime,
            lastTime = lastTime,
            bestSteps = bestSteps,
            clearCount = clearCount,
            bestStars = bestStars,
        };
    }

    /// <summary>
    /// LevelProgressModel 对应的存档类：每关一条 <see cref="LevelRecordData" />。
    /// 用 List 而不是 Dictionary——JsonUtility 不序列化字典。
    /// </summary>
    [Serializable]
    public sealed class LevelProgressData
    {
        public List<LevelRecordData> records = new List<LevelRecordData>();
    }
}
