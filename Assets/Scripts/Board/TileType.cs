using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 棋盘格的当前状态。七大状态分四组，语义对齐用户拍板的三类划分：
    /// ①可行走块：<see cref="Land" />（路）、<see cref="Fire" />（火）、<see cref="Ice" />（冰）—— AI 可正常踏入；
    /// ②虚空 <see cref="Void" />（空）：不可行走，等待被方块覆盖铺成路面；
    /// ③禁止覆盖块 <see cref="Mountain" />（山地）：完全不可走，也不允许被任何方块覆盖；
    /// ④起点 <see cref="Start" /> 与终点 <see cref="Goal" />：极特殊——不可行走（不作中途路径格）、不可被覆盖，
    ///   起点只是 AI 的出发站位，终点由 AI 决策特判「进入即胜利」。
    /// 数值 ↔ 状态直接对齐，关卡画笔（<see cref="BoardLevelPainter" />）循环的是 0~4 五档，起点/终点由棋盘自动写入。
    /// </summary>
    public enum TileType
    {
        /// <summary>空（虚空）：不可通行，可被方块覆盖铺成路面。</summary>
        Void = 0,

        /// <summary>路：可通行，普通路面。</summary>
        Land = 1,

        /// <summary>火：可通行，火属性路面。</summary>
        Fire = 2,

        /// <summary>冰：可通行，冰属性路面。</summary>
        Ice = 3,

        /// <summary>山地：不可通行、不可被覆盖（关卡预置障碍）。</summary>
        Mountain = 4,

        /// <summary>起点（唯一，左下角）：不可作普通路径格、不可被覆盖，AI 在此出生。</summary>
        Start = 5,

        /// <summary>终点（唯一，右上角）：不可作普通路径格、不可被覆盖，AI 进入即胜利。</summary>
        Goal = 6,
    }

    /// <summary>格子状态的静态元数据：分类、可通行性、可覆盖性与默认配色，全项目唯一判定口。</summary>
    public static class TileTypeInfo
    {
        /// <summary>状态分组，与用户对地块的三大类划分一一对应（外加虚空一类）。</summary>
        public enum Category
        {
            /// <summary>可行走块：路 / 火 / 冰。</summary>
            Walkable,

            /// <summary>虚空（空）：不可走但可铺。</summary>
            Void,

            /// <summary>禁止覆盖块：山地，不可走也不可铺。</summary>
            Forbidden,

            /// <summary>起点 / 终点：不可走、不可铺的特殊格。</summary>
            Terminal,
        }

        /// <summary>
        /// 普通可通行性：只有三种可行走块为真。
        /// 起点与终点**不算**可通行——它们不作中途路径格；终点能否进入由 AI 决策特判（见 <see cref="BoardRunModel" />）。
        /// </summary>
        public static bool IsPassable(TileType type)
        {
            switch (type)
            {
                case TileType.Land:
                case TileType.Fire:
                case TileType.Ice:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>能否被方块覆盖：山地与起点/终点钉死，其余（含已铺格）允许再覆盖。</summary>
        public static bool IsCoverable(TileType type)
        {
            switch (type)
            {
                case TileType.Void:
                case TileType.Land:
                case TileType.Fire:
                case TileType.Ice:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 能否作为「方向提示」的落格：AI 实际会踏上的格子——可行走块（路 / 火 / 冰），
        /// 以及 AI 会站上去的起点/终点（预置箭头可用来给定初始朝向）。空地 / 山地一律不放提示。
        /// </summary>
        public static bool IsHintAllowed(TileType type)
        {
            switch (type)
            {
                case TileType.Land:
                case TileType.Fire:
                case TileType.Ice:
                case TileType.Start:
                case TileType.Goal:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>是否起点或终点：两者在覆盖判定与 AI 决策里都要走专属分支。</summary>
        public static bool IsTerminal(TileType type) =>
            type == TileType.Start || type == TileType.Goal;

        public static Category CategoryOf(TileType type)
        {
            switch (type)
            {
                case TileType.Land:
                case TileType.Fire:
                case TileType.Ice:
                    return Category.Walkable;
                case TileType.Mountain:
                    return Category.Forbidden;
                case TileType.Start:
                case TileType.Goal:
                    return Category.Terminal;
                default:
                    return Category.Void;
            }
        }

        /// <summary>中文显示名，供日志与 HUD 文本使用。</summary>
        public static string DisplayName(TileType type)
        {
            switch (type)
            {
                case TileType.Land: return "路";
                case TileType.Fire: return "火";
                case TileType.Ice: return "冰";
                case TileType.Mountain: return "血色深渊";
                case TileType.Start: return "起点";
                case TileType.Goal: return "终点";
                default: return "深渊";
            }
        }

        public static Color Color(TileType type)
        {
            switch (type)
            {
                case TileType.Land: return new Color(0.86f, 0.87f, 0.90f);
                case TileType.Fire: return new Color(0.95f, 0.42f, 0.28f);
                case TileType.Ice: return new Color(0.36f, 0.78f, 0.95f);
                case TileType.Mountain: return new Color(0.36f, 0.28f, 0.22f);
                case TileType.Start: return new Color(0.35f, 0.85f, 0.45f);
                case TileType.Goal: return new Color(0.98f, 0.82f, 0.25f);
                default: return new Color(0.16f, 0.15f, 0.22f);
            }
        }
    }
}
