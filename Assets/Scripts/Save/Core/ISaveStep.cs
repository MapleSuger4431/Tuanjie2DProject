namespace Game.Save
{
    /// <summary>
    /// 一个存档模块 = 一个 Step。SaveDirector 按注册顺序依次执行各 Step，
    /// 全部模块都写进同一个根存档后再统一落盘。
    /// 扩展方式：新增模块只需再加一个 Step 并 Register，不改动已有模块与流程。
    /// </summary>
    public interface ISaveStep
    {
        /// <summary>模块名，用于流程日志与排查。</summary>
        string ModuleName { get; }

        /// <summary>本模块：Model → 根存档。</summary>
        void Collect(SaveGame root);

        /// <summary>本模块：根存档 → Model。</summary>
        void Apply(SaveGame root);
    }
}
