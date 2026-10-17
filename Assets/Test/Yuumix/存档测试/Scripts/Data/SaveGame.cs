using System;
using UnityEngine;

namespace SaveTest
{
    /// <summary>
    /// 根存档容器：版本号 + 时间戳 + 各模块的 Data。
    /// 新增一个存档模块 = 这里加一个字段 + 加一个对应的 ISaveStep。
    /// </summary>
    [Serializable]
    public sealed class SaveGame
    {
        /// <summary>存档格式版本。结构发生不兼容变更时 +1（迁移逻辑在扩展点统一处理）。</summary>
        public const int CurrentVersion = 1;

        [SerializeField] int version = CurrentVersion;
        [SerializeField] string savedAtUtc = string.Empty;
        [SerializeField] PlayData play = new PlayData();
        [SerializeField] SettingsData settings = new SettingsData();

        public int Version => version;

        public string SavedAtUtc => savedAtUtc;

        public PlayData Play
        {
            get => play;
            set => play = value;
        }

        public SettingsData Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>落盘前盖章：写入当前格式版本与 UTC 时间。</summary>
        public void Stamp(string utc)
        {
            version = CurrentVersion;
            savedAtUtc = utc;
        }
    }
}
