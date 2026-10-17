using System;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace SaveTest
{
    /// <summary>
    /// 第二个模块示例：设置项几乎全部需要存档，用于验证「按模块划分、逐步扩展」的流程。
    /// </summary>
    [Serializable]
    public sealed class SettingsModel : AbstractModel, ISavableModel<SettingsData>
    {
        [SerializeField] ObservableValue<float> bgmVolume = new ObservableValue<float>(1f);
        [SerializeField] ObservableValue<float> sfxVolume = new ObservableValue<float>(1f);
        [SerializeField] ObservableValue<string> language = new ObservableValue<string>("zh-CN");

        public ObservableValue<float> BgmVolume => bgmVolume;
        public ObservableValue<float> SfxVolume => sfxVolume;
        public ObservableValue<string> Language => language;

        public void WriteTo(SettingsData data)
        {
            data.bgmVolume = bgmVolume.Value;
            data.sfxVolume = sfxVolume.Value;
            data.language = language.Value;
        }

        public void ReadFrom(SettingsData data)
        {
            bgmVolume.Value = data.bgmVolume;
            sfxVolume.Value = data.sfxVolume;
            language.Value = data.language;
        }
    }
}
