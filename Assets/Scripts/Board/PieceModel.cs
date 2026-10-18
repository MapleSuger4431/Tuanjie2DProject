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
        [Header("形状范围（格数上下限，4~6）")]
        [SerializeField] int minPieceCells = 4;
        [SerializeField] int maxPieceCells = TileShapeLibrary.MaxPieceCells;

        [Header("转向箭头（每块必带）")]
        [Tooltip("一块里箭头格的数量上限 = 块内格数 × 该比例（设计口径是不超过一半）")]
        [SerializeField, Range(0.1f, 0.5f)] float arrowRatio = 0.5f;

        [Header("属性反转格（随机附带）")]
        [Tooltip("每块额外附带 1 个属性反转格的概率；反转格只落在普通路格——火/冰元素格上 AI 属性不符时踏不进去，反转就会变成走到即判负的陷阱")]
        [SerializeField, Range(0f, 1f)] float invertChance = 0.3f;

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
            // 田字格（2×2 实心）设计上明确不要，形状库侧直接排除
            _shapes = TileShapeLibrary.CreatePolyominoes(minPieceCells, maxPieceCells,
                TileShapeLibrary.MaxBounds, excludeSquareBlock: true);
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
        /// 把当前手上这块顺时针旋转 90°（只转当前块，预告块不动）：格偏移与每格状态一起转。
        /// 换的是新实例，形状库里的原件不受影响。
        /// </summary>
        public void RotateCurrent()
        {
            var piece = current.Value;
            if (piece == null) return;

            current.Value = piece.RotateCW();
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

        /// <summary>
        /// 抽形状 + 按本关随机表给每一格各抽一个状态 + 决定每格的效果，包成一个方块实例。
        /// 效果层两条来源：本关在随机表里配了 effects 就按权重逐格抽；没配则走内置发牌
        /// （<see cref="RollPieceEffects" />：转向箭头必带规则 + 按 <see cref="invertChance" /> 概率附带反转格）。
        /// 反转格无论哪条来源都只允许落在普通路格，落点由 <see cref="SanitizeInverts" /> 统一兜底。
        /// </summary>
        TilePiece RandomPiece()
        {
            var shape = _shapes[UnityEngine.Random.Range(0, _shapes.Length)];
            var count = shape.Cells != null ? shape.Cells.Length : 0;
            var types = _pool != null
                ? _pool.RollTypes(count)
                : BoardTilePoolConfig.DefaultTypes(count);

            var effects = _pool != null && _pool.HasEffectPool
                ? _pool.RollEffects(count)
                : RollPieceEffects(count, types);

            SanitizeInverts(effects, types);

            return new TilePiece(shape, types, effects);
        }

        /// <summary>
        /// 转向箭头发牌规则：每块**必定**至少 1 个箭头格（前提是块内有可行走格）；上限 = 块内格数 × <see cref="arrowRatio" />（不超过一半）；
        /// 同一块内不允许出现互为反向的两个箭头（不对冲）——否则一块自己就把 AI 的转向抵消了。
        /// 箭头只落在**可行走格**（路/火/冰）：空地/山地 AI 踏不进去，钉了提示也永远不生效；
        /// 块内全是空地时本块就不带箭头（可行性优先于必带规则）。
        /// </summary>
        TileEffect[] RollArrows(int cellCount, TileType[] types)
        {
            var result = new TileEffect[cellCount];
            if (cellCount <= 0) return result;

            // 候选格只保留可行走状态（与视图侧 TileTypeInfo.IsHintAllowed 的空地/山地禁令同口径）
            var slots = new List<int>(cellCount);
            for (var i = 0; i < cellCount; i++)
            {
                if (types != null && i < types.Length && !TileTypeInfo.IsPassable(types[i])) continue;

                slots.Add(i);
            }

            if (slots.Count == 0) return result;

            var maxArrows = Mathf.Max(1, Mathf.FloorToInt(cellCount * arrowRatio));
            var arrowCount = UnityEngine.Random.Range(1, Mathf.Min(maxArrows, slots.Count) + 1);

            // 洗牌取出箭头落在哪几格：同一格只放一个效果，块内其余格保持无效果
            for (var i = slots.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                var temp = slots[i];
                slots[i] = slots[j];
                slots[j] = temp;
            }

            var used = new List<MoveDirection>(arrowCount);
            for (var i = 0; i < arrowCount; i++)
            {
                var direction = RollDirection(used);
                used.Add(direction);
                result[slots[i]] = ForcedTurnEffect.For(direction);
            }

            return result;
        }

        /// <summary>
        /// 内置效果发牌 = 转向箭头（<see cref="RollArrows" />，保证玩法推得动）+ 按 <see cref="invertChance" />
        /// 概率额外附带 1 个属性反转格。反转格只落在「还没效果且是普通路」的格子上：普通路对火/冰属性都放行，
        /// AI 踏上去反转只会帮忙不会挡路；一块最多 1 个（反多了会在块内来回翻）。
        /// </summary>
        TileEffect[] RollPieceEffects(int cellCount, TileType[] types)
        {
            var effects = RollArrows(cellCount, types);
            MaybeRollInvert(effects, types);
            return effects;
        }

        /// <summary>概率命中后挑一个空着的普通路格放反转；没有合适格子（或资产缺失）就跳过，本块不带反转。</summary>
        void MaybeRollInvert(TileEffect[] effects, TileType[] types)
        {
            if (effects == null || effects.Length == 0) return;
            if (invertChance <= 0f || UnityEngine.Random.value > invertChance) return;

            var slots = new List<int>(effects.Length);
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null) continue;
                if (types == null || i >= types.Length || types[i] != TileType.Land) continue;

                slots.Add(i);
            }

            if (slots.Count == 0) return;

            var invert = ElementSwitchEffect.Asset;
            if (invert == null) return;

            effects[slots[UnityEngine.Random.Range(0, slots.Count)]] = invert;
        }

        /// <summary>
        /// 反转格落点约束（用户拍板：反转格只能在普通的路上）：随机表路径逐格独立抽效果，可能把反转抽到火/冰格上——
        /// AI 属性不符时踏不进去、直行到那儿就判负，反转从帮助变成陷阱。凡不是普通路格上的反转一律清掉。
        /// </summary>
        static void SanitizeInverts(TileEffect[] effects, TileType[] types)
        {
            if (effects == null) return;

            for (var i = 0; i < effects.Length; i++)
            {
                if (!(effects[i] is ElementSwitchEffect)) continue;
                if (types == null || i >= types.Length || types[i] != TileType.Land) effects[i] = null;
            }
        }

        /// <summary>抽一个箭头方向：与已选方向互为反向的一律排除（不对冲），同向允许重复。</summary>
        static MoveDirection RollDirection(List<MoveDirection> used)
        {
            var candidates = new List<MoveDirection>(MoveDirectionInfo.Count);
            for (var i = 0; i < MoveDirectionInfo.Count; i++)
            {
                var direction = MoveDirectionInfo.All[i];
                if (used.Contains(MoveDirectionInfo.Opposite(direction))) continue;

                candidates.Add(direction);
            }

            if (candidates.Count == 0) return MoveDirectionInfo.All[0];

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
    }
}
