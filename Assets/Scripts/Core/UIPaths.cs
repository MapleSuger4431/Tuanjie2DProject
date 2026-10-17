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
    }
}
