using UnityEngine;
using UnityEngine.InputSystem;
using Runestone.AesirArchitecture;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 鼠标交互：悬停格对齐方块包围盒左上角显示虚影，左键点击把当前方块覆盖到棋盘格，随后换新方块。
    /// 输入一律走 Input System（本工程 activeInputHandler=2，旧 Input.* 运行时会抛异常）。
    /// </summary>
    public sealed class BoardPlacementController : MonoViewController<BoardContext>
    {
        [SerializeField] float ghostAlpha = 0.6f;
        [SerializeField] Color invalidColor = new Color(1f, 0.25f, 0.25f, 0.6f);

        BoardModel _board;
        PieceModel _piece;
        BoardGridView _grid;
        TileGhostView _ghost;
        BoardCameraFitter _fitter;
        Camera _camera;

        void Start()
        {
            _board = this.GetModel<BoardModel>();
            _piece = this.GetModel<PieceModel>();
            _grid = GetComponent<BoardGridView>();
            _ghost = GetComponent<TileGhostView>();
            _fitter = GetComponent<BoardCameraFitter>();
            _camera = Camera.main;
        }

        void Update()
        {
            if (_board == null || _piece == null || _grid == null) return;

            var mouse = Mouse.current;
            var shape = _piece.Current.Value;
            if (mouse == null || shape == null)
            {
                _ghost?.Hide();
                return;
            }

            var screen = mouse.position.ReadValue();

            // 棋盘是独立视口：鼠标在视口之外不显示虚影、不接受放置
            if (_fitter != null && !_fitter.ContainsScreenPoint(screen))
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
            bool valid = _board.CanCover(anchor, shape);
            _ghost?.Show(anchor, shape, valid ? GhostColor(shape.Color) : invalidColor);

            if (mouse.leftButton.wasPressedThisFrame && _board.TryCover(anchor, shape))
                _piece.RollNew();
        }

        Color GhostColor(Color shapeColor) =>
            new Color(shapeColor.r, shapeColor.g, shapeColor.b, ghostAlpha);

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
