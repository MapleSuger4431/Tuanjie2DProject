using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.UI;

namespace Sunset.Test.UI
{
    /// <summary>
    /// 登录面板：主菜单入口。四个按钮：新游戏 / 继续游戏 / 说明内容 / 退出。
    /// <para>
    /// 直接使用 Aesir UI 框架：继承 <see cref="AesirBasePanel" />，生命周期由 <see cref="UIModule" /> 驱动
    /// （预制体经 <c>UIModule.RegisterPrefab&lt;LoginPanel&gt;()</c> 注册，<c>UIModule.Show&lt;LoginPanel&gt;()</c> 显示，
    /// 挂载于 UIRoot 的 Normal 层 Canvas 下）。层级内的 Background 节点与 <see cref="backgroundImage" />
    /// 字段是背景占位：当前为纯色 Image，后续把背景图直接挂到 Background 的 Image.sprite（或换为 RawImage）即可。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class LoginPanel : AesirBasePanel
    {
        [Tooltip("背景占位图：后续在此挂背景（替换 sprite 或换 RawImage）")]
        [SerializeField]
        Image backgroundImage;

        [Tooltip("新游戏按钮")]
        [SerializeField]
        Button newGameButton;

        [Tooltip("继续游戏按钮")]
        [SerializeField]
        Button continueGameButton;

        [Tooltip("说明内容按钮")]
        [SerializeField]
        Button helpButton;

        [Tooltip("退出按钮")]
        [SerializeField]
        Button quitButton;

        /// <summary>背景占位图，未配置 sprite 时为纯色占位。</summary>
        public Image BackgroundImage => backgroundImage;

        protected override void OnInit()
        {
            // 预制体静态 Text 的默认字体对中文字形不可靠，运行时统一挂系统中文动态字体
            SandboxUIFonts.Apply(transform);

            newGameButton.onClick.AddListener(() =>
            {
                Debug.Log("[LoginPanel] 新游戏：开始新对局（占位逻辑，后续接入关卡生成）");
            });

            continueGameButton.onClick.AddListener(() =>
            {
                Debug.Log("[LoginPanel] 继续游戏：读取存档（占位逻辑，后续接入存档系统）");
            });

            helpButton.onClick.AddListener(() =>
            {
                Debug.Log("[LoginPanel] 说明内容：打开说明面板（占位逻辑，后续接入说明界面）");
            });

            quitButton.onClick.AddListener(() =>
            {
                Debug.Log("[LoginPanel] 退出游戏");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
    }
}
