using Game.Model;

namespace Game.Save
{
    /// <summary>Settings 模块存档步骤：负责 SettingsModel ↔ SettingsData 这一段转换。</summary>
    public sealed class SettingsSaveStep : ISaveStep
    {
        readonly SettingsModel _model;

        public SettingsSaveStep(SettingsModel model) => _model = model;

        public string ModuleName => "Settings";

        public void Collect(SaveGame root)
        {
            var data = root.Settings ?? new SettingsData();
            _model.WriteTo(data);
            root.Settings = data;
        }

        public void Apply(SaveGame root) => _model.ReadFrom(root.Settings ?? new SettingsData());
    }
}
