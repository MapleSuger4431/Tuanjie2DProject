using System;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 一个待放置的方块实例 = 形状 + 每一格各自随机出来的状态。
    /// 形状模板 <see cref="TileShape" /> 是全局共享的、不能存随机结果，所以随机出来的逐格状态挂在这层实例上。
    /// 下标与 <see cref="TileShape.Cells" /> 一一对齐。
    /// </summary>
    public sealed class TilePiece
    {
        readonly TileShape _shape;
        readonly TileType[] _cellTypes;

        public TilePiece(TileShape shape, TileType[] cellTypes)
        {
            _shape = shape;

            var count = shape != null && shape.Cells != null ? shape.Cells.Length : 0;
            if (cellTypes != null && cellTypes.Length == count)
            {
                _cellTypes = cellTypes;
            }
            else
            {
                _cellTypes = new TileType[count];
                for (int i = 0; i < count; i++) _cellTypes[i] = TileType.Land;
            }
        }

        public TileShape Shape => _shape;

        public Vector2Int[] Cells => _shape != null ? _shape.Cells : Array.Empty<Vector2Int>();

        public int CellCount => _cellTypes.Length;

        /// <summary>第 index 格的状态（与 <see cref="Cells" /> 同下标）。</summary>
        public TileType TypeAt(int index) =>
            index >= 0 && index < _cellTypes.Length ? _cellTypes[index] : TileType.Land;

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(_shape != null ? _shape.ShapeName : "?").Append('[');
            for (int i = 0; i < _cellTypes.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(TileTypeInfo.DisplayName(_cellTypes[i]));
            }

            return sb.Append(']').ToString();
        }
    }
}
