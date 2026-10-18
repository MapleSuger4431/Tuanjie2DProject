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
        [Tooltip("主菜单背景音乐 — 经 AudioModule 的 BGM 通道循环播放，音量随设置面板的音乐滑条")]
        [SerializeField]
        AudioClip bgmClip;

        [Tooltip("背景音乐淡入时长（秒）")]
        [SerializeField]
        float bgmFadeSeconds = 1f;

        void Start()
        {
            // 裸字符串位置参数命中 Show<T>(string path) 重载：按 Resources 路径加载并自动注册
            UIModule.Show<MainMenuPanel>(UIPaths.MainMenuPanel);
            Debug.Log("[MainMenuBoot] 主菜单面板已显示（Resources path 加载）");

            // 右下角消息小窗随主菜单常驻：成就在主菜单解锁（或未来其它来源）时也弹得出来
            UIModule.Show<Game.Board.LevelTipPanel>(UIPaths.LevelTipPanel);

            // BGM 走音频模块的 BGM 通道：音量由 GameSettings（启动已下发）决定，同曲在播幂等返回
            // 未配置片段时静默跳过：PlayBgm 对空 clip 会 LogError
            if (bgmClip != null)
            {
                AudioModule.PlayBgm(bgmClip, bgmFadeSeconds);
            }
        }
    }
}
