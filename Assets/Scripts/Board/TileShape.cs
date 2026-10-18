using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
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

        /// <summary>
        /// 沿用本形状的名称、颜色与铺设状态，只换一套格偏移：形状模板是全局共享的，
        /// 旋转只能产出新实例，就地改 <see cref="Cells" /> 会把整个形状库一起改坏。
        /// </summary>
        public TileShape WithCells(Vector2Int[] cells) => new TileShape(shapeName, color, tileType, cells);
    }

    /// <summary>七种经典俄罗斯方块（均为四格四邻连通，包围盒左上角对齐局部原点），各带一种元素属性。</summary>
    public static class TileShapeLibrary
    {
        /// <summary>一块方块最多几格——形状库枚举上限，也是下一块 16 宫格预览能完整显示的量级。</summary>
        public const int MaxPieceCells = 6;

        /// <summary>形状包围盒的最大边长：预览是 4×4 的十六宫格，超过就显示不全。</summary>
        public const int MaxBounds = 4;

        static readonly Vector2Int[] Neighbors =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
        };

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

        /// <summary>排序口径：先行后列（先行号小的，同行再列号小的）。用 IComparer 形态，好直接喂 Array.Sort。</summary>
        static readonly IComparer<Vector2Int> RowMajor = Comparer<Vector2Int>.Create(
            (a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

        /// <summary>
        /// 顺时针旋转 90°（y 向上：(x,y) → (y, maxX - x)），再平移回包围盒左上角并按先行后列排序，
        /// 与 <see cref="Normalize" /> 同一口径。
        /// <para>
        /// 挂在每格上的状态 <paramref name="types" /> 跟着各自那一格一起转 —— 格偏移与状态是一一对齐的，
        /// 分开转会让「哪一格是火」错位。types 缺失时按路面兜底。
        /// </para>
        /// </summary>
        public static void RotateClockwise(Vector2Int[] cells, TileType[] types,
            out Vector2Int[] rotatedCells, out TileType[] rotatedTypes)
        {
            var count = cells != null ? cells.Length : 0;
            rotatedCells = count > 0 ? new Vector2Int[count] : Array.Empty<Vector2Int>();
            rotatedTypes = count > 0 ? new TileType[count] : Array.Empty<TileType>();
            if (count == 0) return;

            var maxX = 0;
            for (var i = 0; i < count; i++) maxX = Mathf.Max(maxX, cells[i].x);

            for (var i = 0; i < count; i++)
            {
                rotatedCells[i] = new Vector2Int(cells[i].y, maxX - cells[i].x);
                rotatedTypes[i] = types != null && i < types.Length ? types[i] : TileType.Land;
            }

            NormalizeInPlace(rotatedCells, rotatedTypes);
        }

        /// <summary>
        /// 带效果层的旋转：格偏移、逐格状态 <paramref name="types" />、逐格效果 <paramref name="effects" />
        /// 三者一起转（下标始终与格偏移一一对齐）。effects 可为空（多数方块不带效果）。
        /// </summary>
        public static void RotateClockwise(Vector2Int[] cells, TileType[] types, TileEffect[] effects,
            out Vector2Int[] rotatedCells, out TileType[] rotatedTypes, out TileEffect[] rotatedEffects)
        {
            var count = cells != null ? cells.Length : 0;
            rotatedCells = count > 0 ? new Vector2Int[count] : Array.Empty<Vector2Int>();
            rotatedTypes = count > 0 ? new TileType[count] : Array.Empty<TileType>();
            rotatedEffects = count > 0 ? new TileEffect[count] : Array.Empty<TileEffect>();
            if (count == 0) return;

            var maxX = 0;
            for (var i = 0; i < count; i++) maxX = Mathf.Max(maxX, cells[i].x);

            // order 记录「旋转后第 i 个位置来自原来的哪一格」，跟着 cells 一起被重排
            var order = new int[count];
            for (var i = 0; i < count; i++)
            {
                rotatedCells[i] = new Vector2Int(cells[i].y, maxX - cells[i].x);
                order[i] = i;
            }

            NormalizeInPlaceWithOrder(rotatedCells, order);

            for (var i = 0; i < count; i++)
            {
                rotatedTypes[i] = types != null && order[i] < types.Length ? types[order[i]] : TileType.Land;
                rotatedEffects[i] = effects != null && order[i] < effects.Length ? effects[order[i]] : null;
            }
        }

        /// <summary>
        /// 枚举所有四邻连通的格块（固定多联骨牌：旋转 / 镜像算不同形状），格数取 [minCells, maxCells]、
        /// 包围盒边长不超过 maxBounds（否则十六宫格预览放不下）。偏移同样以包围盒左上角为局部原点。
        /// 逐格状态不在这里定——落盘时由关卡随机表按格独立抽。
        /// </summary>
        public static TileShape[] CreatePolyominoes(int minCells = 2,
            int maxCells = MaxPieceCells,
            int maxBounds = MaxBounds,
            bool excludeSquareBlock = false)
        {
            minCells = Mathf.Clamp(minCells, 1, MaxPieceCells);
            maxCells = Mathf.Clamp(maxCells, minCells, MaxPieceCells);
            maxBounds = Mathf.Clamp(maxBounds, 1, MaxBounds);

            var found = new List<Vector2Int[]>();
            var seen = new HashSet<string>();
            var frontier = new List<Vector2Int[]> { new[] { Vector2Int.zero } };
            seen.Add(Key(frontier[0]));

            for (int size = 1; size < maxCells && frontier.Count > 0; size++)
            {
                var grown = new List<Vector2Int[]>();
                for (int i = 0; i < frontier.Count; i++)
                {
                    foreach (var shape in Grow(frontier[i]))
                    {
                        if (!seen.Add(Key(shape))) continue;

                        grown.Add(shape);

                        // 田字格（2×2 实心）设计上明确不要，单独排除，其余四邻连通块照收
                        if (shape.Length >= minCells && Fits(shape, maxBounds)
                                                     && !(excludeSquareBlock && IsSquareBlock(shape)))
                            found.Add(shape);
                    }
                }

                frontier = grown;
            }

            found.Sort(CompareShapes);

            var shapes = new TileShape[found.Count];
            for (int i = 0; i < found.Count; i++)
            {
                var cells = found[i];
                shapes[i] = new TileShape($"{cells.Length}格#{i + 1:00}",
                    Color.HSVToRGB((i * 0.618f) % 1f, 0.45f, 0.92f),
                    TileType.Land,
                    cells);
            }

            return shapes;
        }

        /// <summary>在四邻各补一格，得到所有更大的连通块（已平移回包围盒左上角）。</summary>
        static IEnumerable<Vector2Int[]> Grow(Vector2Int[] cells)
        {
            var occupied = new HashSet<Vector2Int>(cells);

            for (int i = 0; i < cells.Length; i++)
            {
                for (int d = 0; d < Neighbors.Length; d++)
                {
                    var next = cells[i] + Neighbors[d];
                    if (occupied.Contains(next)) continue;

                    var grown = new Vector2Int[cells.Length + 1];
                    Array.Copy(cells, grown, cells.Length);
                    grown[cells.Length] = next;

                    yield return Normalize(grown);
                }
            }
        }

        /// <summary>平移到包围盒左上角（最小值恒为 0,0）并按行、列排序，让同一形状只有一种表达。</summary>
        static Vector2Int[] Normalize(Vector2Int[] cells)
        {
            var result = new Vector2Int[cells.Length];
            Array.Copy(cells, result, cells.Length);
            NormalizeInPlace(result, null);
            return result;
        }

        /// <summary>
        /// 就地平移到包围盒左上角并按先行后列排序；types 与 cells 一一对齐时同步重排
        ///（旋转要把每格的状态一起带走，靠的就是这一步）。
        /// </summary>
        static void NormalizeInPlace(Vector2Int[] cells, TileType[] types)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            for (var i = 0; i < cells.Length; i++)
            {
                minX = Mathf.Min(minX, cells[i].x);
                minY = Mathf.Min(minY, cells[i].y);
            }

            for (var i = 0; i < cells.Length; i++)
                cells[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);

            // 显式写泛型：Comparison<T> 会被优先匹配到非泛型的 Array.Sort(Array, Array, IComparer) 重载而报 CS1503
            if (types != null && types.Length == cells.Length) Array.Sort<Vector2Int, TileType>(cells, types, RowMajor);
            else Array.Sort<Vector2Int>(cells, RowMajor);
        }

        /// <summary>
        /// 就地平移到包围盒左上角并按先行后列排序，同时把下标数组 <paramref name="values" /> 按同样顺序重排
        /// ——带效果层的旋转靠它把「哪一格带什么效果」一起带走。
        /// 不写成 <see cref="NormalizeInPlace(Vector2Int[], TileType[])" /> 的重载是为了避开 null 实参的二义性。
        /// </summary>
        static void NormalizeInPlaceWithOrder(Vector2Int[] cells, int[] values)
        {
            var minX = int.MaxValue;
            var minY = int.MaxValue;
            for (var i = 0; i < cells.Length; i++)
            {
                minX = Mathf.Min(minX, cells[i].x);
                minY = Mathf.Min(minY, cells[i].y);
            }

            for (var i = 0; i < cells.Length; i++)
                cells[i] = new Vector2Int(cells[i].x - minX, cells[i].y - minY);

            Array.Sort<Vector2Int, int>(cells, values, RowMajor);
        }

        /// <summary>田字格判定：恰好 4 格且包围盒 2×2。</summary>
        static bool IsSquareBlock(Vector2Int[] cells)
        {
            if (cells == null || cells.Length != 4) return false;

            var maxX = 0;
            var maxY = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                maxX = Mathf.Max(maxX, cells[i].x);
                maxY = Mathf.Max(maxY, cells[i].y);
            }

            return maxX == 1 && maxY == 1;
        }

        static bool Fits(Vector2Int[] cells, int maxBounds)
        {
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].x >= maxBounds || cells[i].y >= maxBounds) return false;

            return true;
        }

        static string Key(Vector2Int[] cells)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cells.Length; i++)
                sb.Append(cells[i].x).Append(',').Append(cells[i].y).Append(';');

            return sb.ToString();
        }

        static int CompareShapes(Vector2Int[] a, Vector2Int[] b)
        {
            if (a.Length != b.Length) return a.Length.CompareTo(b.Length);

            return string.CompareOrdinal(Key(a), Key(b));
        }
    }
}
