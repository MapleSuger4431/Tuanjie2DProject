using System.Collections.Generic;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 鼠标虚影：按当前方块实例的形状，在悬停格上叠加半透明的格子美术——每一格显示它自己随机出来的状态，
    /// 玩家在落下之前就能看清哪格是火、哪格是冰、哪格是不能走的地面。
    /// 纯渲染组件，不读 Model，故不继承 Aesir View 基类。
    /// </summary>
    public sealed class TileGhostView : MonoBehaviour
    {
        [SerializeField] int sortingOrder = 10;

        [Tooltip("合法落点时虚影的不透明度")]
        [SerializeField, Range(0f, 1f)] float ghostAlpha = 0.6f;

        [Tooltip("整块非法（越界 / 压到山地或起终点）时整块染成这个颜色")]
        [SerializeField] Color invalidColor = new Color(1f, 0.25f, 0.25f, 0.6f);

        BoardGridView _grid;
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        Transform _root;

        void Awake()
        {
            _grid = GetComponent<BoardGridView>();

            var existing = transform.Find("GhostRoot");
            _root = existing != null ? existing : new GameObject("GhostRoot").transform;
            _root.SetParent(transform, false);
        }

        public void Show(Vector2Int anchor, TilePiece piece, bool valid)
        {
            if (_grid == null || piece == null) return;

            var cells = piece.Cells;
            EnsurePool(cells.Length);

            for (int i = 0; i < _pool.Count; i++)
            {
                var renderer = _pool[i];
                bool used = i < cells.Length;
                renderer.gameObject.SetActive(used);
                if (!used) continue;

                renderer.transform.localPosition = _grid.CellToLocal(anchor + cells[i]);

                var type = piece.TypeAt(i);
                var sprite = _grid.SpriteFor(type);
                if (renderer.sprite != sprite)
                {
                    renderer.sprite = sprite;
                    ApplyScale(renderer, sprite);
                }

                var tint = valid ? _grid.TintFor(type) : invalidColor;
                tint.a = valid ? ghostAlpha : invalidColor.a;
                renderer.color = tint;
            }
        }

        public void Hide()
        {
            for (int i = 0; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        }

        void ApplyScale(SpriteRenderer renderer, Sprite sprite)
        {
            if (renderer == null) return;

            float unit = sprite != null ? sprite.bounds.size.x : 1f;
            renderer.transform.localScale = Vector3.one * ((_grid != null ? _grid.CellSize : 1f) / unit);
        }

        void EnsurePool(int count)
        {
            while (_pool.Count < count) _pool.Add(CreateGhostCell());
        }

        SpriteRenderer CreateGhostCell()
        {
            var go = new GameObject("GhostCell");
            go.transform.SetParent(_root, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _grid != null ? _grid.SpriteFor(TileType.Void) : null;
            renderer.sortingOrder = sortingOrder;

            ApplyScale(renderer, renderer.sprite);
            return renderer;
        }
    }
}
