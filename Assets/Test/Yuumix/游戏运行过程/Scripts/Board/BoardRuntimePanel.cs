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
        [SerializeField] Text hintText;

        PieceModel _piece;
        BoardModel _board;

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

            _piece.Next.AddListenerAndInvoke(OnNextChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _board.CoveredCount.AddListenerAndInvoke(OnCoveredCountChanged)
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
            if (placedCountText != null)
                placedCountText.text = $"已放置：{count}";
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
