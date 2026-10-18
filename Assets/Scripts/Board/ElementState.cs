namespace Game.Board
{
    /// <summary>
    /// AI 的元素属性（06 篇 RB-09「火人 / 冰人」）：决定火格与冰格的通行判定——
    /// 火属性才能踏入 <see cref="TileType.Fire" />，冰属性才能踏入 <see cref="TileType.Ice" />，陆地任意属性可走。
    /// 出生值按关卡配置（<see cref="BoardRoot.InitialElement" />，每个关卡场景各配各的），
    /// 运行中被属性反转效果（<see cref="ElementSwitchEffect" />）在火 ↔ 冰之间翻转，立即影响后续每一步判定。
    /// </summary>
    public enum ElementState
    {
        /// <summary>火人：可走 陆地 + 火，不可走 冰。</summary>
        Fire = 0,

        /// <summary>冰人：可走 陆地 + 冰，不可走 火。</summary>
        Ice = 1,
    }

    /// <summary><see cref="ElementState" /> 的静态工具：元素通行判定与显示名。</summary>
    public static class ElementStateInfo
    {
        /// <summary>
        /// 该元素属性能否踏入该格状态：陆地任意可走；火格需火属性、冰格需冰属性；其余状态一律不可
        ///（配合 <see cref="TileTypeInfo.IsPassable" /> 通行层判定使用，单独调用也安全——空 / 山地同样返回 false）。
        /// </summary>
        public static bool CanWalkOn(ElementState element, TileType type)
        {
            switch (type)
            {
                case TileType.Land:
                    return true;
                case TileType.Fire:
                    return element == ElementState.Fire;
                case TileType.Ice:
                    return element == ElementState.Ice;
                default:
                    return false;
            }
        }

        /// <summary>另一个元素（反转用）。</summary>
        public static ElementState Other(ElementState element) =>
            element == ElementState.Fire ? ElementState.Ice : ElementState.Fire;

        /// <summary>中文显示名（日志 / HUD 用）：火 / 冰。</summary>
        public static string DisplayName(ElementState element) =>
            element == ElementState.Fire ? "火" : "冰";
    }
}
