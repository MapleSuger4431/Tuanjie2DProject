using Game.Model;
using Game.Service;
using Runestone.AesirArchitecture;

public sealed class GameContext : AbstractContext<GameContext>
{
    protected override void Configure()
    {
        // 注册 Model——默认按最低档（Quick）：直接注册具体类，不建接口
        RegisterModel(new LevelProgressModel()); // 各关通关记录（最短用时），先于选关Model：它要查询记录
        RegisterModel(new LevelSelectModel());   // 选关系统
        RegisterModel(new PlayModel());          // 玩法存档占位（后续替换为真实玩法数据）
        RegisterModel(new SettingsModel());      // 全局设置（音量 / 语言 / 分辨率）

        // 注册 Service——顺序即初始化顺序：
        // SaveService 先装配存档流程并订阅「设置变更即存档」，SettingsService 再把设置下发到运行时
        RegisterService(new SaveService());
        RegisterService(new SettingsService());
        RegisterService(new LevelProgressService());   // 通关记录写入（关卡场景经它桥接 GameContext）
    }
}
