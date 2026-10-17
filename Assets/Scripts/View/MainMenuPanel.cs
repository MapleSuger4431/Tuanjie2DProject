using Game.Core;
using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 主菜单面板：新游戏 / 继续游戏 / 说明内容 / 设置 / 退出。
    /// <para>
    /// 版式与测试沙盒 LoginPanel 一致（按钮列于右侧，左侧主视觉占位）。
    /// 继承 <see cref="AesirBasePanel" />（RectTransform 根、无 Canvas），挂载于 UIRoot 的 Normal 层。
    /// 层级内的 Background 节点与 <see cref="backgroundImage" /> 字段是背景占位：
    /// 当前为纯色 Image，后续把背景图直接挂到 Background 的 Image.sprite（或换为 RawImage）即可。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class MainMenuPanel : AesirBasePanel
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

        [Tooltip("设置按钮")]
        [SerializeField]
        Button settingsButton;

        [Tooltip("退出按钮")]
        [SerializeField]
        Button quitButton;

        /// <summary>背景占位图，未配置 sprite 时为纯色占位。</summary>
        public Image BackgroundImage => backgroundImage;

        protected override void OnInit()
        {
            // 预制体静态 Text 的默认字体对中文字形不可靠，运行时统一挂系统中文动态字体
            GameFonts.Apply(transform);

            newGameButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 新游戏：开始新对局（占位逻辑，后续接入关卡生成）");
            });

            continueGameButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 继续游戏：读取存档（占位逻辑，后续接入存档系统）");
            });

            // 说明内容：经 UIModule 打开操作说明弹窗（面板形态，Popup 层）。
            // (object) 强转避免字符串字面量命中 Show<T>(string path) 重载——否则标题文本会被当作资源路径
            helpButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 说明内容：打开操作说明弹窗");
                UIModule.Show<MenuHelpPanel>((object)"操作说明", UIPaths.MenuHelpPanel);
            });

            // 设置：经 UIModule 打开设置面板（面板形态，Popup 层）；进面板即应用持久化的音效音量
            GameSettings.ApplySfxVolume();
            settingsButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 设置：打开设置面板");
                UIModule.Show<MenuSettingsPanel>(UIPaths.MenuSettingsPanel);
            });

            quitButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 退出游戏");
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
    }
}
