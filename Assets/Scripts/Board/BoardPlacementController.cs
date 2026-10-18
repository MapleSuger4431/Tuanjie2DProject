using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Game.Input;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;

namespace Game.Board
{
    /// <summary>
    /// 鼠标交互：悬停时把方块的一个**实格（握点）**压在鼠标格心上显示虚影——鼠标永远落在块内某一格的中心，
    /// 而不是包围盒角上（异形块的角格常常是空的，看起来就像虚影飘走了）。左键点击把当前方块覆盖到棋盘格，随后换新方块。
    /// R 键把当前这块整块顺时针旋转 90°（每格状态跟着各自那一格走），握点格跟着同一格转，整块绕鼠标转。
    /// 输入一律走 Input System（本工程 activeInputHandler=2，旧 Input.* 运行时会抛异常）。
    /// 放置成功瞬间经 <see cref="AudioModule" /> 播放一次放置音效。
    /// </summary>
    public sealed class BoardPlacementController : MonoViewController<BoardContext>
    {
        [Tooltip("放置成功时播放的音效 — 经 AudioModule 的 SFX 通道播放")]
        [SerializeField] AudioClip placeSfx;

        [Tooltip("本次播放的局部音量（0-1），与 SFX 通道音量、总音量相乘生效")]
        [SerializeField, Range(0f, 1f)] float placeSfxVolume = 1f;

        BoardModel _board;
        PieceModel _piece;
        BoardRunModel _run;
        BoardGridView _grid;
        TileGhostView _ghost;
        BoardCameraFitter _fitter;
        Camera _camera;

        // 鼠标位置与左键放置走 Input Action（GameInputActions 的 Board 表）
        GameInputActions.BoardActions _input;
        bool _inputReady;

        // 握点：方块里始终压在鼠标格心上的那个实格（取靠近包围盒中心的）。换块即重算
        TilePiece _gripPiece;
        Vector2Int _gripOffset;

        // UI 遮挡检测复用同一份数据，避免每帧分配
        readonly List<RaycastResult> _uiRaycasts = new List<RaycastResult>();
        PointerEventData _uiPointerEvent;

        void Start()
        {
            _board = this.GetModel<BoardModel>();
            _piece = this.GetModel<PieceModel>();
            _run = this.GetModel<BoardRunModel>();
            _grid = GetComponent<BoardGridView>();
            _ghost = GetComponent<TileGhostView>();
            _fitter = GetComponent<BoardCameraFitter>();
            _camera = Camera.main;
            _input = GameInput.Instance.Board;
            // 旋转是「一次按下转一次」的离散动作，走 performed 回调而不是 Update 轮询：
            // 轮询要正好赶上按下那一帧才读得到 WasPressedThisFrame，事件驱动不会漏（与 Zoom / ResetView 同套写法）
            _input.Rotate.performed += OnRotatePerformed;
            _inputReady = true;
        }

        void OnDestroy()
        {
            if (_inputReady) _input.Rotate.performed -= OnRotatePerformed;
        }

        /// <summary>R 键：只在铺路阶段可转，运行 / 结算阶段棋盘已锁定。</summary>
        void OnRotatePerformed(InputAction.CallbackContext context)
        {
            if (_run != null && !_run.IsBuilding) return;

            RotatePiece();
        }

        void Update()
        {
            if (_board == null || _piece == null || _grid == null) return;

            // 运行阶段与结算后锁定放置：虚影隐藏、点击棋盘无效（RB-16）
            if (_run != null && !_run.IsBuilding)
            {
                _ghost?.Hide();
                return;
            }

            // R 键旋转由 Start 里订阅的 Rotate.performed 驱动（不在这里轮询）
            var piece = _piece.Current.Value;

            // 换块就重算握点：新块得有一个实格来接住鼠标
            if (!ReferenceEquals(_gripPiece, piece))
            {
                _gripPiece = piece;
                _gripOffset = ComputeGrip(piece);
            }

            if (!_inputReady || piece == null || piece.Shape == null)
            {
                _ghost?.Hide();
                return;
            }

            var screen = _input.Point.ReadValue<Vector2>();

            // 棋盘是独立视口：鼠标在视口之外不显示虚影、不接受放置
            if (_fitter != null && !_fitter.ContainsScreenPoint(screen))
            {
                _ghost?.Hide();
                return;
            }

            // 鼠标压在运行时 UI 上：这次点击归 UI 消费，不预览也不放置
            if (IsPointerBlockedByUI(screen))
            {
                _ghost?.Hide();
                return;
            }

            // 右键正在拖拽平移视野：此时只搬相机，不预览也不放置
            if (_fitter != null && _fitter.IsPanning)
            {
                _ghost?.Hide();
                return;
            }

            var anchor = ResolveAnchor(ScreenToCell(screen), piece);
            bool valid = _board.CanCover(anchor, piece);
            _ghost?.Show(anchor, piece, valid);

            if (_input.Place.WasPressedThisFrame() && _board.TryCover(anchor, piece))
            {
                PlayPlaceSfx();
                _piece.RollNew();
            }
        }

        void PlayPlaceSfx()
        {
            // 未接音效时静默跳过：AudioModule.PlaySfx 对空 clip 会 LogError，避免无音效场景刷屏
            if (placeSfx == null) return;

            AudioModule.PlaySfx(placeSfx, placeSfxVolume);
        }

        /// <summary>
        /// 整块顺时针转 90°（每格状态跟着走）。握点格按 <see cref="TilePiece.RotateCW" /> 的同一映射跟着转，
        /// 转完它仍然压在鼠标下——效果是整块绕鼠标转，而不是绕包围盒中心跳半格。
        /// </summary>
        void RotatePiece()
        {
            var before = _piece.Current.Value;
            if (before == null || before.Cells == null || before.Cells.Length == 0) return;

            // 握点可能还停在上一块上（旋转回调先于 Update 的换块检查触发）：先归属到当前块再转
            if (!ReferenceEquals(_gripPiece, before))
            {
                _gripPiece = before;
                _gripOffset = ComputeGrip(before);
            }

            _piece.RotateCurrent();

            var after = _piece.Current.Value;
            if (after == null) return;

            _gripOffset = RotatedGrip(_gripOffset, before.Cells);
            _gripPiece = after;
        }

        /// <summary>
        /// 握点格：块内**已占用**、离包围盒中心最近的格（并列取行优先靠前者）。取实格而非包围盒角，
        /// 才能保证虚影出现时鼠标正压在某一块格的中心。
        /// </summary>
        static Vector2Int ComputeGrip(TilePiece piece)
        {
            var cells = piece != null ? piece.Cells : null;
            if (cells == null || cells.Length == 0) return Vector2Int.zero;

            var bounds = piece.Bounds;
            var center = new Vector2((bounds.x - 1) * 0.5f, (bounds.y - 1) * 0.5f);

            var grip = cells[0];
            var bestDistance = float.MaxValue;
            for (var i = 0; i < cells.Length; i++)
            {
                var distance = (new Vector2(cells[i].x, cells[i].y) - center).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    grip = cells[i];
                }
            }

            return grip;
        }

        /// <summary>握点随顺时针旋转的映射：(x, y) → (y, maxX - x)，与 TilePiece.RotateCW 完全同式（块内偏移已归一，无需再平移）。</summary>
        static Vector2Int RotatedGrip(Vector2Int grip, Vector2Int[] cellsBefore)
        {
            var maxX = 0;
            for (var i = 0; i < cellsBefore.Length; i++) maxX = Mathf.Max(maxX, cellsBefore[i].x);

            return new Vector2Int(grip.y, maxX - grip.x);
        }

        /// <summary>
        /// 落点 = 鼠标所在格减去握点偏移，再整体夹回棋盘内：握点格对准鼠标格心，
        /// 贴边放不下时整块往回夹、不出界。虚影与实际落点都取这里的结果，所见即所落。
        /// </summary>
        Vector2Int ResolveAnchor(Vector2Int cell, TilePiece piece)
        {
            var bounds = piece.Bounds;
            var maxX = Mathf.Max(0, _board.Width - bounds.x);
            var maxY = Mathf.Max(0, _board.Height - bounds.y);

            var target = cell - _gripOffset;
            return new Vector2Int(Mathf.Clamp(target.x, 0, maxX), Mathf.Clamp(target.y, 0, maxY));
        }

        /// <summary>
        /// 鼠标是否被运行时的 UI 挡住：走 EventSystem 的 UI 射线（与点击事件同一套判定），
        /// 命中任何可交互 UI 图形（含弹窗蒙版）即视为 UI 占用，不依赖鼠标是否移动过。
        /// </summary>
        bool IsPointerBlockedByUI(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            if (_uiPointerEvent == null) _uiPointerEvent = new PointerEventData(eventSystem);
            _uiPointerEvent.position = screenPosition;

            _uiRaycasts.Clear();
            eventSystem.RaycastAll(_uiPointerEvent, _uiRaycasts);
            return _uiRaycasts.Count > 0;
        }

        Vector2Int ScreenToCell(Vector2 screenPosition)
        {
            // 相机只渲染操作空间矩形，故先把屏幕坐标归一到该矩形内的视口坐标
            var rect = _camera.pixelRect;
            float vx = (screenPosition.x - rect.x) / rect.width;
            float vy = (screenPosition.y - rect.y) / rect.height;

            // 正交相机：z 传「相机到棋盘平面的距离」，反投影后落在本物体所在平面
            float distance = -_camera.transform.position.z;
            var world = _camera.ViewportToWorldPoint(new Vector3(vx, vy, distance));
            return _grid.WorldToCell(world);
        }
    }
}
