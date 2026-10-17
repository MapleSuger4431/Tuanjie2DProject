using System.Collections.Generic;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 棋盘渲染：格子的唯一容器是 <see cref="tilesRoot" /> 父对象，坐标以棋盘左下角为原点（0,0 格在父对象原点，向右上增长）。
    /// 格子优先复用父对象下已有的 <see cref="BoardTile" />（编辑器工具生成、状态已配好的那些），缺失才补建——场景配置即关卡真源。
    /// 铺设结果由 BoardModel 的状态层驱动刷新：预设状态写 BoardTile 的预设字段，运行期覆盖只改显示不落盘。
    /// </summary>
    public sealed class BoardGridView : MonoViewController<BoardContext>
    {
        [SerializeField] Sprite cellSprite;
        [SerializeField] float cellSize = 0.92f;
        [SerializeField] float spacing = 0.08f;
        [SerializeField] int sortingOrder = 0;

        [Tooltip("承载全部格子的父对象；为空时自动取/建名为 Tiles 的子物体")]
        [SerializeField] Transform tilesRoot;

        [Tooltip("棋盘边长（规整正方形）；生成工具写入，运行时同步给 BoardModel")]
        [SerializeField] int boardSize = BoardModel.MinSize;

        BoardModel _board;
        BoardTile[] _tiles;

        public float Step => cellSize + spacing;
        public float CellSize => cellSize;
        public Sprite CellSprite => cellSprite;

        public int BoardSize
        {
            get => boardSize;
            set => boardSize = Mathf.Clamp(value, BoardModel.MinSize, BoardModel.MaxSize);
        }

        public int Width => boardSize;
        public int Height => boardSize;

        /// <summary>棋盘包围盒的世界宽度（含最外格自身尺寸），供相机适配计算。</summary>
        public float BoardWorldWidth => (Width - 1) * Step + cellSize;

        /// <summary>棋盘包围盒的世界高度，供相机适配计算。</summary>
        public float BoardWorldHeight => (Height - 1) * Step + cellSize;

        /// <summary>棋盘几何中心的局部坐标：原点改到左下角后，相机要对齐的是这里而不是父对象原点。</summary>
        public Vector2 BoardCenterLocal =>
            new Vector2((Width - 1) * Step * 0.5f, (Height - 1) * Step * 0.5f);

        /// <summary>(0,0) 格即父对象原点，向右上增长。</summary>
        public Vector2 Origin => Vector2.zero;

        /// <summary>起点：左下角。</summary>
        public Vector2Int StartCell => Vector2Int.zero;

        /// <summary>终点：右上角。</summary>
        public Vector2Int GoalCell => new Vector2Int(Width - 1, Height - 1);

        void Start()
        {
            _board = this.GetModel<BoardModel>();

            Build();

            if (_board != null)
            {
                if (_board.Width != boardSize || _board.Height != boardSize)
                    _board.SetSize(boardSize, boardSize);

                _board.ApplyPreset(CollectPreset());
                Refresh();

                _board.BoardCovered.AddListener(Refresh)
                    .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
                _board.BoardResized.AddListener(OnBoardResized)
                    .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            }
            else
            {
                Refresh();
            }
        }

        void OnBoardResized()
        {
            if (_board == null) return;
            boardSize = _board.Width;
            Build();
            _board.ApplyPreset(CollectPreset());
            Refresh();
        }

        /// <summary>按当前尺寸绑定/补齐格子，并清掉范围内的多余格子。</summary>
        public void Build()
        {
            EnsureRoot();

            var map = new Dictionary<Vector2Int, BoardTile>();
            foreach (var tile in tilesRoot.GetComponentsInChildren<BoardTile>(true))
            {
                if (tile != null && !map.ContainsKey(tile.Cell)) map.Add(tile.Cell, tile);
            }

            _tiles = new BoardTile[Width * Height];

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var coord = new Vector2Int(x, y);
                    if (!map.TryGetValue(coord, out var tile) || tile == null)
                        tile = CreateTile(coord, TileType.Void);
                    else
                        map.Remove(coord);

                    tile.transform.SetParent(tilesRoot, false);
                    tile.transform.localPosition = CellToLocal(coord);
                    tile.Configure(coord, tile.PresetType);
                    _tiles[y * Width + x] = tile;
                }
            }

            foreach (var leftover in map.Values)
            {
                if (leftover != null) DestroyObject(leftover.gameObject);
            }

            // 历史遗留：只有 SpriteRenderer、没挂 BoardTile 的旧 Cell 物体
            for (int i = tilesRoot.childCount - 1; i >= 0; i--)
            {
                var child = tilesRoot.GetChild(i);
                if (child.name == "Cell" && child.GetComponent<BoardTile>() == null)
                    DestroyObject(child.gameObject);
            }
        }

        /// <summary>按给定边长整块重建（清空后重铺，起点/终点自动落在左下与右上）。编辑器生成工具入口。</summary>
        public void BuildTiles(int size)
        {
            boardSize = Mathf.Clamp(size, BoardModel.MinSize, BoardModel.MaxSize);
            EnsureRoot();
            ClearTiles();

            var goal = new Vector2Int(boardSize - 1, boardSize - 1);
            for (int y = 0; y < boardSize; y++)
            {
                for (int x = 0; x < boardSize; x++)
                {
                    var coord = new Vector2Int(x, y);
                    var preset = coord == Vector2Int.zero
                        ? TileType.Start
                        : (coord == goal ? TileType.Goal : TileType.Void);
                    CreateTile(coord, preset);
                }
            }

            _tiles = null;
        }

        /// <summary>清空父对象下全部格子。</summary>
        public void ClearTiles()
        {
            EnsureRoot();
            for (int i = tilesRoot.childCount - 1; i >= 0; i--)
                DestroyObject(tilesRoot.GetChild(i).gameObject);
            _tiles = null;
        }

        /// <summary>收集关卡预设状态（各格 Inspector 里配的），喂给 BoardModel 作为初始状态。</summary>
        public List<KeyValuePair<Vector2Int, TileType>> CollectPreset()
        {
            var preset = new List<KeyValuePair<Vector2Int, TileType>>();

            if (_tiles != null)
            {
                foreach (var tile in _tiles)
                {
                    if (tile != null)
                        preset.Add(new KeyValuePair<Vector2Int, TileType>(tile.Cell, tile.PresetType));
                }
                return preset;
            }

            if (tilesRoot != null)
            {
                foreach (var tile in tilesRoot.GetComponentsInChildren<BoardTile>(true))
                {
                    if (tile != null)
                        preset.Add(new KeyValuePair<Vector2Int, TileType>(tile.Cell, tile.PresetType));
                }
            }

            return preset;
        }

        /// <summary>按 BoardModel 状态层刷新每格显示；没有 Model 时回落关卡预设。</summary>
        public void Refresh()
        {
            if (_tiles == null || _tiles.Length != Width * Height) Build();

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var tile = _tiles[y * Width + x];
                    if (tile == null) continue;

                    var coord = new Vector2Int(x, y);
                    tile.SetRuntimeType(_board != null ? (TileType?)_board.TypeAt(coord) : null);
                }
            }
        }

        public Vector2 CellToLocal(Vector2Int cell) =>
            Origin + new Vector2(cell.x * Step, cell.y * Step);

        /// <summary>世界坐标 → 最近格（先转到本物体局部空间，再按步长取整）。</summary>
        public Vector2Int WorldToCell(Vector2 world) =>
            LocalToCell(transform.InverseTransformPoint(world));

        public Vector2Int LocalToCell(Vector2 local) =>
            new Vector2Int(
                Mathf.RoundToInt((local.x - Origin.x) / Step),
                Mathf.RoundToInt((local.y - Origin.y) / Step));

        void EnsureRoot()
        {
            if (tilesRoot != null) return;

            var existing = transform.Find("Tiles");
            tilesRoot = existing != null ? existing : new GameObject("Tiles").transform;
            tilesRoot.SetParent(transform, false);
            tilesRoot.localPosition = Vector3.zero;
            tilesRoot.localRotation = Quaternion.identity;
            tilesRoot.localScale = Vector3.one;
        }

        BoardTile CreateTile(Vector2Int coord, TileType preset)
        {
            var go = new GameObject($"Cell_{coord.x}_{coord.y}");
            go.transform.SetParent(tilesRoot, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = cellSprite;
            renderer.sortingOrder = sortingOrder;
            go.transform.localScale = Vector3.one * (cellSize / SpriteUnitSize(cellSprite));

            var tile = go.AddComponent<BoardTile>();
            tile.Configure(coord, preset);
            return tile;
        }

        static void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        static float SpriteUnitSize(Sprite sprite) => sprite == null ? 1f : sprite.bounds.size.x;
    }
}
