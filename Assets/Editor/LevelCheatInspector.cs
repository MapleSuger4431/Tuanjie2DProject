using Game.Board;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.EditorTools
{
    /// <summary>
    /// 关卡作弊器的 Inspector 按钮：挂在关卡场景自带的 <see cref="BoardRuntimeDriver" /> 上（不改场景）。
    /// <list type="bullet">
    /// <item>「直接通关本关」——走正常结算链，用来直接看通关面板 / 记录 / 解锁的真实表现；</item>
    /// <item>「通关并进入下一关」——被卡住时一键推进到后续关卡。</item>
    /// </list>
    /// 只在 Play 模式生效；核心逻辑在运行时类 <see cref="LevelCheat" />，便于 Play 验证脚本复用。
    /// </summary>
    [CustomEditor(typeof(BoardRuntimeDriver))]
    public sealed class LevelCheatInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("关卡作弊器（测试用）", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("进入 Play 模式后可用（作用于正在运行的关卡场景）。", MessageType.Info);
                DrawDefaultInspector();
                return;
            }

            DrawCheatStatus();

            using (new EditorGUI.DisabledScope(!CheatAvailable()))
            {
                if (GUILayout.Button("★ 直接通关本关（正常结算 + 记录）"))
                    LevelCheat.FinishCurrentLevel();

                if (GUILayout.Button("▶ 通关并进入下一关"))
                    LevelCheat.PassAndGotoNextLevel();
            }

            EditorGUILayout.Space(4);
            DrawDefaultInspector();
        }

        static bool CheatAvailable()
        {
            return BoardContext.Instance != null && BoardContext.Instance.GetModel<BoardRunModel>() != null;
        }

        static void DrawCheatStatus()
        {
            var run = BoardContext.Instance != null ? BoardContext.Instance.GetModel<BoardRunModel>() : null;
            if (run == null)
            {
                EditorGUILayout.HelpBox("未找到棋盘运行模型（BoardRunModel）。", MessageType.Warning);
                return;
            }

            var sceneName = SceneManager.GetActiveScene().name;
            EditorGUILayout.HelpBox(
                $"场景：{sceneName}\n" +
                $"阶段：{run.CurrentPhase.Value}　结果：{run.Result.Value}\n" +
                $"步数：{run.StepCount.Value}　金币：{run.CoinsCollected.Value}/{run.CoinTotal}",
                MessageType.None);
        }
    }
}
