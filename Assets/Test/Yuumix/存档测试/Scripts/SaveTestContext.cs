using Runestone.AesirArchitecture;

namespace SaveTest
{
    /// <summary>存档测试的 Context：按具体类注册全部 Model（Quick 档）。</summary>
    public sealed class SaveTestContext : AbstractContext<SaveTestContext>
    {
        protected override void Configure()
        {
            RegisterModel(new PlayModel());
            RegisterModel(new SettingsModel());
        }
    }
}
