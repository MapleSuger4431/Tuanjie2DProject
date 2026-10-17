using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>棋盘玩法步骤的 Context：按具体类注册全部 Model（Quick 档）。</summary>
    public sealed class BoardContext : AbstractContext<BoardContext>
    {
        protected override void Configure()
        {
            // 随机表必须最先注册：PieceModel 初始化抽第一块时就要读它
            RegisterModel(new BoardTilePoolModel());
            RegisterModel(new BoardModel());
            RegisterModel(new PieceModel());
            RegisterModel(new BoardTimeModel());
            // 偏好顺序先于运行模型注册：BoardRunModel 初始化时要取它
            RegisterModel(new AiPreferenceModel());
            RegisterModel(new BoardRunModel());
        }
    }
}
