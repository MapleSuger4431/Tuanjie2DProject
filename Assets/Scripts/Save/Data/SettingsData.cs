using System;

namespace Game.Save
{
    /// <summary>SettingsModel 对应的存档类：设置项几乎全部需要存档。</summary>
    [Serializable]
    public sealed class SettingsData
    {
        public float bgmVolume = 1f;
        public float sfxVolume = 1f;
        public string language = "zh-CN";
        public int resolutionIndex;
    }
}
