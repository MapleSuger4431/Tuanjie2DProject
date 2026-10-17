using Runestone.AesirArchitecture;

namespace GameRuntimeTest
{
    /// <summary>游戏运行过程测试场景的 Context：按具体类注册全部 Model（Quick 档）。</summary>
    public sealed class GameRuntimeTestContext : AbstractContext<GameRuntimeTestContext>
    {
        protected override void Configure()
        {
            RegisterModel(new RuntimeFlowModel());
            RegisterModel(new RuntimeScoreModel());
        }
    }
}
