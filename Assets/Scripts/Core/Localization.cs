using System;
using UnityEngine.Localization.Settings;

namespace Game.Core
{
    /// <summary>
    /// 轻量本地化门面（基于 Unity Localization 包 com.unity.localization）：
    /// 全游戏文本统一存于 GameText 字符串表（zh-CN / en，Localization Tables 窗口可编辑），
    /// <see cref="Key" /> 枚举提供编译期键名；<see cref="Get" /> 同步取值（表未加载时短暂等待）。
    /// <para>
    /// 语言状态归 <see cref="Game.Model.SettingsModel" />：改值后由 <c>SettingsService</c>
    /// 切换包的 SelectedLocale 并广播 <see cref="OnLanguageChanged" />，已打开的界面订阅后整体刷新。
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

            // ---- 选关场景 ----
            LevelSelectTitle,
            LevelName,
            LevelSelectBack,
            LevelLocked,
            LevelNoStar,
            LevelBestRecord,
            LevelNoRecord,
            LevelDetailEnter,
            LevelDetailCancel,

            /// <summary>关卡详情「最短用时」（{0} = mm:ss.d）。</summary>
            LevelBestTime,

            /// <summary>关卡详情「上次用时」（{0} = mm:ss.d）。</summary>
            LevelLastTime,

            // ---- 关卡场景内 ----
            /// <summary>关卡内「返回选关」按钮。</summary>
            LevelBackToSelect,

            /// <summary>关卡内「重新开始本关」按钮。</summary>
            LevelRestart,

            /// <summary>关卡内「铺路完成」按钮：停表并锁定放置，进入「选偏好 → Play」的待运行阶段。</summary>
            LevelFinishBuilding,

            /// <summary>关卡内操作提示行：放置 / 旋转 / 缩放 / 平移。</summary>
            LevelBoardHint,

            // ---- 关卡结算 ----
            /// <summary>通关成功结算面板标题。</summary>
            LevelClearTitle,

            /// <summary>通关失败结算面板标题。</summary>
            LevelFailTitle,

            /// <summary>结算面板「重玩此关」按钮。</summary>
            LevelResultRetry,

            /// <summary>结算面板「回到选关界面」按钮。</summary>
            LevelResultBackToSelect,

            /// <summary>通关成功的用时行（{0} = mm:ss.d）；只有通关页显示，失败页不记时间。</summary>
            LevelClearTime,

            /// <summary>通关成功且刷新最短纪录时的用时行（{0} = mm:ss.d）。</summary>
            LevelClearTimeNewRecord,

            /// <summary>结算面板成绩行「步数」（{0} = 步数）。</summary>
            LevelResultSteps,

            /// <summary>结算面板成绩行「已用方块」（{0} = 已用，{1} = 上限）。</summary>
            LevelResultPieces,
        }

        /// <summary>语言切换事件：GameSettings.SetLanguage 保存后触发，已打开的界面应整体刷新文本。</summary>
        public static event Action OnLanguageChanged;

        /// <summary>字符串表集名（Assets/Localization/Tables/GameText）。</summary>
        const string TableName = "GameText";

        /// <summary>
        /// 当前语言代码（如 zh-CN / en），取自 Unity Localization 的 SelectedLocale；未初始化时为空串。
        /// 供需要按语言切换的美术资源（如标题图）判定。
        /// </summary>
        public static string LanguageCode =>
            LocalizationSettings.SelectedLocale != null ? LocalizationSettings.SelectedLocale.Identifier.Code : string.Empty;

        /// <summary>当前语言是否为中文（zh-*）；非中文一律走英文资源。</summary>
        public static bool IsChinese => LanguageCode.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        /// <summary>按当前语言取文本（Unity Localization 字符串表；表未加载时 WaitForCompletion 短暂等待）。</summary>
        public static string Get(Key key)
        {
            var op = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TableName, key.ToString());
            return op.WaitForCompletion();
        }

        /// <summary>
        /// 取带占位符的文本：表值按 Smart Format 写（如「第 {0} 关」），args 依次填位。
        /// 表未加载时 WaitForCompletion 短暂等待。
        /// </summary>
        public static string Get(Key key, params object[] args)
        {
            var op = args == null || args.Length == 0
                ? LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TableName, key.ToString())
                : LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TableName, key.ToString(), args);
            return op.WaitForCompletion();
        }

        /// <summary>由 SettingsService 在语言切换后调用，广播语言切换。</summary>
        public static void RaiseLanguageChanged()
        {
            OnLanguageChanged?.Invoke();
        }
    }
}
