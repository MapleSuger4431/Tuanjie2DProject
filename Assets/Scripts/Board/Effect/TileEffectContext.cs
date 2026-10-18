using UnityEngine;

namespace Game.Board
{
    /// <summary>AI 的只读快照：效果据此判断，但不能改它。</summary>
    public struct AiPawnState
    {
        /// <summary>AI 当前所在格（也就是刚踏入、触发本效果的那一格）。</summary>
        public Vector2Int Cell;

        /// <summary>来路格；等于 <see cref="Cell" /> 时表示「无来路」（刚出发或刚被传送）。</summary>
        public Vector2Int PreviousCell;

        /// <summary>本步选用的方向；尚未移动过为 null。</summary>
        public MoveDirection? LastDirection;

        /// <summary>已走步数。</summary>
        public int StepCount;

        /// <summary>AI 当前元素属性（火 / 冰）：属性转换类效果据此算目标值。</summary>
        public ElementState Element;
    }

    /// <summary>
    /// 效果执行后回填的结果。效果只负责填，<see cref="BoardRunModel" /> 统一应用——
    /// 效果里不许直接改棋盘或改 AI 位置。
    /// </summary>
    public sealed class TileEffectResult
    {
        /// <summary>传送落点；传送不计步数，落点不再结算效果（一步最多一个效果）。</summary>
        public Vector2Int? TeleportTo;

        /// <summary>强制下一步朝向：优先级高于方向偏好，且允许破例走回来路格。</summary>
        public MoveDirection? ForcedNext;

        /// <summary>清空来路（传送后 AI 可朝任意方向走，含回头）。</summary>
        public bool ClearIncoming;

        /// <summary>立即停下（判负）。</summary>
        public bool Stop;

        /// <summary>元素属性转换目标：踏入本格后 AI 属性变成该值（反转效果按快照算好再填）；null 表示不变。</summary>
        public ElementState? SetElement;

        public bool HasAny => TeleportTo.HasValue || ForcedNext.HasValue || ClearIncoming || Stop || SetElement.HasValue;

        public void Clear()
        {
            TeleportTo = null;
            ForcedNext = null;
            ClearIncoming = false;
            Stop = false;
            SetElement = null;
        }
    }

    /// <summary>
    /// 传给效果的上下文：棋盘根 <see cref="BoardRoot" /> 是跨格联动的唯一入口，
    /// 效果不持任何格子引用，需要查别的格一律走它。
    /// </summary>
    public readonly struct TileEffectContext
    {
        public readonly BoardRoot Board;
        public readonly Vector2Int Cell;
        public readonly AiPawnState Pawn;
        public readonly TileEffectResult Result;

        public TileEffectContext(BoardRoot board, Vector2Int cell, AiPawnState pawn, TileEffectResult result)
        {
            Board = board;
            Cell = cell;
            Pawn = pawn;
            Result = result;
        }
    }
}
