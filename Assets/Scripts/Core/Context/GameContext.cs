using Runestone.AesirArchitecture;

public sealed class GameContext : AbstractContext<GameContext>
{
    protected override void Configure()
    {
        // 注册 Model——默认按最低档（Quick）：直接注册具体类，不建接口
        // RegisterModel(new PlayerModel());
    }
}
