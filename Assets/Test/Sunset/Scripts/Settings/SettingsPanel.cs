using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.UI;

namespace Sunset.Test.UI
{
    /// <summary>
    /// 设置面板（设置按钮单独测试）：分辨率档位切换、音效/音乐音量、语言（中文 / English）。
    /// <para>
    /// 挂 UIRoot 的 Popup 层；蒙版点击 / × 按钮双通道关闭。
    /// 设置值经 <see cref="GameSettings" />（PlayerPrefs）持久化、即时生效；
    /// 语言切换会即时刷新本面板全部标签（中文 ↔ English）作为功能演示。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class SettingsPanel : AesirBasePanel
    {
        [Tooltip("标题文本")]
        [SerializeField]
        Text titleText;

        [Tooltip("分辨率行标签")]
        [SerializeField]
        Text resolutionRowLabel;

        [Tooltip("分辨率当前值文本")]
        [SerializeField]
        Text resolutionValueText;

        [Tooltip("音效行标签")]
        [SerializeField]
        Text sfxLabel;

        [Tooltip("音乐行标签")]
        [SerializeField]
        Text musicLabel;

        [Tooltip("语言行标签")]
        [SerializeField]
        Text languageLabel;

        [Tooltip("分辨率上一档按钮")]
        [SerializeField]
        Button resolutionPrevButton;

        [Tooltip("分辨率下一档按钮")]
        [SerializeField]
        Button resolutionNextButton;

        [Tooltip("音效音量滑条")]
        [SerializeField]
        Slider sfxSlider;

        [Tooltip("音乐音量滑条")]
        [SerializeField]
        Slider musicSlider;

        [Tooltip("中文按钮")]
        [SerializeField]
        Button chineseButton;

        [Tooltip("English 按钮")]
        [SerializeField]
        Button englishButton;

        [Tooltip("关闭按钮")]
        [SerializeField]
        Button closeButton;

        Image chineseButtonImage;
        Image englishButtonImage;

        static readonly Color NormalButtonColor = Color.white;
        static readonly Color SelectedButtonColor = new Color(0.60f, 0.80f, 1.00f, 1f);

        protected override void OnInit()
        {
            // 静态 Text 的默认字体对中文字形不可靠，运行时统一挂系统中文动态字体
            SandboxUIFonts.Apply(transform);

            // 每次打开都把已保存的音效音量应用到 AudioListener（保证持久化值即时生效）
            GameSettings.ApplySfxVolume();

            closeButton.onClick.AddListener(HideSelf);

            // 蒙版点击关闭：面板没有窗口形态的 OnMaskClicked 回调，由 Mask 子物体 Button 直接承接
            var maskTransform = transform.Find("Mask");
            if (maskTransform != null)
            {
                var maskButton = maskTransform.GetComponent<Button>();
                if (maskButton != null)
                {
                    maskButton.onClick.AddListener(HideSelf);
                }
            }

            // 分辨率档位：< / > 循环切换，即时应用并保存，并刷新数值文本
            resolutionPrevButton.onClick.AddListener(() =>
            {
                GameSettings.SetResolution(GameSettings.ResolutionIndex - 1);
                RefreshVisuals();
            });

            resolutionNextButton.onClick.AddListener(() =>
            {
                GameSettings.SetResolution(GameSettings.ResolutionIndex + 1);
                RefreshVisuals();
            });

            // 音量：滑条值直接写入 GameSettings（音效占位应用 AudioListener，音乐先持久化）
            sfxSlider.onValueChanged.AddListener(GameSettings.SetSfxVolume);
            musicSlider.onValueChanged.AddListener(GameSettings.SetMusicVolume);

            // 语言：点选后保存并刷新全部标签
            chineseButtonImage = chineseButton.GetComponent<Image>();
            englishButtonImage = englishButton.GetComponent<Image>();

            chineseButton.onClick.AddListener(() =>
            {
                GameSettings.SetLanguage(GameSettings.Language.Chinese);
                RefreshVisuals();
            });

            englishButton.onClick.AddListener(() =>
            {
                GameSettings.SetLanguage(GameSettings.Language.English);
                RefreshVisuals();
            });
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);
            RefreshVisuals();
        }

        /// <summary>把 GameSettings 当前值刷新到 UI（滑条静默同步，不触发 onValueChanged 回写）。</summary>
        void RefreshVisuals()
        {
            var r = GameSettings.Resolutions[GameSettings.ResolutionIndex];
            resolutionValueText.text = r.x + " × " + r.y;

            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);

            bool zh = GameSettings.CurrentLanguage == GameSettings.Language.Chinese;
            titleText.text = zh ? "设置" : "Settings";
            resolutionRowLabel.text = zh ? "分辨率" : "Resolution";
            sfxLabel.text = zh ? "音效" : "SFX";
            musicLabel.text = zh ? "音乐" : "Music";
            languageLabel.text = zh ? "语言" : "Language";

            // 语言选中态高亮（按钮 Transition 为 None，由 Image.color 直接呈现选中色）
            if (chineseButtonImage != null)
            {
                chineseButtonImage.color = zh ? SelectedButtonColor : NormalButtonColor;
            }

            if (englishButtonImage != null)
            {
                englishButtonImage.color = zh ? NormalButtonColor : SelectedButtonColor;
            }
        }
    }
}
