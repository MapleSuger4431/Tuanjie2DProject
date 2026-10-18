using System;
using Game.Save;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Model
{
    /// <summary>
    /// 全局设置 Model：音效/音乐音量、语言、分辨率档位、全屏。
    /// <para>
    /// 设置值不再走 PlayerPrefs，统一由存档系统持久化（<see cref="SettingsData" />）：
    /// 本 Model 只承载运行时状态，改动经 ObservableValue 广播——
    /// <c>SettingsService</c> 负责下发到 AudioModule / Localization / Screen，<c>SaveService</c> 负责落盘。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class SettingsModel : AbstractModel, ISavableModel<SettingsData>
    {
        /// <summary>中文语言代码（与 Unity Localization 的 Locale Identifier 一致）。</summary>
        public const string LanguageChinese = "zh-CN";

        /// <summary>英文语言代码。</summary>
        public const string LanguageEnglish = "en";

        /// <summary>可选分辨率档位（宽 × 高）。</summary>
        public static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160),
        };

        /// <summary>首次使用（无存档）时默认选中与当前屏幕宽度最接近的档位。</summary>
        static readonly int DefaultResolutionIndex = IndexOfClosestToCurrent();

        [SerializeField] ObservableValue<float> bgmVolume = new ObservableValue<float>(1f);
        [SerializeField] ObservableValue<float> sfxVolume = new ObservableValue<float>(1f);
        [SerializeField] ObservableValue<string> language = new ObservableValue<string>(LanguageChinese);
        [SerializeField] ObservableValue<int> resolutionIndex = new ObservableValue<int>(DefaultResolutionIndex);
        [SerializeField] ObservableValue<bool> fullscreen = new ObservableValue<bool>(false);

        public ObservableValue<float> BgmVolume => bgmVolume;
        public ObservableValue<float> SfxVolume => sfxVolume;
        public ObservableValue<string> Language => language;
        public ObservableValue<int> ResolutionIndex => resolutionIndex;
        public ObservableValue<bool> Fullscreen => fullscreen;

        public void WriteTo(SettingsData data)
        {
            data.bgmVolume = bgmVolume.Value;
            data.sfxVolume = sfxVolume.Value;
            data.language = language.Value;
            data.resolutionIndex = resolutionIndex.Value;
            data.fullscreen = fullscreen.Value;
        }

        public void ReadFrom(SettingsData data)
        {
            bgmVolume.Value = data.bgmVolume;
            sfxVolume.Value = data.sfxVolume;
            language.Value = string.IsNullOrEmpty(data.language) ? LanguageChinese : data.language;
            resolutionIndex.Value = Mathf.Clamp(data.resolutionIndex, 0, Resolutions.Length - 1);
            fullscreen.Value = data.fullscreen;
        }

        static int IndexOfClosestToCurrent()
        {
            var best = 0;
            var bestDiff = float.MaxValue;
            for (var i = 0; i < Resolutions.Length; i++)
            {
                var diff = Mathf.Abs(Resolutions[i].x - Screen.width);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = i;
                }
            }

            return best;
        }
    }
}
