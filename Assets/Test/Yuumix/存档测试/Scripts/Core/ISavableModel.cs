namespace SaveTest
{
    /// <summary>
    /// 可存档 Model 的契约：由 Model 自己决定「哪些内容进存档、读档时怎么回填」。
    /// 存档数据放对应的 Data 类（PlayModel → PlayData），Data 只承载需要落盘的字段，
    /// 因此「运行时数据」与「存档数据」天然分离——Model 里只有一部分内容会进存档。
    /// </summary>
    /// <typeparam name="TData">本 Model 对应的存档类。</typeparam>
    public interface ISavableModel<TData> where TData : class, new()
    {
        /// <summary>Model → Data：只写需要存档的字段。</summary>
        void WriteTo(TData data);

        /// <summary>Data → Model：回填到运行时字段（赋 ObservableValue.Value 即自动通知 UI）。</summary>
        void ReadFrom(TData data);
    }
}
