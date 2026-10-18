using Game.Model;
using Game.Save;

namespace Game.Save
{
    /// <summary>成就存档步骤：负责 AchievementModel ↔ AchievementData 这一段转换。</summary>
    public sealed class AchievementSaveStep : ISaveStep
    {
        readonly AchievementModel _model;

        public AchievementSaveStep(AchievementModel model) => _model = model;

        public string ModuleName => "Achievements";

        public void Collect(SaveGame root)
        {
            var data = root.Achievements ?? new AchievementData();
            _model.WriteTo(data);
            root.Achievements = data;
        }

        public void Apply(SaveGame root) => _model.ReadFrom(root.Achievements ?? new AchievementData());
    }
}
