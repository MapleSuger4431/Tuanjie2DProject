using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameRuntimeTest.Board;

namespace GameRuntimeTest.EditorTools
{
    /// <summary>
    /// 棋盘生成/配置工具：一键按规整尺寸重建格子，单格状态在各自 Inspector 里改 BoardTile.type（改完即时变色）。
    /// 生成会清空父对象下现有格子（手工摆放会被覆盖）；(0,0) 自动设为起点，右上角设为终点。
    /// </summary>
    public sealed class BoardBuilderWindow : EditorWindow
    {
        static readonly int[] Sizes = { 10, 15, 20 };
        static readonly string[] SizeLabels = { "10 × 10", "15 × 15", "20 × 20" };

        BoardGridView _target;
        int _sizeIndex;

        [MenuItem("Tools/Yuumix/游戏运行过程/棋盘生成器")]
        static void Open()
        {
            var window = GetWindow<BoardBuilderWindow>("棋盘生成器");
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("棋盘生成 / 单格配置", EditorStyles.boldLabel);

            _target = (BoardGridView)EditorGUILayout.ObjectField(
                "棋盘视图", _target, typeof(BoardGridView), true);

            if (_target == null)
                _target = UnityEngine.Object.FindObjectOfType<BoardGridView>();

            _sizeIndex = EditorGUILayout.Popup("棋盘尺寸", _sizeIndex, SizeLabels);

            EditorGUILayout.HelpBox(
                "生成会清空父对象（Tiles）下现有格子并重建：左下 (0,0) = 起点，右上角 = 终点，其余为「空」。\n" +
                "单格配置：在 Hierarchy 里选中 Cell_x_y，改 Inspector 上 BoardTile 的 Type（冰/火/空/可通行/山地）。",
                MessageType.Info);

            EditorGUI.BeginDisabledGroup(_target == null);

            if (GUILayout.Button("生成 / 重建棋盘"))
                Build();

            if (GUILayout.Button("清空铺设配置（保留山地与起终点）"))
                ClearPreset();

            EditorGUI.EndDisabledGroup();

            if (_target != null)
                EditorGUILayout.LabelField("当前尺寸", $"{_target.BoardSize} × {_target.BoardSize}");
        }

        void Build()
        {
            int size = Sizes[Mathf.Clamp(_sizeIndex, 0, Sizes.Length - 1)];

            Undo.RegisterCompleteObjectUndo(_target.gameObject, "Build Board");
            _target.BuildTiles(size);
            EditorUtility.SetDirty(_target);
            Save();

            Debug.Log($"[BoardBuilder] 已生成 {size}×{size} 棋盘（{size * size} 格），起点 (0,0)、终点 ({size - 1},{size - 1})。");
        }

        void ClearPreset()
        {
            Undo.RegisterCompleteObjectUndo(_target.gameObject, "Clear Board Preset");

            int size = _target.BoardSize;
            var goal = new Vector2Int(size - 1, size - 1);
            int changed = 0;

            foreach (var tile in _target.GetComponentsInChildren<BoardTile>(true))
            {
                if (tile == null) continue;

                var coord = tile.Cell;
                var next = tile.PresetType == TileType.Mountain
                    ? TileType.Mountain
                    : (coord == Vector2Int.zero
                        ? TileType.Start
                        : (coord == goal ? TileType.Goal : TileType.Void));

                if (next != tile.PresetType) changed++;
                tile.Configure(coord, next);
                EditorUtility.SetDirty(tile);
            }

            Save();
            Debug.Log($"[BoardBuilder] 已重置 {changed} 格的预设（山地与起终点保留）。");
        }

        void Save()
        {
            var scene = _target.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
