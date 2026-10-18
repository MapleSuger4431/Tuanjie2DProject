using System;
using System.Collections.Generic;

namespace Game.Save
{
    /// <summary>单条成就的存档状态。</summary>
    [Serializable]
    public sealed class AchievementRecordData
    {
        /// <summary>对应 <see cref="Game.Model.GameAchievement.Id" />。</summary>
        public string id = string.Empty;

        /// <summary>是否已解锁（一旦解锁不因任何后续操作回退）。</summary>
        public bool unlocked;

        /// <summary>解锁时间（ISO 8601 UTC）。</summary>
        public string unlockTimeUtc = string.Empty;

        /// <summary>计数型成就的当前进度；事件型恒为 0 或阈值。</summary>
        public int progress;
    }

    /// <summary>成就模块存档数据（挂在 <see cref="SaveGame" /> 上）。</summary>
    [Serializable]
    public sealed class AchievementData
    {
        public List<AchievementRecordData> records = new List<AchievementRecordData>();
    }
}
