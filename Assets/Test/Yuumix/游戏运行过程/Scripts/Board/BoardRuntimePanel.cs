using UnityEngine;
using UnityEngine.UI;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 棋盘运行时常驻面板（挂 UIRoot 的层画布下，自身不带 Canvas）：
    /// 上下白板占位 + 右侧操作面板，面板上方预告下一块。全部数据来自 Model 的 ObservableValue。
    /// </summary>
    public sealed class BoardRuntimePanel : AesirBasePanelViewController<BoardContext>
    {
        [SerializeField] RectTransform topBar;
        [SerializeField] RectTransform bottomBar;
        [SerializeField] RectTransform rightPanel;
        [SerializeField] NextPieceView nextPieceView;
        [SerializeField] Text placedCountText;
        [SerializeField] Text timeText;
        [SerializeField] Text hintText;

        PieceModel _piece;
        BoardModel _board;
        BoardTimeModel _time;

        protected override void OnInit()
        {
            RuntimeTestFonts.Apply(transform);
            StretchToScreen();
        }

        protected override void OnShow(object payload)
        {
            base.OnShow(payload);

            // 布局真源仍在 BoardScreenLayout：把 UI 根画布与三块占位白板交给它驱动
            var layout = FindObjectOfType<BoardScreenLayout>();
            if (layout != null)
            {
                layout.RegisterCanvas(GetComponentInParent<Canvas>());
                layout.RegisterBars(topBar, bottomBar, rightPanel);
            }

            _piece = this.GetModel<PieceModel>();
            _board = this.GetModel<BoardModel>();
            _time = this.GetModel<BoardTimeModel>();

            _piece.Next.AddListenerAndInvoke(OnNextChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _board.CoveredCount.AddListenerAndInvoke(OnCoveredCountChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _time.Elapsed.AddListenerAndInvoke(_ => OnTimeChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _time.State.AddListenerAndInvoke(_ => OnTimeChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);

            if (hintText != null)
                hintText.text = "左键放置当前方块　滚轮缩放棋盘";
        }

        void OnNextChanged(TileShape shape)
        {
            if (nextPieceView != null) nextPieceView.Render(shape);
        }

        void OnCoveredCountChanged(int count)
        {
            if (placedCountText == null) return;

            placedCountText.text = _board != null ? $"已用：{count}/{_board.PieceLimit}" : $"已用：{count}";
        }

        /// <summary>秒表一行文本：未开始 / 计时中 / 已结束共用，用时越短越好。</summary>
        void OnTimeChanged()
        {
            if (timeText == null || _time == null) return;

            string suffix;
            if (_time.State.Value == BoardTimeModel.StopwatchState.Running) suffix = string.Empty;
            else if (_time.State.Value == BoardTimeModel.StopwatchState.Finished) suffix = "（已结束）";
            else suffix = "（未开始）";

            timeText.text = $"用时：{_time.FormatElapsed()}{suffix}";
        }

        /// <summary>面板本体铺满整屏，三块占位白板各自锚定自己的区域。</summary>
        void StretchToScreen()
        {
            var rect = transform as RectTransform;
            if (rect == null) return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
