using System.Collections.Generic;
using UnityEngine;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 鼠标虚影：按当前方块形状，在悬停格上叠加半透明色块。
    /// 纯渲染组件，不读 Model，故不继承 Aesir View 基类。
    /// </summary>
    public sealed class TileGhostView : MonoBehaviour
    {
        [SerializeField] int sortingOrder = 10;

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

        public void Show(Vector2Int anchor, TileShape shape, Color tint)
        {
            if (_grid == null || shape == null) return;

            var cells = shape.Cells;
            EnsurePool(cells.Length);

            for (int i = 0; i < _pool.Count; i++)
            {
                var renderer = _pool[i];
                bool used = i < cells.Length;
                renderer.gameObject.SetActive(used);
                if (!used) continue;

                renderer.transform.localPosition = _grid.CellToLocal(anchor + cells[i]);
                renderer.color = tint;
            }
        }

        public void Hide()
        {
            for (int i = 0; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
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
            renderer.sprite = _grid != null ? _grid.CellSprite : null;
            renderer.sortingOrder = sortingOrder;

            float unit = renderer.sprite != null ? renderer.sprite.bounds.size.x : 1f;
            go.transform.localScale = Vector3.one * ((_grid != null ? _grid.CellSize : 1f) / unit);
            return renderer;
        }
    }
}
