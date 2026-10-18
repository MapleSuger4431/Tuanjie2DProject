using System;
using System.Collections.Generic;
using UnityEngine;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// AI 的方向偏好顺序（岔路口决策唯一依据）：四方向的**有序列表**，靠前者优先。
    /// 默认 右 → 左 → 上 → 下；铺路阶段可在 UI 上自由调序，点 Play 进入运行阶段后锁定为只读。
    /// 调序一律走 <see cref="MoveUp" /> / <see cref="MoveDown" /> / <see cref="MoveToTop" />，改完发 <see cref="OrderChanged" /> 驱动 UI 重建行。
    /// </summary>
    [Serializable]
    public sealed class AiPreferenceModel : AbstractModel
    {
        [Tooltip("方向优先级顺序，索引越小越优先")]
        [SerializeField] MoveDirection[] order =
        {
            MoveDirection.Right,
            MoveDirection.Left,
            MoveDirection.Up,
            MoveDirection.Down,
        };

        [SerializeField] ObservableValue<bool> locked = new ObservableValue<bool>(false);

        /// <summary>顺序变更（含锁定状态变化）后触发 → UI 重建排序行。</summary>
        public MiniEvent OrderChanged { get; } = new MiniEvent();

        /// <summary>是否已锁定：Play 之后为 true，UI 只读展示。</summary>
        public ObservableValue<bool> Locked => locked;

        /// <summary>当前顺序（只读视图，外部不得直接改数组）。</summary>
        public IReadOnlyList<MoveDirection> Order => order;

        public int Count => order != null ? order.Length : 0;

        /// <summary>取指定优先位的方向；越界返回第一项兜底。</summary>
        public MoveDirection At(int index)
        {
            if (order == null || order.Length == 0) return MoveDirection.Right;
            return order[Mathf.Clamp(index, 0, order.Length - 1)];
        }

        protected override void OnInitialize() => Normalize();

        /// <summary>锁定/解锁；只有值变化才发事件。</summary>
        public void SetLocked(bool value)
        {
            if (locked.Value == value) return;

            locked.Value = value;
            OrderChanged.Invoke();
        }

        /// <summary>把第 index 位往前挪一位（与前一格交换）；已在首位或已锁定时无效。</summary>
        public bool MoveUp(int index)
        {
            if (index <= 0 || index >= Count) return false;
            return Swap(index, index - 1);
        }

        /// <summary>把第 index 位往后挪一位（与后一格交换）；已在末位或已锁定时无效。</summary>
        public bool MoveDown(int index)
        {
            if (index < 0 || index >= Count - 1) return false;
            return Swap(index, index + 1);
        }

        /// <summary>点选置顶：把第 index 位提到最前，其余顺延。</summary>
        public bool MoveToTop(int index)
        {
            if (locked.Value || index <= 0 || index >= Count) return false;

            var picked = order[index];
            for (var i = index; i > 0; i--) order[i] = order[i - 1];
            order[0] = picked;
            OrderChanged.Invoke();
            return true;
        }

        /// <summary>恢复默认顺序（右→左→上→下）。</summary>
        public void ResetOrder()
        {
            if (locked.Value) return;

            order = new[]
            {
                MoveDirection.Right,
                MoveDirection.Left,
                MoveDirection.Up,
                MoveDirection.Down,
            };
            OrderChanged.Invoke();
        }

        bool Swap(int a, int b)
        {
            if (locked.Value || a < 0 || b < 0 || a >= Count || b >= Count) return false;

            var temp = order[a];
            order[a] = order[b];
            order[b] = temp;
            OrderChanged.Invoke();
            return true;
        }

        /// <summary>修正异常数据：长度不等于 4 或方向重复时重建为默认顺序。</summary>
        void Normalize()
        {
            if (order != null && order.Length == MoveDirectionInfo.Count && IsPermutation()) return;

            order = new[]
            {
                MoveDirection.Right,
                MoveDirection.Left,
                MoveDirection.Up,
                MoveDirection.Down,
            };
        }

        bool IsPermutation()
        {
            var seen = new bool[MoveDirectionInfo.Count];
            for (var i = 0; i < order.Length; i++)
            {
                var value = (int)order[i];
                if (value < 0 || value >= MoveDirectionInfo.Count || seen[value]) return false;
                seen[value] = true;
            }

            return true;
        }
    }
}
