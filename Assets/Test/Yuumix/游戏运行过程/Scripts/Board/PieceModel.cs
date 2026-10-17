using System;
using System.Collections.Generic;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 当前待放置方块 + 下一块预告。形状按均匀权重随机抽取（后续要接权重表 / 上限计数就在这里改）。
    /// 两块都由 Model 持有，UI 只订阅，不自行推算。
    /// </summary>
    [Serializable]
    public sealed class PieceModel : AbstractModel
    {
        [SerializeField] ObservableValue<TileShape> current = new ObservableValue<TileShape>(null);
        [SerializeField] ObservableValue<TileShape> next = new ObservableValue<TileShape>(null);

        TileShape[] _shapes;
        BoardModel _board;

        public ObservableValue<TileShape> Current => current;

        /// <summary>下一块预告（右侧面板预览的真源）。</summary>
        public ObservableValue<TileShape> Next => next;

        public IReadOnlyList<TileShape> Shapes => _shapes;

        protected override void OnInitialize()
        {
            _board = this.GetModel<BoardModel>();
            _shapes = TileShapeLibrary.CreateTetrominoes();
            next.Value = RandomShape();
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
            next.Value = HasNextPiece ? RandomShape() : null;
        }

        TileShape RandomShape() => _shapes[UnityEngine.Random.Range(0, _shapes.Length)];
    }
}
