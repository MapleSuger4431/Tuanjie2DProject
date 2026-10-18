using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// AI 的单步移动方向（四邻接，不走斜向）。
    /// 约定同 <see cref="CellEffect" />：底层类型固定 int、每项显式写数值、每项带中文 InspectorName。
    /// 默认值顺序即默认优先级：右 → 左 → 上 → 下。
    /// </summary>
    public enum MoveDirection
    {
        /// <summary>向右（+x）。</summary>
        [InspectorName("右")] Right = 0,

        /// <summary>向左（-x）。</summary>
        [InspectorName("左")] Left = 1,

        /// <summary>向上（+y）。</summary>
        [InspectorName("上")] Up = 2,

        /// <summary>向下（-y）。</summary>
        [InspectorName("下")] Down = 3,
    }

    /// <summary>方向的静态元数据：偏移、反向、中文名；AI 单步决策与 UI 排序列表共用。</summary>
    public static class MoveDirectionInfo
    {
        /// <summary>四个方向的固定遍历顺序（即默认优先级顺序）。</summary>
        public static readonly MoveDirection[] All =
        {
            MoveDirection.Right,
            MoveDirection.Left,
            MoveDirection.Up,
            MoveDirection.Down,
        };

        public const int Count = 4;

        public static Vector2Int Offset(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Right: return Vector2Int.right;
                case MoveDirection.Left: return Vector2Int.left;
                case MoveDirection.Up: return Vector2Int.up;
                default: return Vector2Int.down;
            }
        }

        /// <summary>反向：用于禁止 180° 掉头的判定。</summary>
        public static MoveDirection Opposite(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Right: return MoveDirection.Left;
                case MoveDirection.Left: return MoveDirection.Right;
                case MoveDirection.Up: return MoveDirection.Down;
                default: return MoveDirection.Up;
            }
        }

        /// <summary>
        /// 顺时针旋转 90°（y 向上：(x, y) → (y, -x)）：上→右→下→左→上。
        /// 与 <see cref="TilePiece.RotateCW" /> 把格偏移做同一旋转，箭头方向才能跟方块一起转。
        /// </summary>
        public static MoveDirection RotateCW(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Up: return MoveDirection.Right;
                case MoveDirection.Right: return MoveDirection.Down;
                case MoveDirection.Down: return MoveDirection.Left;
                default: return MoveDirection.Up;
            }
        }

        /// <summary>中文名，UI 排序列表直接显示。</summary>
        public static string DisplayName(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Right: return "右 →";
                case MoveDirection.Left: return "左 ←";
                case MoveDirection.Up: return "上 ↑";
                default: return "下 ↓";
            }
        }
    }
}
