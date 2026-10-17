using Runestone.AesirModules;
using UnityEngine;

namespace Sunset.Test.UI
{
    /// <summary>
    /// 登录场景引导脚本（对齐 SampleUIBoot 的用法）：场景内仅放置本脚本与主相机，
    /// UIRoot / UIModule 未预放置，首次调用静态 API 时自动创建。
    /// 预制体注册后经 <see cref="UIModule" /> 驱动面板生命周期。
    /// </summary>
    [AddComponentMenu("")]
    public class LoginUIBoot : MonoBehaviour
    {
        [Tooltip("登录面板预制体")]
        [SerializeField]
        GameObject loginPanelPrefab;

        void Start()
        {
            // 面板预制体注册（实际项目可经 RegisterPrefab 或 Resources path 完成）
            UIModule.RegisterPrefab<LoginPanel>(loginPanelPrefab);

            UIModule.Show<LoginPanel>();
            Debug.Log("[LoginUIBoot] 登录面板已注册并显示");
        }
    }
}
