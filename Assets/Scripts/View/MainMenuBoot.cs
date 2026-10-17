using Game.Core;
using Runestone.AesirModules;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 主菜单场景引导脚本：场景内仅放置本脚本与主相机，
    /// UIRoot / UIModule / EventSystem 由 UIModule 首次调用自动创建（零预放置零配置）。
    /// <para>
    /// UI 预制体按项目约定存放于 Assets/Resources/UIPrefabs/，经 path 由
    /// <see cref="ResourcesUILoader" />（裸 Resources.Load）加载并注册到注册表，无需序列化引用。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class MainMenuBoot : MonoBehaviour
    {
        void Start()
        {
            // 裸字符串位置参数命中 Show<T>(string path) 重载：按 Resources 路径加载并自动注册
            UIModule.Show<MainMenuPanel>(UIPaths.MainMenuPanel);
            Debug.Log("[MainMenuBoot] 主菜单面板已显示（Resources path 加载）");
        }
    }
}
