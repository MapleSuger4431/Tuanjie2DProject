using Game.Core;
using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 主菜单面板：开始游戏 / 玩法说明 / 设置 / 退出游戏。
    /// <para>
    /// 版式按封面设计图（按钮列于右下，左侧为棋盘场景主视觉）。
    /// 按钮文本与主视觉提示经本地化（<see cref="Game.Core.Localization" />）随语言切换即时刷新。
    /// 继承 <see cref="AesirBasePanel" />（RectTransform 根、无 Canvas），挂载于 UIRoot 的 Normal 层。
    /// 层级内的 Background 节点由 MainMenuBuilder 挂 AI 抠图成品封面背景（封面背景.png），
    /// 品牌区（徽章/标题/EDGE GRID/分隔线/标语）以切片 UI 分层叠放其上。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class MainMenuPanel : AesirBasePanel
    {
        [Tooltip("主菜单背景图（AI 抠图成品，由 MainMenuBuilder 接线）")]
        [SerializeField]
        Image backgroundImage;

        [Tooltip("新游戏按钮")]
        [SerializeField]
        Button newGameButton;

        [Tooltip("说明内容按钮")]
        [SerializeField]
        Button helpButton;

        [Tooltip("设置按钮")]
        [SerializeField]
        Button settingsButton;

        [Tooltip("退出按钮")]
        [SerializeField]
        Button quitButton;

        /// <summary>主菜单背景图（AI 抠图成品封面背景，生成预制体时接线）。</summary>
        public Image BackgroundImage => backgroundImage;

        protected override void OnInit()
        {
            // 预制体静态 Text 的默认字体对中文字形不可靠，运行时统一挂系统中文动态字体
            GameFonts.Apply(transform);

            // 把持久化的语言应用到 Unity Localization（必须在取文本前选中正确语言）
            GameSettings.ApplyLoadedLocale();

            // 全游戏语言本地化：订阅切换事件（设置面板切语言时按钮/提示即时刷新），初始化即应用一次
            Localization.OnLanguageChanged += ApplyLocalization;
            ApplyLocalization();

            newGameButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 新游戏：开始新对局（占位逻辑，后续接入关卡生成）");
            });

            // 说明内容：经 UIModule 打开操作说明弹窗（面板形态，Popup 层）；标题/正文由本地化接管
            helpButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 说明内容：打开操作说明弹窗");
                UIModule.Show<MenuHelpPanel>(UIPaths.MenuHelpPanel);
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

        protected override void OnDestroy()
        {
            Localization.OnLanguageChanged -= ApplyLocalization;
            base.OnDestroy();
        }

        /// <summary>按当前语言刷新主菜单全部文本（语言切换事件触发）。</summary>
        void ApplyLocalization()
        {
            SetLabel(newGameButton, Localization.Key.MenuNewGame);
            SetLabel(helpButton, Localization.Key.MenuHelp);
            SetLabel(settingsButton, Localization.Key.MenuSettings);
            SetLabel(quitButton, Localization.Key.MenuQuit);

            var hint = transform.Find("VisualPlaceholder/PlaceholderHint");
            if (hint != null)
            {
                hint.GetComponent<Text>().text = Localization.Get(Localization.Key.MenuPlaceholderHint);
            }
        }

        static void SetLabel(Button button, Localization.Key key)
        {
            var label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null)
            {
                label.text = Localization.Get(key);
            }
        }
    }
}
