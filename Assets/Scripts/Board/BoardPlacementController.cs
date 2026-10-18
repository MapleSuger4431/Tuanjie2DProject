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
    /// 鼠标交互：悬停格对齐方块包围盒左上角显示虚影，左键点击把当前方块覆盖到棋盘格，随后换新方块。
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
            _inputReady = true;
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

            var piece = _piece.Current.Value;
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

            var anchor = ScreenToCell(screen);
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
