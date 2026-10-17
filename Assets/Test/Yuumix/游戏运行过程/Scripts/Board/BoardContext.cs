using Runestone.AesirArchitecture;

namespace GameRuntimeTest.Board
{
    /// <summary>棋盘玩法步骤的 Context：按具体类注册全部 Model（Quick 档）。</summary>
    public sealed class BoardContext : AbstractContext<BoardContext>
    {
        protected override void Configure()
        {
            RegisterModel(new BoardModel());
            RegisterModel(new PieceModel());
        }
    }
}
