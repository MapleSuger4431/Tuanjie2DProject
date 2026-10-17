using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 通关成功结算面板：标题 + 成绩三行（步数 / 用时 / 已用方块）
    /// +「重玩此关 / 回到选关界面」两个出口（出口行为与步数、已用方块两行在 <see cref="LevelResultPanel" />）。
    /// <para>
    /// 用时只有通关页显示：失败页不记时间（<see cref="LevelFailPanel" /> 没有这一行）。
    /// 用时的**展示**在此，写入在 <c>LevelProgressService</c>：BoardRuntimePanel 在通关瞬间先落记录，
    /// 再把「是否破纪录」作为 payload 传进来，本面板据此在用时行追加「新纪录」。
    /// </para>
    /// </summary>
    public sealed class LevelClearPanel : LevelResultPanel
    {
        [Tooltip("本局用时文本（仅通关页有）")]
        [SerializeField]
        TMP_Text timeLabel;

        /// <summary>本次通关是否刷新了该关最短用时（由 payload 传入）。</summary>
        bool _newRecord;

        protected override Localization.Key TitleKey => Localization.Key.LevelClearTitle;

        /// <summary>payload 为 bool：破纪录时用时行换「新纪录」文案；无 payload（如复用打开）按非纪录处理。</summary>
        protected override void OnShow(object payload)
        {
            _newRecord = payload is bool isNewRecord && isNewRecord;
            base.OnShow(payload);
        }

        protected override void ApplyResultText()
        {
            base.ApplyResultText();

            if (timeLabel == null) return;

            var time = Timer != null ? Timer.FormatElapsed() : "--:--.-";
            timeLabel.text = _newRecord
                ? Localization.Get(Localization.Key.LevelClearTimeNewRecord, time)
                : Localization.Get(Localization.Key.LevelClearTime, time);
        }
    }
}
