using System.Collections.Generic;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// 棋盘渲染：格子的唯一容器是 <see cref="tilesRoot" /> 父对象，坐标以棋盘左下角为原点（0,0 格在父对象原点，向右上增长）。
    /// 格子优先复用父对象下已有的 <see cref="BoardTile" />（编辑器工具生成、状态已配好的那些），缺失才补建——场景配置即关卡真源。
    /// 铺设结果由 BoardModel 的状态层驱动刷新：预设状态写 BoardTile 的预设字段，运行期覆盖只改显示不落盘。
    /// </summary>
    public sealed class BoardGridView : MonoViewController<BoardContext>
    {
        [SerializeField] Sprite cellSprite;

        [Tooltip("索引与 TileType 数值对齐：0 空 / 1 路 / 2 火 / 3 冰 / 4 山地 / 5 起点 / 6 终点。起点与终点没配专属美术时回落路面贴图并染分类色")]
        [SerializeField] Sprite[] tileSprites = new Sprite[7];

        [SerializeField] float cellSize = 0.92f;
        [SerializeField] float spacing = 0.08f;
        [SerializeField] int sortingOrder = 0;

        [Tooltip("承载全部格子的父对象；为空时自动取/建名为 Tiles 的子物体")]
        [SerializeField] Transform tilesRoot;

        [Tooltip("棋盘边长（规整正方形）；生成工具写入，运行时同步给 BoardModel")]
        [SerializeField] int boardSize = BoardModel.MinSize;

        [Tooltip("起点格（关卡画笔写入）；越界或未配置回落左下角，运行时同步给 BoardModel")]
        [SerializeField] Vector2Int startCell = new Vector2Int(0, 0);

        [Tooltip("终点格（关卡画笔写入）；(-1,-1) 未配置 = 右上角，运行时同步给 BoardModel")]
        [SerializeField] Vector2Int goalCell = new Vector2Int(-1, -1);

        BoardModel _board;
        BoardTile[] _tiles;

        public float Step => cellSize + spacing;
        public float CellSize => cellSize;
        public Sprite CellSprite => cellSprite;

        /// <summary>按格子状态取贴图：有专属美术就用美术，否则回落纯色 cell（贴图整套未配时的兜底）。</summary>
        public Sprite SpriteFor(TileType type) => ArtFor(type) ?? cellSprite;

        /// <summary>有专属美术的格子走原色显示，没配到美术的（含没配专属图的起点/终点）走分类色染色。</summary>
        public Color TintFor(TileType type) =>
            HasOwnArt(type) ? Color.white : TileTypeInfo.Color(type);

        /// <summary>取该状态的贴图，索引与 TileType 数值一一对应；起点与终点没配专属美术时回落路面贴图。</summary>
        Sprite ArtFor(TileType type)
        {
            if (tileSprites == null) return null;

            int index = (int)type;
            if (index >= 0 && index < tileSprites.Length && tileSprites[index] != null)
                return tileSprites[index];

            return TileTypeInfo.IsTerminal(type) && tileSprites.Length > (int)TileType.Land
                ? tileSprites[(int)TileType.Land]
                : null;
        }

        /// <summary>该状态有没有自己的美术：索引位上有贴图才算，起点/终点同样按 5 / 6 位判定。</summary>
        bool HasOwnArt(TileType type)
        {
            if (tileSprites == null) return false;

            int index = (int)type;
            return index >= 0 && index < tileSprites.Length && tileSprites[index] != null;
        }

        /// <summary>
        /// 按当前贴图配置刷新全部格子的精灵与缩放（编辑器与运行时通用）。
        /// 缩放必须跟着贴图尺寸走——贴图从 32px 纯色换成 315px 美术后，沿用旧 scale 会让格子放大到互相重叠。
        /// </summary>
        public void ApplyTileSprites()
        {
            EnsureRoot();
            if (tilesRoot == null) return;

            foreach (var tile in tilesRoot.GetComponentsInChildren<BoardTile>(true))
                ApplyTileSprite(tile);
        }

        /// <summary>给单个格子套上当前状态的贴图、缩放与染色。</summary>
        void ApplyTileSprite(BoardTile tile)
        {
            if (tile == null) return;

            var sprite = SpriteFor(tile.CurrentType);
            var renderer = tile.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
                if (sprite != null)
                    tile.transform.localScale = Vector3.one * (cellSize / SpriteUnitSize(sprite));
            }

            tile.ApplyVisual();
        }

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

        /// <summary>
        /// 按坐标取格子视图（供 <see cref="BoardRoot" /> 拉关卡预置效果）。
        /// 越界或尚未 <see cref="Build" />（_tiles 未建好）时返回 null。
        /// </summary>
        public BoardTile TileAt(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= Width || cell.y < 0 || cell.y >= Height) return null;
            if (_tiles == null || _tiles.Length != Width * Height) return null;

            return _tiles[cell.y * Width + cell.x];
        }

        /// <summary>起点（关卡可配，画笔写入）；未配置或越界回落左下角。</summary>
        public Vector2Int StartCell
        {
            get
            {
                var inBounds = startCell.x >= 0 && startCell.x < Width && startCell.y >= 0 && startCell.y < Height;
                return inBounds ? startCell : Vector2Int.zero;
            }
        }

        /// <summary>终点（关卡可配，画笔写入）；未配置、越界或与起点重合回落右上角。</summary>
        public Vector2Int GoalCell
        {
            get
            {
                var inBounds = goalCell.x >= 0 && goalCell.x < Width && goalCell.y >= 0 && goalCell.y < Height;
                return inBounds && goalCell != StartCell ? goalCell : new Vector2Int(Width - 1, Height - 1);
            }
        }

        /// <summary>写入起终点配置（关卡画笔 / 生成工具入口）；随场景序列化，运行时在 <see cref="Start" /> 下发数据层。</summary>
        public void SetTerminals(Vector2Int start, Vector2Int goal)
        {
            startCell = start;
            goalCell = goal;
        }

        void OnValidate()
        {
            // 旧场景反序列化出的默认值归一成「未配置」语义，运行时按角位兜底
            if (startCell.x < 0 || startCell.x >= Width || startCell.y < 0 || startCell.y >= Height)
                startCell = Vector2Int.zero;

            if (goalCell.x < 0 || goalCell.x >= Width || goalCell.y < 0 || goalCell.y >= Height || goalCell == startCell)
                goalCell = new Vector2Int(-1, -1);
        }

        void Start()
        {
            _board = this.GetModel<BoardModel>();

            Build();

            if (_board != null)
            {
                if (_board.Width != boardSize || _board.Height != boardSize)
                    _board.SetSize(boardSize, boardSize);

                // 起终点按关卡配置下发数据层（旧场景未配置时回落角位，等价旧行为）；
                // 配置变化会触发 TerminalsChanged → 运行模型把 AI 放回新起点
                _board.SetStartGoal(StartCell, GoalCell);

                _board.ApplyPreset(CollectPreset(), CollectEffectPreset());
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
            _board.ApplyPreset(CollectPreset(), CollectEffectPreset());
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
                    tile.Configure(coord, tile.PresetType);
                    LayoutTile(tile);
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

        /// <summary>按给定边长整块重建（清空后重铺，起点/终点按配置或回落左下/右上）。编辑器生成工具入口。</summary>
        public void BuildTiles(int size) =>
            BuildTiles(size, Vector2Int.zero, new Vector2Int(size - 1, size - 1));

        /// <summary>带起终点配置的重建（关卡画笔用）：配置先落序列化字段，格子预设按其写入。</summary>
        public void BuildTiles(int size, Vector2Int start, Vector2Int goal)
        {
            boardSize = Mathf.Clamp(size, BoardModel.MinSize, BoardModel.MaxSize);
            SetTerminals(start, goal);

            // 调用方传了重合/越界配置时兜底回角位，保证重建出的棋盘始终有且只有一对起终点
            var from = StartCell;
            var to = goal == from ? new Vector2Int(boardSize - 1, boardSize - 1) : GoalCell;

            EnsureRoot();
            ClearTiles();

            for (int y = 0; y < boardSize; y++)
            {
                for (int x = 0; x < boardSize; x++)
                {
                    var coord = new Vector2Int(x, y);
                    var preset = coord == from ? TileType.Start : (coord == to ? TileType.Goal : TileType.Void);
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

        /// <summary>
        /// 编辑器工具入口：把 Tiles 下全部既有格子按游戏运行时的布局重新摆放（位置 / 缩放 / 贴图 / 排序），
        /// 不增删格子、不改状态与效果。摆完保存场景，编辑器里打开关卡即可直接看到棋盘排布，便于调试。
        /// 与 <see cref="Build" /> 共用同一份 <see cref="LayoutTile" />，运行时再摆一遍结果不变。
        /// </summary>
        public void LayoutTiles()
        {
            EnsureRoot();
            if (tilesRoot == null) return;

            foreach (var tile in tilesRoot.GetComponentsInChildren<BoardTile>(true))
                LayoutTile(tile);
        }

        /// <summary>把单个格子摆到运行时位置并套上贴图/缩放/排序——编辑器与运行时共用的唯一布局逻辑。</summary>
        void LayoutTile(BoardTile tile)
        {
            if (tile == null) return;

            tile.transform.localPosition = CellToLocal(tile.Cell);
            ApplyTileSprite(tile);
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

        /// <summary>
        /// 收集关卡预置的效果层（各格 Inspector 里拖的效果资产），喂给 BoardModel 作为效果层初值。
        /// 与 <see cref="CollectPreset" /> 平行：一层通行状态、一层效果，互不影响。
        /// </summary>
        public List<KeyValuePair<Vector2Int, TileEffect>> CollectEffectPreset()
        {
            var preset = new List<KeyValuePair<Vector2Int, TileEffect>>();

            if (_tiles != null)
            {
                foreach (var tile in _tiles)
                {
                    if (tile != null)
                        preset.Add(new KeyValuePair<Vector2Int, TileEffect>(tile.Cell, tile.PresetEffect));
                }

                return preset;
            }

            if (tilesRoot != null)
            {
                foreach (var tile in tilesRoot.GetComponentsInChildren<BoardTile>(true))
                {
                    if (tile != null)
                        preset.Add(new KeyValuePair<Vector2Int, TileEffect>(tile.Cell, tile.PresetEffect));
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
            // 新建即落位：编辑器工具重建后场景序列化的就是游戏中的排列，不必等运行时 Build
            go.transform.localPosition = CellToLocal(coord);

            var renderer = go.AddComponent<SpriteRenderer>();
            var sprite = SpriteFor(preset);
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            go.transform.localScale = Vector3.one * (cellSize / SpriteUnitSize(sprite));

            var tile = go.AddComponent<BoardTile>();
            tile.Configure(coord, preset);
            return tile;
        }

        // new：隐藏 UnityEngine.Object 的同名静态方法，避免 CS0108
        static new void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        static float SpriteUnitSize(Sprite sprite) => sprite == null ? 1f : sprite.bounds.size.x;
    }
}
