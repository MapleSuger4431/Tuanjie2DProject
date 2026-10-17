using UnityEngine;
using UnityEngine.InputSystem;
using Game.Input;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 棋盘相机适配与缩放。
    /// 棋盘是屏幕左侧一块**独立的视口空间**：把相机的 pixelRect 设成 BoardScreenLayout 给出的操作空间矩形，
    /// 相机只在这块矩形内渲染，放大时超出的格子被天然裁掉，看到的永远只有区域内的棋盘。
    /// 缩放下限固定为 1（整块棋盘刚好占满操作空间），只能放大不能缩小，上限 maxZoom。
    /// 棋盘尺寸随关卡从 10×10 长到 20×20，适配结果自动跟着变。
    /// </summary>
    public sealed class BoardCameraFitter : MonoBehaviour
    {
        [SerializeField] Camera targetCamera;
        [SerializeField] BoardScreenLayout layout;

        [Tooltip("放大上限；下限恒为 1（刚好占满操作空间，含四边间隔）")]
        [SerializeField] float maxZoom = 3f;

        [Tooltip("棋盘包围盒与视口四边保留的屏幕像素间隔：留出来才看得到最外圈格子的外边")]
        [SerializeField] float boardMarginPixels = 20f;

        [SerializeField] float zoomStep = 1.1f;

        BoardGridView _grid;
        float _zoom = 1f;

        // 鼠标控制一律走 Input Action（GameInputActions 的 Board 表），不再直接读 Mouse.current
        GameInputActions.BoardActions _input;
        bool _inputReady;

        // 平移状态：_panOffset 是相机相对「棋盘几何中心」的世界偏移；拖拽时内容跟手，故相机反向走
        Vector2 _panOffset;
        bool _panning;
        Vector2 _panLastScreen;

        /// <summary>当前是否正在右键拖拽平移（拖拽中不响应放置交互）。</summary>
        public bool IsPanning => _panning;

        /// <summary>当前缩放倍率（1 = 整块棋盘刚好占满操作空间）。</summary>
        public float Zoom => _zoom;

        /// <summary>刚好装下整块棋盘所需的 orthographicSize。</summary>
        public float FitOrthographicSize { get; private set; }

        /// <summary>棋盘视口矩形（当前屏幕像素，左下原点）。</summary>
        public Rect ViewportRect { get; private set; }

        void Awake()
        {
            _grid = GetComponent<BoardGridView>();
            if (targetCamera == null) targetCamera = Camera.main;
            if (layout == null) layout = FindObjectOfType<BoardScreenLayout>();
        }

        void OnEnable()
        {
            _input = GameInput.Instance.Board;
            // 滚轮与中键是「一次一格」的离散输入：走 performed 回调，避免轮询读到残留的 delta 值
            _input.Zoom.performed += OnZoomPerformed;
            _input.ResetView.performed += OnResetViewPerformed;
            _inputReady = true;
        }

        void OnDisable()
        {
            if (!_inputReady) return;

            _input.Zoom.performed -= OnZoomPerformed;
            _input.ResetView.performed -= OnResetViewPerformed;
            _inputReady = false;
        }

        void LateUpdate()
        {
            ReadPanInput();
            Apply();
        }

        void OnZoomPerformed(InputAction.CallbackContext context)
        {
            float scroll = context.ReadValue<Vector2>().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float factor = scroll > 0f ? zoomStep : 1f / zoomStep;
            SetZoom(_zoom * factor);
        }

        void OnResetViewPerformed(InputAction.CallbackContext context) => ResetPan();

        /// <summary>右键在棋盘视口内按住拖拽 = 平移视野；拖出视口也继续跟手，松开结束。</summary>
        void ReadPanInput()
        {
            if (!_inputReady) return;

            if (_input.Pan.WasPressedThisFrame())
            {
                var start = _input.Point.ReadValue<Vector2>();
                // 起点必须在棋盘视口内，避免右侧 UI 区域的右键误触发平移
                if (ContainsScreenPoint(start))
                {
                    _panning = true;
                    _panLastScreen = start;
                }
            }

            if (!_panning) return;

            if (_input.Pan.IsPressed())
            {
                var now = _input.Point.ReadValue<Vector2>();
                _panOffset -= ScreenDeltaToWorld(now - _panLastScreen);
                _panLastScreen = now;
            }

            if (_input.Pan.WasReleasedThisFrame()) _panning = false;
        }

        /// <summary>屏幕像素位移换算成世界位移：视口高度对应 2*orthoSize。</summary>
        Vector2 ScreenDeltaToWorld(Vector2 screenDelta)
        {
            float ortho = FitOrthographicSize > 0f ? FitOrthographicSize / _zoom : 1f;
            float height = ViewportRect.height > 0f ? ViewportRect.height : Mathf.Max(Screen.height, 1);
            float worldPerPixel = 2f * ortho / height;
            return screenDelta * worldPerPixel;
        }

        /// <summary>外部（UI 归位按钮）把视野拉回棋盘中心。</summary>
        public void ResetPan() => _panOffset = Vector2.zero;

        /// <summary>外部（UI 缩放按钮）直接设置倍率，会被夹到 1~maxZoom。</summary>
        public void SetZoom(float value) => _zoom = Mathf.Clamp(value, 1f, Mathf.Max(maxZoom, 1f));

        /// <summary>判断屏幕点是否落在棋盘操作空间内（视口外不接受操作）。</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint) => ViewportRect.Contains(screenPoint);

        /// <summary>按当前布局矩形与棋盘尺寸重算视口、正交大小与相机位置。</summary>
        public void Apply()
        {
            if (targetCamera == null || _grid == null || layout == null) return;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            var rect = ClampToScreen(layout.OperationRect);
            ViewportRect = rect;

            targetCamera.pixelRect = rect;
            targetCamera.aspect = rect.width / rect.height;

            // 四周留边：棋盘只占「视口减去间隔」的那块，最外圈格子的外边才不会被视口边缘切掉
            float margin = Mathf.Max(boardMarginPixels, 0f);
            float usableHeight = Mathf.Max(rect.height - margin * 2f, 1f);
            float usableWidth = Mathf.Max(rect.width - margin * 2f, 1f);

            // 视口高度 ↔ 2*orthoSize，视口宽度 ↔ 2*orthoSize*aspect：两个约束取更严格者
            float fit = Mathf.Max(
                _grid.BoardWorldHeight * 0.5f * rect.height / usableHeight,
                _grid.BoardWorldWidth * 0.5f * rect.height / usableWidth);
            FitOrthographicSize = fit;

            targetCamera.orthographic = true;
            float ortho = fit / _zoom;
            targetCamera.orthographicSize = ortho;

            // 视口即操作空间；棋盘原点是左下角，故对齐点是棋盘几何中心而非父对象原点
            var boardCenter = transform.TransformPoint(_grid.BoardCenterLocal);
            var center = ClampPan(boardCenter, ortho, targetCamera.aspect, margin, rect.height);
            targetCamera.transform.position = new Vector3(
                center.x, center.y, targetCamera.transform.position.z);
        }

        /// <summary>
        /// 把平移偏移夹在棋盘范围内：可视范围已盖住整块棋盘时偏移只能是 0，
        /// 于是 zoom=1（整盘可见）自动归位、放大后拖到棋盘边缘就停住，不会拖出空白。
        /// </summary>
        Vector2 ClampPan(Vector2 boardCenter, float ortho, float aspect, float margin, float rectHeight)
        {
            float worldPerPixel = 2f * ortho / Mathf.Max(rectHeight, 1f);
            float marginWorld = Mathf.Max(margin, 0f) * worldPerPixel;

            float limitX = _grid.BoardWorldWidth * 0.5f + marginWorld - ortho * aspect;
            float limitY = _grid.BoardWorldHeight * 0.5f + marginWorld - ortho;

            _panOffset.x = Mathf.Clamp(_panOffset.x, -Mathf.Max(limitX, 0f), Mathf.Max(limitX, 0f));
            _panOffset.y = Mathf.Clamp(_panOffset.y, -Mathf.Max(limitY, 0f), Mathf.Max(limitY, 0f));
            return boardCenter + _panOffset;
        }

        static Rect ClampToScreen(Rect rect)
        {
            float x = Mathf.Clamp(rect.x, 0f, Mathf.Max(Screen.width - 1f, 0f));
            float y = Mathf.Clamp(rect.y, 0f, Mathf.Max(Screen.height - 1f, 0f));
            float w = Mathf.Clamp(rect.width, 1f, Screen.width - x);
            float h = Mathf.Clamp(rect.height, 1f, Screen.height - y);
            return new Rect(x, y, w, h);
        }
    }
}
