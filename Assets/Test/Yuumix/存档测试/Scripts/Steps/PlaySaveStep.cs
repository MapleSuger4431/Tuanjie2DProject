namespace SaveTest
{
    /// <summary>Play 模块存档步骤：负责 PlayModel ↔ PlayData 这一段转换。</summary>
    public sealed class PlaySaveStep : ISaveStep
    {
        readonly PlayModel _model;

        public PlaySaveStep(PlayModel model) => _model = model;

        public string ModuleName => "Play";

        public void Collect(SaveGame root)
        {
            var data = root.Play ?? new PlayData();
            _model.WriteTo(data);
            root.Play = data;
        }

        public void Apply(SaveGame root) => _model.ReadFrom(root.Play ?? new PlayData());
    }
}
