namespace Game.Core
{
    /// <summary>
    /// 场景资产完整路径常量：Build Settings 登记与 SceneModule 切换共用。
    /// SceneModule 必须传完整资产路径（含扩展名，Build Settings 粒度即路径），改路径只改这里。
    /// </summary>
    public static class ScenePaths
    {
        /// <summary>主菜单场景（已建成的主菜单 UI 资产，后续按需经 SceneModule 接入）。</summary>
        public const string MainMenu = "Assets/Scenes/MainMenu.scene";

        /// <summary>主场景（Bootstrap 直接切入的游戏主场景）。</summary>
        public const string MainScene = "Assets/Scenes/MainScene.scene";
    }
}
