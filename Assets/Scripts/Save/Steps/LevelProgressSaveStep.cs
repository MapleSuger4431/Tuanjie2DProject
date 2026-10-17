using System.Collections.Generic;
using Game.Model;
using Game.Save;

namespace Game.Save
{
    /// <summary>关卡进度存档步骤：负责 LevelProgressModel ↔ LevelProgressData 这一段转换。</summary>
    public sealed class LevelProgressSaveStep : ISaveStep
    {
        readonly LevelProgressModel _model;

        public LevelProgressSaveStep(LevelProgressModel model) => _model = model;

        public string ModuleName => "Levels";

        public void Collect(SaveGame root)
        {
            var data = root.Levels ?? new LevelProgressData();
            _model.WriteTo(data);
            root.Levels = data;
        }

        public void Apply(SaveGame root) => _model.ReadFrom(root.Levels ?? new LevelProgressData());
    }
}
