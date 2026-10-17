using UnityEngine;

namespace Sunset.Test.UI
{
    /// <summary>
    /// 全局设置状态（测试版）：分辨率档位、音效/音乐音量、语言（中文 / English）。
    /// 经 PlayerPrefs 持久化，跨 Play 会话保留。
    /// <para>
    /// 音效音量暂以 <c>AudioListener.volume</c> 全局占位应用（工程内尚无音效/音乐分轨）；
    /// 音乐音量先持久化，待接入 Aesir AudioModule 后再分轨应用。
    /// </para>
    /// </summary>
    public static class GameSettings
    {
        public enum Language
        {
            Chinese = 0,
            English = 1,
        }

        /// <summary>可选分辨率档位（宽 × 高）。</summary>
        public static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
        };

        const string KeyResolution = "TestSettings.ResolutionIndex";
        const string KeySfxVolume = "TestSettings.SfxVolume";
        const string KeyMusicVolume = "TestSettings.MusicVolume";
        const string KeyLanguage = "TestSettings.Language";

        public static int ResolutionIndex { get; private set; } = LoadResolutionIndex();
        public static float SfxVolume { get; private set; } = PlayerPrefs.GetFloat(KeySfxVolume, 1f);
        public static float MusicVolume { get; private set; } = PlayerPrefs.GetFloat(KeyMusicVolume, 1f);
        public static Language CurrentLanguage { get; private set; } = LoadLanguage();

        static int LoadResolutionIndex()
        {
            var index = PlayerPrefs.GetInt(KeyResolution, IndexOfClosestToCurrent());
            return Mathf.Clamp(index, 0, Resolutions.Length - 1);
        }

        /// <summary>首次使用时默认选中与当前屏幕宽度最接近的档位。</summary>
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

        static Language LoadLanguage()
        {
            var raw = PlayerPrefs.GetInt(KeyLanguage, (int)Language.Chinese);
            return (Language)Mathf.Clamp(raw, 0, 1);
        }

        /// <summary>设置分辨率档位并立即应用（编辑器内改变 Game View 尺寸，打包后全屏切换）。</summary>
        public static void SetResolution(int index)
        {
            index = Mathf.Clamp(index, 0, Resolutions.Length - 1);
            ResolutionIndex = index;
            PlayerPrefs.SetInt(KeyResolution, index);
            PlayerPrefs.Save();

            var r = Resolutions[index];
            Screen.SetResolution(r.x, r.y, false);
        }

        /// <summary>设置音效音量：占位应用到全局监听音量（待 AudioModule 接入后改分轨）。</summary>
        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeySfxVolume, SfxVolume);
            PlayerPrefs.Save();

            AudioListener.volume = SfxVolume;
        }

        /// <summary>把已保存的音效音量应用到 AudioListener（进游戏或打开设置面板时调用，确保持久化值即时生效）。</summary>
        public static void ApplySfxVolume()
        {
            AudioListener.volume = SfxVolume;
        }

        /// <summary>设置音乐音量：先持久化（工程内尚无音乐系统，接入 AudioModule 后在此应用）。</summary>
        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(KeyMusicVolume, MusicVolume);
            PlayerPrefs.Save();
        }

        /// <summary>设置界面语言（中文 / English）。</summary>
        public static void SetLanguage(Language language)
        {
            CurrentLanguage = language;
            PlayerPrefs.SetInt(KeyLanguage, (int)language);
            PlayerPrefs.Save();
        }
    }
}
