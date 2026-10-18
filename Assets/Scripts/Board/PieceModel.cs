using System;
using System.Collections.Generic;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// 当前待放置方块 + 下一块预告。形状按均匀权重随机抽取；形状定下来后，块内每一格再按本关随机表
    /// （<see cref="BoardTilePoolModel" />）独立抽一个状态——所以同一块里可以同时有空地 / 冰 / 火 / 路 / 山地。
    /// 两块都由 Model 持有，UI 只订阅，不自行推算。
    /// </summary>
    [Serializable]
    public sealed class PieceModel : AbstractModel
    {
        [Header("形状范围（格数上下限，2~5）")]
        [SerializeField] int minPieceCells = 2;
        [SerializeField] int maxPieceCells = TileShapeLibrary.MaxPieceCells;

        [SerializeField] ObservableValue<TilePiece> current = new ObservableValue<TilePiece>(null);
        [SerializeField] ObservableValue<TilePiece> next = new ObservableValue<TilePiece>(null);

        TileShape[] _shapes;
        BoardModel _board;
        BoardTilePoolModel _pool;

        public ObservableValue<TilePiece> Current => current;

        /// <summary>下一块预告（右侧面板预览的真源）。</summary>
        public ObservableValue<TilePiece> Next => next;

        public IReadOnlyList<TileShape> Shapes => _shapes;

        protected override void OnInitialize()
        {
            _board = this.GetModel<BoardModel>();
            _pool = this.GetModel<BoardTilePoolModel>();
            _shapes = TileShapeLibrary.CreatePolyominoes(minPieceCells, maxPieceCells);
            next.Value = RandomPiece();
            RollNew();
        }

        /// <summary>本关还能再发几块：当前块用掉后是否还有下一块（上限由棋盘尺寸给出）。</summary>
        bool HasNextPiece => _board == null || _board.CoveredCount.Value + 1 < _board.PieceLimit;

        /// <summary>
        /// 把预告块提为当前块，并补抽下一块（放置成功后调用）。
        /// 达到本关方块上限后不再补抽：当前块放完即变 null，虚影隐藏、点击失效。
        /// </summary>
        public void RollNew()
        {
            if (_shapes == null || _shapes.Length == 0) return;

            current.Value = next.Value;
            next.Value = HasNextPiece ? RandomPiece() : null;
        }

        /// <summary>
        /// 重新抽取当前块与下一块（关卡重开用）：先给预告位补一块，再走一次 <see cref="RollNew" /> 把它提为当前块。
        /// 调用前必须已把 CoveredCount 归零，否则 HasNextPiece 会误判「已达上限」而抽不出下一块。
        /// </summary>
        public void Reroll()
        {
            if (_shapes == null || _shapes.Length == 0) return;

            next.Value = RandomPiece();
            RollNew();
        }

        /// <summary>抽形状 + 按本关随机表给每一格各抽一个状态，包成一个方块实例。</summary>
        TilePiece RandomPiece()
        {
            var shape = _shapes[UnityEngine.Random.Range(0, _shapes.Length)];
            var types = _pool != null
                ? _pool.RollTypes(shape.Cells != null ? shape.Cells.Length : 0)
                : BoardTilePoolConfig.DefaultTypes(shape.Cells != null ? shape.Cells.Length : 0);

            return new TilePiece(shape, types);
        }
    }
}
