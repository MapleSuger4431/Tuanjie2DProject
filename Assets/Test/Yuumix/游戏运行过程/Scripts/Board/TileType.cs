using UnityEngine;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 棋盘格的当前状态：5 种玩法状态 + 起点/终点两种专属状态。
    /// 语义对齐 Docs/Requirements/06 3.1 / 3.2：Mountain ⇔ Forbidden（关卡预置、不可覆盖），Void ⇔ 未铺设虚空。
    /// 文档的 Hollow（洞）与 Ban（落定后永久障碍）本步不做，枚举新增值不影响既有判定。
    /// </summary>
    public enum TileType
    {
        /// <summary>空（虚空）：不可通行，可被方块覆盖。</summary>
        Void = 0,

        /// <summary>可通行：普通路面，任何属性均可走。</summary>
        Land = 1,

        /// <summary>火：仅火属性可通行。</summary>
        Fire = 2,

        /// <summary>冰：仅冰属性可通行。</summary>
        Ice = 3,

        /// <summary>山地：不可通行、不可被覆盖（关卡预置障碍）。</summary>
        Mountain = 4,

        /// <summary>起点（唯一，不可覆盖，AI 可站立）。</summary>
        Start = 5,

        /// <summary>终点（唯一，不可覆盖，AI 可站立）。</summary>
        Goal = 6,
    }

    /// <summary>格子状态的静态元数据：可通行性、可覆盖性与默认配色，全项目唯一判定口。</summary>
    public static class TileTypeInfo
    {
        public static bool IsPassable(TileType type)
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
