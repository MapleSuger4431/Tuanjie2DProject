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

        [Tooltip("成就图鉴按钮（打开成就图鉴面板）")]
        [SerializeField]
        Button achievementButton;

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

        [Header("标题图（按语言切换）")]

        [Tooltip("品牌区标题节点（由 MainMenuBuilder 接线）")]
        [SerializeField]
        Image titleImage;

        [Tooltip("中文标题图（pieces/title.png：边际棋盘）")]
        [SerializeField]
        Sprite titleSpriteZh;

        [Tooltip("英文标题图（pieces/title_en.png：Marginal Chessboard）")]
        [SerializeField]
        Sprite titleSpriteEn;

        [Tooltip("中文标题的最大显示框（等比缩放，不改锚点）")]
        [SerializeField]
        Vector2 titleBoxZh = new Vector2(589f, 137f);

        [Tooltip("英文标题的最大显示框（两行字，比中文略高以保持观感）")]
        [SerializeField]
        Vector2 titleBoxEn = new Vector2(589f, 190f);

        /// <summary>主菜单背景图（AI 抠图成品封面背景，生成预制体时接线）。</summary>
        public Image BackgroundImage => backgroundImage;

        protected override void OnInit()
        {
            // 预制体静态 Text 的默认字体对中文字形不可靠，运行时统一挂系统中文动态字体
            GameFonts.Apply(transform);

            // 语言已由 SettingsService 在启动时从存档应用到 Unity Localization（取文本前已选中正确语言）

            // 全游戏语言本地化：订阅切换事件（设置面板切语言时按钮/提示即时刷新），初始化即应用一次
            Localization.OnLanguageChanged += ApplyLocalization;
            ApplyLocalization();

            // 成就图鉴：打开成就图鉴面板（Popup 层），列出全部成就与解锁状态
            achievementButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 成就图鉴：打开成就图鉴面板");
                UIModule.Show<Game.View.AchievementPanel>(UIPaths.AchievementPanel);
            });

            // 开始游戏：带加载过场切到选择关卡场景（加载面板盖住画面 → 加载 → 淡出揭幕）
            newGameButton.onClick.AddListener(() =>
            {
                Debug.Log($"[MainMenuPanel] 开始游戏：带加载过场切换选择关卡场景 {ScenePaths.LevelSelect}");
                LoadingPanel.Begin(ScenePaths.LevelSelect);
            });

            // 说明内容：经 UIModule 打开操作说明弹窗（面板形态，Popup 层）；标题/正文由本地化接管
            helpButton.onClick.AddListener(() =>
            {
                Debug.Log("[MainMenuPanel] 说明内容：打开操作说明弹窗");
                UIModule.Show<MenuHelpPanel>(UIPaths.MenuHelpPanel);
            });

            // 设置：经 UIModule 打开设置面板（面板形态，Popup 层）；音量等设置由 SettingsService 统一下发
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

        /// <summary>按当前语言刷新主菜单全部文本与标题图（语言切换事件触发）。</summary>
        void ApplyLocalization()
        {
            SetLabel(newGameButton, Localization.Key.MenuNewGame);
            SetLabelText(achievementButton, Localization.Get("MenuAchievements"));
            SetLabel(helpButton, Localization.Key.MenuHelp);
            SetLabel(settingsButton, Localization.Key.MenuSettings);
            SetLabel(quitButton, Localization.Key.MenuQuit);

            ApplyTitleSprite();

            var hint = transform.Find("VisualPlaceholder/PlaceholderHint");
            if (hint != null)
            {
                hint.GetComponent<Text>().text = Localization.Get(Localization.Key.MenuPlaceholderHint);
            }
        }

        /// <summary>
        /// 标题图按语言切换：中文走「边际棋盘」切片，其余语言走「Marginal Chessboard」切片；
        /// 两张图长宽比不同，切换后按各自的最大显示框等比重算尺寸（居中锚点不变，不改版式位置）。
        /// </summary>
        void ApplyTitleSprite()
        {
            if (titleImage == null)
            {
                return;
            }

            var english = !Localization.IsChinese;
            var sprite = english ? titleSpriteEn : titleSpriteZh;
            if (sprite == null)
            {
                // 缺图时回落到另一语言，避免标题空白
                sprite = english ? titleSpriteZh : titleSpriteEn;
            }

            if (sprite == null || sprite == titleImage.sprite)
            {
                return;
            }

            titleImage.sprite = sprite;
            var box = english ? titleBoxEn : titleBoxZh;
            var scale = Mathf.Min(box.x / sprite.rect.width, box.y / sprite.rect.height);
            titleImage.rectTransform.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
        }

        /// <summary>按**键名字符串**设置按钮文本：数据驱动的文案（如成就）不占 Key 枚举。</summary>
        static void SetLabelText(Button button, string text)
        {
            var label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null)
            {
                label.text = text;
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
