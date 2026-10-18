using System;
using Game.Model;
using Game.Save;
using Runestone.AesirArchitecture;

namespace Game.Service
{
    /// <summary>
    /// 存档服务：装配各模块的 <see cref="ISaveStep" />、统一落盘与读档（<see cref="SaveStorage" />）。
    /// <para>
    /// 扩展方式：新增存档模块 = 加一个 Step + 在 <see cref="OnInitialize" /> 里 Register 一行，
    /// 已有模块与流程不动。
    /// </para>
    /// <para>
    /// 「设置变更即存档」由本服务订阅 <see cref="SettingsModel" /> 的可观察字段实现；
    /// 读档回填期间（<c>_loading</c>）触发的变更不回写，避免刚读完就立刻再写一遍。
    /// 设置值下发到运行时（AudioModule / Localization / Screen）由 <see cref="SettingsService" /> 负责。
    /// </para>
    /// </summary>
    public sealed class SaveService : AbstractService
    {
        SaveDirector _director;
        SettingsModel _settings;
        LevelProgressModel _levels;
        AchievementModel _achievements;
        bool _loading;

        Action<float> _onBgmChanged;
        Action<float> _onSfxChanged;
        Action<string> _onLanguageChanged;
        Action<int> _onResolutionChanged;
        Action<bool> _onFullscreenChanged;
        Action _onProgressChanged;
        Action _onAchievementChanged;

        /// <summary>当前存档位置（诊断/日志用；编辑器为项目根 TestSaves/，打包版为 PlayerPrefs）。</summary>
        public string SaveLocation => SaveStorage.DefaultPath;

        /// <summary>是否已有存档（用于「继续游戏」可用性判断）。</summary>
        public bool HasSave => SaveStorage.DefaultExists();

        protected override void OnInitialize()
        {
            _settings = this.GetModel<SettingsModel>();
            _levels = this.GetModel<LevelProgressModel>();

            _onProgressChanged = SaveOnProgressChanged;
            _levels.Changed.AddListener(_onProgressChanged);

            _achievements = this.GetModel<AchievementModel>();
            _onAchievementChanged = SaveOnAchievementChanged;
            _achievements.Changed.AddListener(_onAchievementChanged);

            _director = new SaveDirector()
                .Register(new PlaySaveStep(this.GetModel<PlayModel>()))
                .Register(new SettingsSaveStep(_settings))
                .Register(new LevelProgressSaveStep(_levels))
                .Register(new AchievementSaveStep(_achievements));

            _onBgmChanged = _ => SaveOnSettingsChanged();
            _onSfxChanged = _ => SaveOnSettingsChanged();
            _onLanguageChanged = _ => SaveOnSettingsChanged();
            _onResolutionChanged = _ => SaveOnSettingsChanged();
            _onFullscreenChanged = _ => SaveOnSettingsChanged();

            _settings.BgmVolume.AddListener(_onBgmChanged);
            _settings.SfxVolume.AddListener(_onSfxChanged);
            _settings.Language.AddListener(_onLanguageChanged);
            _settings.ResolutionIndex.AddListener(_onResolutionChanged);
            _settings.Fullscreen.AddListener(_onFullscreenChanged);
        }

        protected override void OnDispose()
        {
            if (_levels != null && _onProgressChanged != null) _levels.Changed.RemoveListener(_onProgressChanged);
            if (_achievements != null && _onAchievementChanged != null)
                _achievements.Changed.RemoveListener(_onAchievementChanged);

            if (_settings == null) return;

            _settings.BgmVolume.RemoveListener(_onBgmChanged);
            _settings.SfxVolume.RemoveListener(_onSfxChanged);
            _settings.Language.RemoveListener(_onLanguageChanged);
            _settings.ResolutionIndex.RemoveListener(_onResolutionChanged);
            _settings.Fullscreen.RemoveListener(_onFullscreenChanged);
        }

        /// <summary>按模块顺序收集数据并落盘（读档过程中调用会被忽略）。</summary>
        public void Save()
        {
            if (_loading) return;
            _director.Save();
        }

        /// <summary>读档并回填各模块；无存档时返回 false（各模块保持默认值）。</summary>
        public bool Load()
        {
            _loading = true;
            try
            {
                return _director.Load();
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>删除存档。</summary>
        public void Delete() => SaveStorage.DeleteDefault();

        void SaveOnSettingsChanged()
        {
            if (_loading) return;
            Save();
        }

        /// <summary>成就解锁 / 进度推进即落盘：解锁状态与计数进度都要跨会话保留。</summary>
        void SaveOnAchievementChanged()
        {
            if (_loading) return;
            Save();
        }

        /// <summary>通关记录变化即落盘：破纪录要留住，没破纪录也要记住「通关次数 +1 / 上次用时」。</summary>
        void SaveOnProgressChanged()
        {
            if (_loading) return;
            Save();
        }
    }
}
