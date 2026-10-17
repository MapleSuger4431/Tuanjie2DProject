using UnityEngine;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 屏幕布局真源：用 UI 白板占位上下与右侧，剩下的左侧正方形就是棋盘的独立操作空间。
    /// 所有尺寸以 designResolution（1920×1080）为单位书写，按 UiScale 等比缩放到当前屏幕。
    /// UI 已接入 Aesir UIRoot：画布与三块白板由 BoardRuntimePanel 显示时注册进来
    /// （场景序列化引用仅作回退），缩放系数直接取 UI 根画布的 scaleFactor，保证 UI 与棋盘视口同一真源。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class BoardScreenLayout : MonoBehaviour
    {
        [SerializeField] Canvas canvas;
        [SerializeField] RectTransform topBar;
        [SerializeField] RectTransform bottomBar;
        [SerializeField] RectTransform rightPanel;

        [Tooltip("所有留白与占位尺寸的基准分辨率")]
        [SerializeField] Vector2 designResolution = new Vector2(1920f, 1080f);

        [SerializeField] float paddingLeft = 100f;
        [SerializeField] float reserveTop = 100f;
        [SerializeField] float reserveBottom = 100f;

        [Tooltip("右侧 UI 区宽度（1920 下 572 = 1920 - 100 - 65%屏宽）")]
        [SerializeField] float reserveRight = 572f;

        /// <summary>设计分辨率到当前屏幕的等比缩放系数。</summary>
        public float UiScale { get; private set; } = 1f;

        /// <summary>棋盘操作空间（当前屏幕像素，左下原点）：可用矩形内最大的**正方形**，靠左垂直居中。</summary>
        public Rect OperationRect { get; private set; }

        void LateUpdate() => Recompute();

        /// <summary>由 BoardRuntimePanel 在显示时注册 UI 根画布（UIRoot 的层画布）。</summary>
        public void RegisterCanvas(Canvas uiCanvas) => canvas = uiCanvas;

        /// <summary>由 BoardRuntimePanel 在显示时注册三块占位白板。</summary>
        public void RegisterBars(RectTransform top, RectTransform bottom, RectTransform right)
        {
            topBar = top;
            bottomBar = bottom;
            rightPanel = right;
        }

        /// <summary>按当前屏幕尺寸重算缩放系数、占位白板尺寸与操作空间矩形。</summary>
        public void Recompute()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;

            // 缩放系数一律跟 UI 根画布走（CanvasScaler 已算好），棋盘视口才不会与 UI 白板错位
            UiScale = canvas != null
                ? canvas.scaleFactor
                : Mathf.Min(Screen.width / designResolution.x, Screen.height / designResolution.y);

            ApplyTopBar(topBar, reserveTop);
            ApplyBottomBar(bottomBar, reserveBottom);
            ApplyRightPanel(rightPanel, reserveRight, reserveTop, reserveBottom);

            float left = paddingLeft * UiScale;
            float bottom = reserveBottom * UiScale;
            float right = reserveRight * UiScale;
            float top = reserveTop * UiScale;

            float availableWidth = Mathf.Max(Screen.width - left - right, 1f);
            float availableHeight = Mathf.Max(Screen.height - bottom - top, 1f);

            // 棋盘是正方形，蒙板（棋盘视口）也取正方形：缩放为 1 时正好铺满整块棋盘，不留两侧底板
            float side = Mathf.Min(availableWidth, availableHeight);
            OperationRect = new Rect(left, bottom + (availableHeight - side) * 0.5f, side, side);
        }

        static void ApplyTopBar(RectTransform bar, float height)
        {
            if (bar == null) return;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, height);
        }

        static void ApplyBottomBar(RectTransform bar, float height)
        {
            if (bar == null) return;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = Vector2.zero;
            bar.sizeDelta = new Vector2(0f, height);
        }

        static void ApplyRightPanel(RectTransform panel, float width, float top, float bottom)
        {
            if (panel == null) return;
            panel.anchorMin = new Vector2(1f, 0f);
            panel.anchorMax = Vector2.one;
            panel.pivot = new Vector2(1f, 0.5f);
            panel.offsetMin = new Vector2(-width, bottom);
            panel.offsetMax = new Vector2(0f, -top);
        }
    }
}
