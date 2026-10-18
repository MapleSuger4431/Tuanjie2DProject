namespace Game.Core
{
    /// <summary>
    /// UI 资源路径常量（相对任意 Resources 目录，不含扩展名）。
    /// UIModule.Show/Open 的 path 参数按此经 ResourcesUILoader 加载并自动注册，改路径只改这里。
    /// </summary>
    public static class UIPaths
    {
        /// <summary>主菜单面板：Resources/UIPrefabs/MainMenuPanel。</summary>
        public const string MainMenuPanel = "UIPrefabs/MainMenuPanel";

        /// <summary>操作说明弹窗面板：Resources/UIPrefabs/MenuHelpPanel。</summary>
        public const string MenuHelpPanel = "UIPrefabs/MenuHelpPanel";

        /// <summary>设置面板：Resources/UIPrefabs/MenuSettingsPanel。</summary>
        public const string MenuSettingsPanel = "UIPrefabs/MenuSettingsPanel";

        /// <summary>选关面板：Resources/UIPrefabs/LevelSelectPanel。</summary>
        public const string LevelSelectPanel = "UIPrefabs/LevelSelectPanel";

        /// <summary>关卡详情弹窗（面板形态，Popup 层）：Resources/UIPrefabs/LevelDetailPanel。</summary>
        public const string LevelDetailPanel = "UIPrefabs/LevelDetailPanel";

        /// <summary>棋盘关卡运行时面板：Resources/UIPrefabs/BoardRuntimePanel（由关卡场景 BoardRuntimeDriver 拉起）。</summary>
        public const string BoardRuntimePanel = "UIPrefabs/BoardRuntimePanel";

        /// <summary>通关成功结算面板：Resources/UIPrefabs/LevelClearPanel。</summary>
        public const string LevelClearPanel = "UIPrefabs/LevelClearPanel";

        /// <summary>通关失败结算面板：Resources/UIPrefabs/LevelFailPanel。</summary>
        public const string LevelFailPanel = "UIPrefabs/LevelFailPanel";

        /// <summary>关卡内右下角成就 / 消息小窗（Top 层，常驻）：Resources/UIPrefabs/LevelTipPanel。</summary>
        public const string LevelTipPanel = "UIPrefabs/LevelTipPanel";
    }
}
