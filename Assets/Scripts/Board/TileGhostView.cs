using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 鼠标虚影：按当前方块实例的形状，在悬停格上叠加半透明的格子美术——每一格显示它自己随机出来的状态，
    /// 带转向箭头的格同时叠出箭头；整块再用一圈**沿形状外轮廓**的边框围起来，
    /// 玩家落下之前就能看清这块占哪几格、哪几格会让 AI 转向。
    /// 纯渲染组件，不读 Model，故不继承 Aesir View 基类。
    /// </summary>
    public sealed class TileGhostView : MonoBehaviour
    {
        [SerializeField] int sortingOrder = 10;

        [Tooltip("合法落点时虚影的不透明度")]
        [SerializeField, Range(0f, 1f)] float ghostAlpha = 0.6f;

        [Tooltip("整块非法（越界 / 压到山地或起终点）时整块染成这个颜色")]
        [SerializeField] Color invalidColor = new Color(1f, 0.25f, 0.25f, 0.6f);

        [Header("落块边框")]
        [Tooltip("合法落点时边框的颜色")]
        [SerializeField] Color outlineColor = new Color(1f, 1f, 1f, 0.95f);

        [Tooltip("边框粗细（相对格子尺寸的比例）")]
        [SerializeField, Range(0.02f, 0.4f)] float outlineWidth = 0.1f;

        BoardGridView _grid;
        readonly List<GhostCell> _pool = new List<GhostCell>();
        readonly List<SpriteRenderer> _edges = new List<SpriteRenderer>();
        Transform _root;
        Transform _outlineRoot;
        Sprite _edgeSprite;

        void Awake()
        {
            _grid = GetComponent<BoardGridView>();

            var existing = transform.Find("GhostRoot");
            _root = existing != null ? existing : new GameObject("GhostRoot").transform;
            _root.SetParent(transform, false);

            var outlineExisting = _root.Find("GhostOutline");
            _outlineRoot = outlineExisting != null ? outlineExisting : new GameObject("GhostOutline").transform;
            _outlineRoot.SetParent(_root, false);
        }

        public void Show(Vector2Int anchor, TilePiece piece, bool valid)
        {
            if (_grid == null || piece == null)
            {
                Hide();
                return;
            }

            var cells = piece.Cells;
            ShowCells(anchor, piece, cells, valid);
            ShowOutline(anchor, cells, valid);
        }

        public void Hide()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                _pool[i].cell.gameObject.SetActive(false);
                if (_pool[i].arrow != null) _pool[i].arrow.gameObject.SetActive(false);
            }

            for (int i = 0; i < _edges.Count; i++) _edges[i].gameObject.SetActive(false);
        }

        void ShowCells(Vector2Int anchor, TilePiece piece, Vector2Int[] cells, bool valid)
        {
            EnsurePool(cells.Length);

            for (int i = 0; i < _pool.Count; i++)
            {
                var entry = _pool[i];
                bool used = i < cells.Length;
                entry.cell.gameObject.SetActive(used);
                if (entry.arrow != null) entry.arrow.gameObject.SetActive(false);
                if (!used) continue;

                var local = _grid.CellToLocal(anchor + cells[i]);
                entry.cell.transform.localPosition = local;

                var type = piece.TypeAt(i);
                var sprite = _grid.SpriteFor(type);
                if (entry.cell.sprite != sprite)
                {
                    entry.cell.sprite = sprite;
                    ApplyCellScale(entry.cell, sprite);
                }

                var tint = valid ? _grid.TintFor(type) : invalidColor;
                tint.a = valid ? ghostAlpha : invalidColor.a;
                entry.cell.color = tint;

                // 该格带的转向箭头：跟着虚影一起预览，落下前就能看出 AI 会在哪拐弯；
                // 空地/山地这类 AI 踏不进的格子不放提示（与棋盘格上的显示规则一致）
                var icon = piece.EffectAt(i)?.BoardIcon;
                if (icon == null || !TileTypeInfo.IsHintAllowed(type) || entry.arrow == null) continue;

                entry.arrow.sprite = icon;
                entry.arrow.color = valid ? new Color(1f, 1f, 1f, ghostAlpha) : invalidColor;
                // 箭头是 GhostCell 的子物体：父级已经在格心，这里相对父级归零即可；
                // 写成格心坐标会在缩放过的父级坐标系里叠出 1+scale 倍的漂移
                entry.arrow.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                var unit = icon.bounds.size.x > 0f ? icon.bounds.size.x : 1f;
                // 提示图标世界尺寸 = 格子尺寸（满格，与落盘后的棋盘显示一致）；
                // 箭头挂在按格子缩放过的 GhostCell 下面，必须补偿父级缩放，否则只有格子的三成大
                var parentScale = entry.arrow.transform.parent != null
                    ? entry.arrow.transform.parent.lossyScale.x
                    : 1f;
                if (parentScale > 1e-4f)
                    entry.arrow.transform.localScale =
                        Vector3.one * (_grid.CellSize / (unit * parentScale));
                entry.arrow.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 沿形状外轮廓描边：逐格看四个方向，邻格不在本块里就沿那条边画一根——
        /// 于是 L 形、T 形这类异形块描出来的是它自己的轮廓，而不是外接矩形。
        /// </summary>
        void ShowOutline(Vector2Int anchor, Vector2Int[] cells, bool valid)
        {
            var occupied = new HashSet<Vector2Int>();
            for (int i = 0; i < cells.Length; i++) occupied.Add(anchor + cells[i]);

            var step = _grid.Step;
            var thickness = _grid.CellSize * outlineWidth;
            var color = valid ? outlineColor : invalidColor;
            var used = 0;

            for (int i = 0; i < cells.Length; i++)
            {
                var cell = anchor + cells[i];
                var center = _grid.CellToLocal(cell);

                for (int d = 0; d < Neighbors.Length; d++)
                {
                    if (occupied.Contains(cell + Neighbors[d])) continue;

                    var renderer = EnsureEdge(used++);
                    var offset = Neighbors[d];

                    // 边贴在该格朝外那一侧；长度补一个厚度，四角自然闭合
                    renderer.transform.localPosition =
                        new Vector3(center.x + offset.x * step * 0.5f, center.y + offset.y * step * 0.5f, -0.02f);
                    renderer.transform.localRotation = offset.x != 0
                        ? Quaternion.Euler(0f, 0f, 90f)
                        : Quaternion.identity;
                    renderer.transform.localScale = new Vector3(step + thickness, thickness, 1f);
                    renderer.color = color;
                    renderer.gameObject.SetActive(true);
                }
            }

            for (int i = used; i < _edges.Count; i++) _edges[i].gameObject.SetActive(false);
        }

        void ApplyCellScale(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer == null) return;

            float unit = sprite != null ? sprite.bounds.size.x : 1f;
            renderer.transform.localScale = Vector3.one * ((_grid != null ? _grid.CellSize : 1f) / unit);
        }

        void EnsurePool(int count)
        {
            while (_pool.Count < count) _pool.Add(CreateGhostCell());
        }

        GhostCell CreateGhostCell()
        {
            var go = new GameObject("GhostCell");
            go.transform.SetParent(_root, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _grid != null ? _grid.SpriteFor(TileType.Void) : null;
            renderer.sortingOrder = sortingOrder;
            ApplyCellScale(renderer, renderer.sprite);

            var arrowObject = new GameObject("Arrow");
            arrowObject.transform.SetParent(go.transform, false);

            var arrow = arrowObject.AddComponent<SpriteRenderer>();
            arrow.sortingOrder = sortingOrder + 1;
            arrow.gameObject.SetActive(false);

            return new GhostCell { cell = renderer, arrow = arrow };
        }

        SpriteRenderer EnsureEdge(int index)
        {
            while (_edges.Count <= index)
            {
                var go = new GameObject("Edge");
                go.transform.SetParent(_outlineRoot, false);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = EdgeSprite;
                renderer.sortingOrder = sortingOrder + 2;
                renderer.gameObject.SetActive(false);

                _edges.Add(renderer);
            }

            return _edges[index];
        }

        /// <summary>边框用的纯白块：1×1 单位的精灵，靠 scale 拉成任意长宽的细条。</summary>
        Sprite EdgeSprite
        {
            get
            {
                if (_edgeSprite != null) return _edgeSprite;

                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color[16];
                for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                texture.SetPixels(pixels);
                texture.Apply(false, true);

                // pixelsPerUnit 取纹理宽 → bounds 恰为 1 个单位，scale 直接当尺寸用
                _edgeSprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                _edgeSprite.name = "GhostEdge";
                return _edgeSprite;
            }
        }

        static readonly Vector2Int[] Neighbors =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right,
        };

        struct GhostCell
        {
            public SpriteRenderer cell;
            public SpriteRenderer arrow;
        }
    }
}
