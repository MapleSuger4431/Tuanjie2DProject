using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Core
{
    /// <summary>
    /// 启动场景唯一引导脚本：初始化 Context，并显示首个 UI。
    /// 场景内除本脚本（与 AudioListener）外不放任何东西。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        void Start()
        {
            // 触发 Context 初始化（执行 Configure，注册全部 Model / Service）
            _ = GameContext.Instance;

            // 首个 UI——主菜单面板写好后启用（路径常量集中在 UIPaths；首次 Show 会自动创建 UIRoot）
            // UIModule.Show<MenuPanel>(path: UIPaths.MenuPanel);
        }
    }
}
