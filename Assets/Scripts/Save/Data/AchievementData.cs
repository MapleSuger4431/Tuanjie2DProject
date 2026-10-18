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

        /// <summary>
        /// 解锁通知（右下角小窗）是否已发过：每个存档每条成就只提示一次，
        /// 随存档持久化，防止任何路径（重开 / 重进 / 未来重放）重复弹提示。旧档缺字段反序列化为 false，
        /// 但已解锁成就判定层直接跳过，不会被重复通知。
        /// </summary>
        public bool notified;
    }

    /// <summary>成就模块存档数据（挂在 <see cref="SaveGame" /> 上）。</summary>
    [Serializable]
    public sealed class AchievementData
    {
        public List<AchievementRecordData> records = new List<AchievementRecordData>();
    }
}
