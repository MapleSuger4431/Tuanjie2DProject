using Game.Core;

namespace Game.Board
{
    /// <summary>
    /// 通关失败结算面板：标题 + 成绩两行（步数 / 已用方块）
    /// +「重玩此关 / 回到选关界面」两个出口（行为与成绩区都在 <see cref="LevelResultPanel" />）。
    /// <para>
    /// 失败页**不记时间**：秒表停在「无路可走」那一刻，用时作为成绩没有意义，故不展示。
    /// </para>
    /// </summary>
    public sealed class LevelFailPanel : LevelResultPanel
    {
        protected override Localization.Key TitleKey => Localization.Key.LevelFailTitle;
    }
}
