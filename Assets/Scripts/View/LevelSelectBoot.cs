using Game.Core;
using Runestone.AesirModules;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 选择关卡场景引导脚本：场景内仅放置本脚本与主相机，
    /// UIRoot / UIModule / EventSystem 由 UIModule 首次调用自动创建（零预放置零配置）。
    /// UI 预制体按项目约定存放于 Assets/Resources/UIPrefabs/，经 path 由 ResourcesUILoader 加载。
    /// </summary>
    [AddComponentMenu("")]
    public class LevelSelectBoot : MonoBehaviour
    {
        void Start()
        {
            UIModule.Show<LevelSelectPanel>(UIPaths.LevelSelectPanel);
            Debug.Log("[LevelSelectBoot] 选关面板已显示（Resources path 加载）");
        }
    }
}
