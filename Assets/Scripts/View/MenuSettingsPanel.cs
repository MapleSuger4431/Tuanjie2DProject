using Game.Core;
using Game.Model;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.UI;

namespace Game.View
{
    /// <summary>
    /// 设置面板（主菜单入口）：分辨率档位切换、全屏、音效/音乐音量、语言（中文 / English）。
    /// <para>
    /// 面板形态：RectTransform 根、无 Canvas，挂载于 UIRoot 的 Popup 层；
    /// 蒙版点击 / × 按钮双通道关闭。
    /// 经 <c>UIModule.Show&lt;MenuSettingsPanel&gt;(UIPaths.MenuSettingsPanel)</c> 打开。
    /// </para>
    /// <para>
    /// 面板只读写 <see cref="SettingsModel" />，不碰存储也不碰音频模块：
    /// 下发到 AudioModule / Localization / Screen 由 <c>SettingsService</c> 负责，
    /// 落盘由 <c>SaveService</c> 负责（设置变更即存档）。语言切换经本地化广播刷新本面板与全游戏文本。
    /// </para>
    /// </summary>
    [AddComponentMenu("")]
    public class MenuSettingsPanel : AesirBasePanelViewController<GameContext>
    {
        [Tooltip("标题文本")]
        [SerializeField]
        Text titleText;

        [Tooltip("标题美术字（中文模式显示，英文模式回落标题文本）")]
        [SerializeField]
        Image titleImage;

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

        [Tooltip("分辨率行标签美术字（中文模式显示）")]
        [SerializeField]
        Image resolutionRowLabelImage;

        [Tooltip("音效行标签美术字（中文模式显示）")]
        [SerializeField]
        Image sfxLabelImage;

        [Tooltip("音乐行标签美术字（中文模式显示）")]
        [SerializeField]
        Image musicLabelImage;

        [Tooltip("语言行标签美术字（中文模式显示）")]
        [SerializeField]
        Image languageLabelImage;

        [Tooltip("分辨率上一档按钮")]
        [SerializeField]
        Button resolutionPrevButton;

        [Tooltip("分辨率下一档按钮")]
        [SerializeField]
        Button resolutionNextButton;

        [Tooltip("全屏切换按钮")]
        [SerializeField]
        Button fullscreenButton;

        [Tooltip("全屏状态文本（全屏：开 / 关）")]
        [SerializeField]
        Text fullscreenText;

        [Tooltip("全屏按钮选中态胶囊底图（复用语言行同款切片）")]
        [SerializeField]
        Sprite fullscreenSelectedSprite;

        [Tooltip("全屏按钮未选中态胶囊底图")]
        [SerializeField]
        Sprite fullscreenUnselectedSprite;

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

        [Tooltip("语言按钮选中态胶囊底图（切片自带发光态）")]
        [SerializeField]
        Sprite languageSelectedSprite;

        [Tooltip("语言按钮未选中态胶囊底图")]
        [SerializeField]
        Sprite languageUnselectedSprite;

        [Tooltip("UI 点击音效 — 经 AudioModule 的 SFX 通道播放，随设置面板的音效滑条即时变化")]
        [SerializeField]
        AudioClip uiClickSfx;

        [Tooltip("UI 点击音效的局部音量（与 SFX 通道音量相乘生效）")]
        [SerializeField, Range(0f, 1f)]
        float uiClickVolume = 1f;

        SettingsModel _settings;

        Image chineseButtonImage;
        Image englishButtonImage;
        Image fullscreenButtonImage;

        /// <summary>连续拖动滑条时的点击音节流间隔（秒），避免每帧一响糊成一片。</summary>
        const float ClickThrottleSeconds = 0.08f;

        float lastClickTime = -1f;

        protected override void OnInit()
        {
            // 静态 Text 的默认字体对中文字形不可靠，运行时统一挂系统中文动态字体
            GameFonts.Apply(transform);

            // 生命周期：克隆 → OnInit → Start —— Model 缓存与按钮绑定必须在 OnInit 完成
            _settings = this.GetModel<SettingsModel>();

            closeButton.onClick.AddListener(PlayClick);
            closeButton.onClick.AddListener(HideSelf);

            // 蒙版点击关闭：面板没有 AesirBaseWindow.OnMaskClicked 回调，由 Mask 子物体的 Button 直接承接
            var maskTransform = transform.Find("Mask");
            if (maskTransform != null)
            {
                var maskButton = maskTransform.GetComponent<Button>();
                if (maskButton != null)
                {
                    maskButton.onClick.AddListener(HideSelf);
                }
            }

            // 分辨率档位：< / > 循环切换（改 Model → SettingsService 应用到屏幕 → SaveService 落盘）
            resolutionPrevButton.onClick.AddListener(() =>
            {
                PlayClick();
                ShiftResolution(-1);
            });

            resolutionNextButton.onClick.AddListener(() =>
            {
                PlayClick();
                ShiftResolution(1);
            });

            // 全屏：点击即写 Model（SettingsService 下发 Screen.fullScreen 并落盘）；
            // 全屏没有语言那样的切换广播链，刷新必须在这里自己做
            fullscreenButtonImage = fullscreenButton.GetComponent<Image>();
            fullscreenButton.onClick.AddListener(() =>
            {
                PlayClick();
                _settings.Fullscreen.Value = !_settings.Fullscreen.Value;
                RefreshVisuals();
            });

            // 音量：滑条值只写 Model，下发 AudioModule 与落盘都由 Service 订阅完成。
            // 拖动时按节流播放点击音——既是操作反馈，也能直接听到当前音量大小
            sfxSlider.onValueChanged.AddListener(value =>
            {
                _settings.SfxVolume.Value = value;
                PlayClick();
            });

            musicSlider.onValueChanged.AddListener(value =>
            {
                _settings.BgmVolume.Value = value;
                PlayClick();
            });

            // 语言：点选即写 Model（SettingsService 切 Locale 并广播，本面板与全游戏文本即时刷新）
            chineseButtonImage = chineseButton.GetComponent<Image>();
            englishButtonImage = englishButton.GetComponent<Image>();

            Localization.OnLanguageChanged += RefreshVisuals;

            chineseButton.onClick.AddListener(() =>
            {
                PlayClick();
                _settings.Language.Value = SettingsModel.LanguageChinese;
            });

            englishButton.onClick.AddListener(() =>
            {
                PlayClick();
                _settings.Language.Value = SettingsModel.LanguageEnglish;
            });
        }

        protected override void OnDestroy()
        {
            Localization.OnLanguageChanged -= RefreshVisuals;
            base.OnDestroy();
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);
            RefreshVisuals();
        }

        /// <summary>把分辨率档位循环前移/后移一位（越界回绕）。</summary>
        void ShiftResolution(int delta)
        {
            var count = SettingsModel.Resolutions.Length;
            var next = (_settings.ResolutionIndex.Value + delta + count) % count;
            _settings.ResolutionIndex.Value = next;
            RefreshVisuals();
        }

        /// <summary>
        /// 播放一次 UI 点击音效（经 AudioModule 的 SFX 通道，音量随设置面板的音效滑条）。
        /// 未配置片段时静默跳过；拖动滑条等连续触发按 <see cref="ClickThrottleSeconds" /> 节流。
        /// </summary>
        void PlayClick()
        {
            if (uiClickSfx == null)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now - lastClickTime < ClickThrottleSeconds)
            {
                return;
            }

            lastClickTime = now;
            AudioModule.PlaySfx(uiClickSfx, uiClickVolume);
        }

        /// <summary>把 SettingsModel 当前值刷新到 UI（滑条静默同步，不触发 onValueChanged 回写）。</summary>
        void RefreshVisuals()
        {
            var r = SettingsModel.Resolutions[_settings.ResolutionIndex.Value];
            resolutionValueText.text = r.x + " × " + r.y;

            bool fs = _settings.Fullscreen.Value;
            if (fullscreenText != null)
            {
                fullscreenText.text = Localization.Get(Localization.Key.SettingsFullscreen,
                    Localization.Get(fs ? Localization.Key.SettingsStateOn : Localization.Key.SettingsStateOff));
            }

            if (fullscreenButtonImage != null)
            {
                fullscreenButtonImage.sprite = fs ? fullscreenSelectedSprite : fullscreenUnselectedSprite;
            }

            sfxSlider.SetValueWithoutNotify(_settings.SfxVolume.Value);
            musicSlider.SetValueWithoutNotify(_settings.BgmVolume.Value);

            bool zh = _settings.Language.Value == SettingsModel.LanguageChinese;
            titleText.text = Localization.Get(Localization.Key.SettingsTitle);
            resolutionRowLabel.text = Localization.Get(Localization.Key.SettingsResolution);
            sfxLabel.text = Localization.Get(Localization.Key.SettingsSfx);
            musicLabel.text = Localization.Get(Localization.Key.SettingsMusic);
            languageLabel.text = Localization.Get(Localization.Key.SettingsLanguage);

            // 中文模式用图集金字切片，英文模式回落运行时文本
            SetBakedOrText(titleImage, titleText, zh);
            SetBakedOrText(resolutionRowLabelImage, resolutionRowLabel, zh);
            SetBakedOrText(sfxLabelImage, sfxLabel, zh);
            SetBakedOrText(musicLabelImage, musicLabel, zh);
            SetBakedOrText(languageLabelImage, languageLabel, zh);

            // 语言选中态：胶囊底图互换（切片自带发光/哑光两态，不染色）
            if (chineseButtonImage != null)
            {
                chineseButtonImage.sprite = zh ? languageSelectedSprite : languageUnselectedSprite;
            }

            if (englishButtonImage != null)
            {
                englishButtonImage.sprite = zh ? languageUnselectedSprite : languageSelectedSprite;
            }
        }

        /// <summary>中文模式显示图集金字切片、英文模式回落运行时文本（切片缺失时文本常显）。</summary>
        static void SetBakedOrText(Image baked, Text fallback, bool zh)
        {
            if (baked != null)
            {
                baked.gameObject.SetActive(zh);
            }

            if (fallback != null)
            {
                fallback.gameObject.SetActive(!zh || baked == null);
            }
        }
    }
}
