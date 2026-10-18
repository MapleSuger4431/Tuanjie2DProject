using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Board;

namespace Game.EditorTools
{
    /// <summary>
    /// <see cref="BoardLevelPainter" /> 的 Inspector：把一维化的二维数组画成可点的棋盘网格（左键 0→4 循环、右键反向），
    /// 配「应用数组 → 子物体 / 从子物体读取 / 按边长重建格子 / 全部置 0」四个按钮。
    /// </summary>
    [CustomEditor(typeof(BoardLevelPainter))]
    public sealed class BoardLevelPainterEditor : UnityEditor.Editor
    {
        const float MinCell = 14f;
        const float MaxCell = 26f;
        const float MaxGridHeight = 460f;
        const float HeaderWidth = 24f;

        SerializedProperty _gridView;
        Vector2 _scroll;
        bool _showNumber = true;
        GUIStyle _header;
        GUIStyle _cellLabel;

        /// <summary>效果批量操作的资产槽（只存在 Inspector 会话里，不落盘到画笔组件）。</summary>
        TileEffect _effect;

        BoardLevelPainter Painter => (BoardLevelPainter)target;

        void OnEnable() => _gridView = serializedObject.FindProperty("gridView");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_gridView);
            serializedObject.ApplyModifiedProperties();

            DrawSizeSlider();
            _showNumber = EditorGUILayout.Toggle("显示数值", _showNumber);

            EditorGUILayout.Space(2f);
            DrawLegend();
            EditorGUILayout.Space(2f);
            DrawGrid();
            DrawButtons();
            DrawEffectSection();

            EditorGUILayout.HelpBox(
                "数组按行主序（索引 = y × 边长 + x）与棋盘子物体一一对应；左下角固定起点、右上角固定终点，不参与循环。\n" +
                "效果层与状态数组互不影响：逐格在各自 Inspector 上拖 TileEffect 资产，或用下面的「应用到全盘 / 清空效果层」批量处理。",
                MessageType.None);
        }

        // ---------- 头部控件 ----------

        void DrawSizeSlider()
        {
            var painter = Painter;
            var next = EditorGUILayout.IntSlider("棋盘边长", painter.Size, BoardModel.MinSize, BoardModel.MaxSize);
            if (next == painter.Size) return;

            Undo.RecordObject(painter, "Board Level Painter Resize");
            painter.Resize(next);
            EditorUtility.SetDirty(painter);
        }

        void DrawLegend()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("图例", EditorStyles.miniLabel, GUILayout.Width(32f));

            for (var state = 0; state < BoardLevelPainter.StateCount; state++)
                DrawSwatch(BoardLevelPainter.StateColor(state), $"{state} {BoardLevelPainter.StateName(state)}");

            DrawSwatch(TileTypeInfo.Color(TileType.Start), "起点");
            DrawSwatch(TileTypeInfo.Color(TileType.Goal), "终点");
            EditorGUILayout.EndHorizontal();
        }

        static void DrawSwatch(Color color, string label)
        {
            var rect = GUILayoutUtility.GetRect(14f, 14f, GUILayout.Width(14f), GUILayout.Height(14f));
            EditorGUI.DrawRect(rect, color);
            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.Space(6f);
        }

        // ---------- 网格 ----------

        void DrawGrid()
        {
            var painter = Painter;
            var size = painter.Size;
            var cell = Mathf.Clamp(
                Mathf.Floor((EditorGUIUtility.currentViewWidth - HeaderWidth - 40f) / size), MinCell, MaxCell);

            EditorGUILayout.LabelField("棋盘状态（左键循环 0→4，右键反向）", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(string.Empty, GUILayout.Width(HeaderWidth));
            for (var x = 0; x < size; x++)
                EditorGUILayout.LabelField(x.ToString(), HeaderStyle, GUILayout.Width(cell));
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(MaxGridHeight));
            for (var y = size - 1; y >= 0; y--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(y.ToString(), HeaderStyle, GUILayout.Width(HeaderWidth), GUILayout.Height(cell));
                for (var x = 0; x < size; x++) DrawCell(painter, x, y, cell);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawCell(BoardLevelPainter painter, int x, int y, float cell)
        {
            var rect = GUILayoutUtility.GetRect(cell, cell, GUILayout.Width(cell), GUILayout.Height(cell));
            var isStart = painter.IsStartCell(x, y);
            var isGoal = !isStart && painter.IsGoalCell(x, y);
            var reserved = isStart || isGoal;
            var state = painter.StateAt(x, y);
            var color = reserved
                ? TileTypeInfo.Color(isStart ? TileType.Start : TileType.Goal)
                : BoardLevelPainter.StateColor(state);

            // 留 1px 空隙当网格线，比逐格描边省事
            var fill = new Rect(rect.x, rect.y, rect.width - 1f, rect.height - 1f);
            EditorGUI.DrawRect(fill, color);

            if (reserved)
                GUI.Label(fill, isStart ? "起" : "终", CellLabelStyle(color));
            else if (_showNumber)
                GUI.Label(fill, state.ToString(), CellLabelStyle(color));

            // 有效果的格子右上角点一个青色小方块：效果层与状态数组是两层，别混着看
            if (EffectAt(painter, x, y) != null)
            {
                var mark = Mathf.Max(5f, fill.width * 0.3f);
                EditorGUI.DrawRect(new Rect(fill.xMax - mark - 1f, fill.y + 1f, mark, mark),
                    new Color(0.2f, 0.9f, 0.95f));
            }

            if (Event.current.type != EventType.MouseDown || !rect.Contains(Event.current.mousePosition)) return;

            if (!reserved)
            {
                Undo.RecordObject(painter, "Board Level Painter");
                painter.CycleState(x, y, Event.current.button == 1 ? -1 : 1);
                EditorUtility.SetDirty(painter);
            }

            Event.current.Use();
            Repaint();
        }

        // ---------- 效果层 ----------

        void DrawEffectSection()
        {
            var painter = Painter;

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("效果层（叠在格子上，不改变通行性）", EditorStyles.boldLabel);

            _effect = (TileEffect)EditorGUILayout.ObjectField("效果资产", _effect, typeof(TileEffect), false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("应用到全盘", GUILayout.Height(22f)))
            {
                RecordBoard(painter);
                var applied = painter.ApplyEffectToTiles(_effect);
                MarkDirty(painter);
                Debug.Log($"[BoardLevelPainter] 已把「{(_effect != null ? _effect.DisplayName : "空")}」应用到 {applied} 格。", painter);
            }

            if (GUILayout.Button("清空效果层", GUILayout.Height(22f)))
            {
                RecordBoard(painter);
                var cleared = painter.ClearEffects();
                MarkDirty(painter);
                Debug.Log($"[BoardLevelPainter] 已清空 {cleared} 格的效果。", painter);
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>该格当前配的效果（用于网格上画标记）；格子还没建好时为 null。</summary>
        static TileEffect EffectAt(BoardLevelPainter painter, int x, int y)
        {
            var grid = painter.ResolveGrid();
            var tile = grid != null ? grid.TileAt(new Vector2Int(x, y)) : null;

            return tile != null ? tile.PresetEffect : null;
        }

        // ---------- 按钮 ----------

        void DrawButtons()
        {
            var painter = Painter;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("应用数组 → 子物体", GUILayout.Height(26f)))
            {
                RecordBoard(painter);
                var applied = painter.ApplyToTiles();
                MarkDirty(painter);
                Debug.Log($"[BoardLevelPainter] 已应用 {applied} 格。", painter);
            }

            if (GUILayout.Button("从子物体读取", GUILayout.Height(26f)))
            {
                RecordBoard(painter);
                var read = painter.CaptureFromTiles();
                MarkDirty(painter);
                Debug.Log($"[BoardLevelPainter] 已读取 {read} 格。", painter);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("按边长重建格子", GUILayout.Height(22f)))
            {
                RecordBoard(painter);
                painter.GenerateTiles();
                painter.ApplyToTiles();
                MarkDirty(painter);
                Debug.Log($"[BoardLevelPainter] 已按边长 {painter.Size} 重建格子。", painter);
            }

            if (GUILayout.Button("全部置 0", GUILayout.Height(22f)))
            {
                Undo.RecordObject(painter, "Board Level Painter Clear");
                painter.ClearStates();
                EditorUtility.SetDirty(painter);
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }

        // ---------- 工具 ----------

        static void RecordBoard(BoardLevelPainter painter)
        {
            if (painter == null) return;

            Undo.RecordObject(painter, "Board Level Painter");
            var grid = painter.ResolveGrid();
            // 应用时可能整盘重建（创建/销毁子物体），整棵子树一起进 Undo 栈
            if (grid != null) Undo.RegisterFullObjectHierarchyUndo(grid.gameObject, "Board Level Painter");
        }

        static void MarkDirty(BoardLevelPainter painter)
        {
            if (painter == null) return;

            EditorUtility.SetDirty(painter);
            foreach (var tile in painter.CollectTiles())
            {
                if (tile != null) EditorUtility.SetDirty(tile);
            }

            EditorSceneManager.MarkSceneDirty(painter.gameObject.scene);
        }

        GUIStyle HeaderStyle =>
            _header ?? (_header = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter });

        GUIStyle CellLabelStyle(Color background)
        {
            if (_cellLabel == null)
                _cellLabel = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };

            // 底色亮就压黑字，暗就用白字，保证数值可读
            var luminance = 0.299f * background.r + 0.587f * background.g + 0.114f * background.b;
            _cellLabel.normal.textColor = luminance > 0.5f ? Color.black : Color.white;
            return _cellLabel;
        }
    }
}
