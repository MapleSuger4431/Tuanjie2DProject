using System;
using Game.Model;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Game.Service
{
    /// <summary>
    /// 设置服务：把 <see cref="SettingsModel" /> 的值下发到运行时系统——
    /// 音效/音乐音量 → AF 框架 <see cref="AudioModule" /> 的 SFX / BGM 通道，
    /// 语言 → Unity Localization 的 SelectedLocale，分辨率档位 → <c>Screen.SetResolution</c>，全屏 → <c>Screen.fullScreen</c>。
    /// <para>
    /// 订阅用 <c>AddListenerAndInvoke</c>：初始化时即下发一次，读档改写 Model 时也会自动下发，
    /// 因此「启动读档 → 设置生效」不需要额外的应用入口。
    /// </para>
    /// </summary>
    public sealed class SettingsService : AbstractService
    {
        SettingsModel _settings;

        Action<float> _onBgmVolume;
        Action<float> _onSfxVolume;
        Action<string> _onLanguage;
        Action<int> _onResolutionIndex;
        Action<bool> _onFullscreen;

        /// <summary>抑制「本服务自己改 SelectedLocale」引发的回环（改值期间不再回灌模型）。</summary>
        bool _applyingLocale;

        protected override void OnInitialize()
        {
            _settings = this.GetModel<SettingsModel>();

            _onBgmVolume = v => AudioModule.BgmVolume = v;
            _onSfxVolume = v => AudioModule.SfxVolume = v;
            _onLanguage = ApplyLanguage;
            _onResolutionIndex = ApplyResolution;
            _onFullscreen = ApplyFullscreen;

            _settings.BgmVolume.AddListenerAndInvoke(_onBgmVolume);
            _settings.SfxVolume.AddListenerAndInvoke(_onSfxVolume);
            _settings.Language.AddListenerAndInvoke(_onLanguage);
            _settings.ResolutionIndex.AddListenerAndInvoke(_onResolutionIndex);
            _settings.Fullscreen.AddListenerAndInvoke(_onFullscreen);

            // 外部改语言的入口（Localization 包自带的调试下拉框直接写 SelectedLocale，不经模型）：
            // 订阅后回灌 SettingsModel，界面刷新 / 设置面板选中态 / 存档三者才能跟着一致
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        protected override void OnDispose()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;

            if (_settings == null) return;

            _settings.BgmVolume.RemoveListener(_onBgmVolume);
            _settings.SfxVolume.RemoveListener(_onSfxVolume);
            _settings.Language.RemoveListener(_onLanguage);
            _settings.ResolutionIndex.RemoveListener(_onResolutionIndex);
            _settings.Fullscreen.RemoveListener(_onFullscreen);
        }

        /// <summary>切换 Unity Localization 的 SelectedLocale 并广播，已打开的界面即时刷新文本。</summary>
        void ApplyLanguage(string code)
        {
            var init = LocalizationSettings.InitializationOperation;

            // 广播必须排在 SelectedLocale 真正生效之后，否则界面会按旧语言取值（文本不刷新）
            if (init.IsDone)
            {
                ApplyLocaleNow(code);
                Game.Core.Localization.RaiseLanguageChanged();
            }
            else
            {
                init.Completed += _ =>
                {
                    ApplyLocaleNow(code);
                    Game.Core.Localization.RaiseLanguageChanged();
                };
            }
        }

        void ApplyResolution(int index)
        {
            index = Mathf.Clamp(index, 0, SettingsModel.Resolutions.Length - 1);
            var r = SettingsModel.Resolutions[index];
            Screen.SetResolution(r.x, r.y, _settings.Fullscreen.Value);
        }

        void ApplyFullscreen(bool value)
        {
            // 只切窗口模式不改分辨率；编辑器里受 Game View 钳制，打包后完整生效
            Screen.fullScreen = value;
        }

        void ApplyLocaleNow(string code)
        {
            var id = new LocaleIdentifier(code);
            var locale = LocalizationSettings.AvailableLocales.GetLocale(id);
            if (locale == null)
            {
                Debug.LogWarning($"[SettingsService] 可用语言中找不到 {id.Code}，请检查 Localization Settings 的 Available Locales");
                return;
            }

            if (LocalizationSettings.SelectedLocale != locale)
            {
                _applyingLocale = true;
                try
                {
                    LocalizationSettings.SelectedLocale = locale;
                }
                finally
                {
                    _applyingLocale = false;
                }
            }
        }

        /// <summary>
        /// 外部改动 SelectedLocale 的回调：把语言回灌 <see cref="SettingsModel" />，
        /// 使界面刷新、设置面板选中态与存档落盘一起跟上（本服务自己的改动由 <see cref="_applyingLocale" /> 跳过）。
        /// </summary>
        void OnSelectedLocaleChanged(Locale locale)
        {
            if (_applyingLocale || _settings == null || locale == null) return;

            var code = locale.Identifier.Code;

            // ObservableValue 赋同值不触发监听，这里手动广播一次，保证界面仍会刷新
            if (string.Equals(_settings.Language.Value, code))
            {
                Game.Core.Localization.RaiseLanguageChanged();
                return;
            }

            _settings.Language.Value = code;
        }
    }
}
