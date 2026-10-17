using System;

namespace SaveTest
{
    /// <summary>SettingsModel 对应的存档类。</summary>
    [Serializable]
    public sealed class SettingsData
    {
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public string language = "zh-CN";
    }
}
