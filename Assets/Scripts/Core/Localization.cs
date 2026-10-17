using System;
using UnityEngine.Localization.Settings;

namespace Game.Core
{
    /// <summary>
    /// 轻量本地化门面（基于 Unity Localization 包 com.unity.localization）：
    /// 全游戏文本统一存于 GameText 字符串表（zh-CN / en，Localization Tables 窗口可编辑），
    /// <see cref="Key" /> 枚举提供编译期键名；<see cref="Get" /> 同步取值（表未加载时短暂等待）。
    /// <para>
    /// 语言状态与持久化仍归 <see cref="GameSettings" />：SetLanguage 保存后切换包的 SelectedLocale
    /// 并广播 <see cref="OnLanguageChanged" />，已打开的界面订阅后整体刷新。
    /// </para>
    /// </summary>
    public static class Localization
    {
        /// <summary>全游戏本地化文本键（对应 GameText 表条目）。</summary>
        public enum Key
        {
            MenuNewGame,
            MenuContinue,
            MenuHelp,
            MenuSettings,
            MenuQuit,
            MenuPlaceholderHint,
            HelpTitle,
            HelpBody,
            SettingsTitle,
            SettingsResolution,
            SettingsSfx,
            SettingsMusic,
            SettingsLanguage,
        }

        /// <summary>语言切换事件：GameSettings.SetLanguage 保存后触发，已打开的界面应整体刷新文本。</summary>
        public static event Action OnLanguageChanged;

        /// <summary>字符串表集名（Assets/Localization/Tables/GameText）。</summary>
        const string TableName = "GameText";

        /// <summary>按当前语言取文本（Unity Localization 字符串表；表未加载时 WaitForCompletion 短暂等待）。</summary>
        public static string Get(Key key)
        {
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TableName, key.ToString());
            return op.WaitForCompletion();
        }

        /// <summary>由 GameSettings.SetLanguage 在保存后调用，广播语言切换。</summary>
        public static void RaiseLanguageChanged()
        {
            OnLanguageChanged?.Invoke();
        }
    }
}
