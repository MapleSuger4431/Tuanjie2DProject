namespace Game.Core
{
    /// <summary>
    /// 场景资产完整路径常量：Build Settings 登记与 SceneModule 切换共用。
    /// SceneModule 必须传完整资产路径（含扩展名，Build Settings 粒度即路径），改路径只改这里。
    /// </summary>
    public static class ScenePaths
    {
        /// <summary>启动场景：只放 GameBootstrap，加载完本地化资源后切主场景。</summary>
        public const string Bootstrap = "Assets/Scenes/Bootstrap.unity";

        /// <summary>主场景（主菜单）：开始游戏入口，右上角无设置、设置按钮按版式排在按钮列。</summary>
        public const string MainMenu = "Assets/Scenes/MainMenu.scene";

        /// <summary>选择关卡场景：大地图选关，可返回主场景，右上角设置按钮。</summary>
        public const string LevelSelect = "Assets/Scenes/LevelSelect.scene";
    }
}
