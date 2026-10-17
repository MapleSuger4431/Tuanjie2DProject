using Runestone.AesirModules;
using UnityEngine;

namespace Sunset.Test.UI
{
    /// <summary>
    /// 登录场景引导脚本（Resources 资产管理版）：场景内仅放置本脚本与主相机，
    /// UIRoot / UIModule 未预放置，首次调用静态 API 时自动创建。
    /// <para>
    /// UI 预制体按项目约定存放于 Assets/Resources/UIPrefabs/，经 path 由
    /// <see cref="ResourcesUILoader" />（裸 Resources.Load）自动加载并注册到注册表，
    /// 无需序列化引用——注册表未命中时按 path 加载后自动注册。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class LoginUIBoot : MonoBehaviour
    {
        void Start()
        {
            // path 加载：Resources/UIPrefabs/LoginPanel 自动加载注册
            UIModule.Show<LoginPanel>("UIPrefabs/LoginPanel");
            Debug.Log("[LoginUIBoot] 登录面板已显示（Resources path 加载）");
        }
    }
}
