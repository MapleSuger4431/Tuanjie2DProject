using System;
using UnityEngine;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 方块形状：四邻连通的格偏移集合 + 形状色 + 该方块铺设到棋盘上的元素状态。
    /// 偏移以包围盒左上角为局部原点（最小值恒为 0,0），放置时该原点对齐鼠标所在格。
    /// </summary>
    [Serializable]
    public sealed class TileShape
    {
        [SerializeField] string shapeName;
        [SerializeField] Vector2Int[] cells;
        [SerializeField] Color color;
        [SerializeField] TileType tileType = TileType.Land;

        public string ShapeName => shapeName;
        public Vector2Int[] Cells => cells;
        public Color Color => color;

        /// <summary>铺设后写入目标格的状态（Land / Fire / Ice）。</summary>
        public TileType TileType => tileType;

        public TileShape(string shapeName, Color color, TileType tileType, Vector2Int[] cells)
        {
            this.shapeName = shapeName;
            this.color = color;
            this.tileType = tileType;
            this.cells = cells;
        }
    }

    /// <summary>七种经典俄罗斯方块（均为四格四邻连通，包围盒左上角对齐局部原点），各带一种元素属性。</summary>
    public static class TileShapeLibrary
    {
        public static TileShape[] CreateTetrominoes()
        {
            return new[]
            {
                new TileShape("I", new Color(0.24f, 0.80f, 0.94f), TileType.Ice,
                    Cells(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0))),
                new TileShape("O", new Color(0.98f, 0.83f, 0.24f), TileType.Land,
                    Cells(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1))),
                new TileShape("T", new Color(0.71f, 0.45f, 0.92f), TileType.Fire,
                    Cells(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(1, 1))),
                new TileShape("S", new Color(0.36f, 0.82f, 0.44f), TileType.Land,
                    Cells(new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1))),
                new TileShape("Z", new Color(0.94f, 0.35f, 0.38f), TileType.Fire,
                    Cells(new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1))),
                new TileShape("J", new Color(0.32f, 0.47f, 0.94f), TileType.Ice,
                    Cells(new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1))),
                new TileShape("L", new Color(0.98f, 0.60f, 0.24f), TileType.Land,
                    Cells(new Vector2Int(2, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1)))
            };
        }

        static Vector2Int[] Cells(params Vector2Int[] cells) => cells;
    }
}
