using Game.Core;
using Game.Model;
using Game.View;
using Runestone.AesirArchitecture;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Board
{
    /// <summary>
    /// 关卡作弊器（测试用）：直接通过当前关卡并跳到之后的关卡，专治卡关拖慢测试效率。
    /// <para>
    /// 按钮入口画在关卡场景自带 <see cref="BoardRuntimeDriver" /> 的 Inspector 上
    /// （Assets/Editor/LevelCheatInspector.cs，不改任何场景）；本类是运行时静态入口，
    /// Play 验证脚本也能直接调用。
    /// </para>
    /// <para>
    /// 通关刻意走真实结算链（<see cref="BoardRunModel.ForceFinish" /> → RunFinished →
    /// 记录 / 星级 / 成就 / 通关面板照常发生），不是绕过业务的假数据——作弊的同时也在回归主流程。
    /// </para>
    /// </summary>
    public static class LevelCheat
    {
        /// <summary>连跳保护：一次「进下一关」含 1.2s 过场 + 淡出，冷却期内的重复调用直接忽略。</summary>
        const float PassCooldownSeconds = 3f;

        static float _lastPassTime = -999f;

        /// <summary>
        /// 直接判定当前关卡胜利（铺路 / 运行 / 已失败等任意阶段均可触发）。
        /// 走正常结算链：停秒表、写通关记录（最短用时 + 星级 + 成就）、弹通关面板。
        /// 返回是否成功触发（不在关卡场景时失败）。
        /// </summary>
        public static bool FinishCurrentLevel()
        {
            var run = BoardContext.Instance != null ? BoardContext.Instance.GetModel<BoardRunModel>() : null;
            if (run == null)
            {
                Debug.LogWarning("[LevelCheat] 未找到 BoardRunModel（当前不在关卡场景？），作弊无效");
                return false;
            }

            if (run.CurrentPhase.Value == BoardRunModel.Phase.Settled &&
                run.Result.Value == BoardRunModel.RunResult.Win)
            {
                Debug.Log("[LevelCheat] 本关已是通关状态，无需重复触发");
                return true;
            }

            run.ForceFinish(BoardRunModel.RunResult.Win);
            Debug.Log($"[LevelCheat] 作弊通关：{SceneManager.GetActiveScene().name}，" +
                      $"用时 {_timeElapsedText(run)}，{run.StepCount.Value} 步，金币 {run.CoinsCollected.Value}/{run.CoinTotal}");
            return true;
        }

        /// <summary>
        /// 通关当前关卡并立即进入下一关（走加载过场，与本工程其它跳转同链路）。
        /// 已是最后一关时回到选关界面，方便顺势检查各关解锁状态。
        /// 返回是否成功发起跳转。
        /// </summary>
        public static bool PassAndGotoNextLevel()
        {
            if (Time.unscaledTime - _lastPassTime < PassCooldownSeconds)
            {
                Debug.Log("[LevelCheat] 正在跳转中，忽略重复调用");
                return false;
            }

            if (!FinishCurrentLevel()) return false;

            var target = ResolveNextScenePath(out var targetName);
            if (target == null)
            {
                Debug.LogWarning("[LevelCheat] 当前场景不在关卡表里，找不到「下一关」");
                return false;
            }

            // 预检 Build Settings，避免框架加载失败的 Error 噪音（与 LevelDetailPanel 同款预检）
            if (SceneUtility.GetBuildIndexByScenePath(target) < 0)
            {
                Debug.LogWarning($"[LevelCheat] 场景 \"{target}\" 未登记 Build Settings，无法跳转");
                return false;
            }

            _lastPassTime = Time.unscaledTime;
            Debug.Log($"[LevelCheat] 通关成功，前往 {targetName}（{target}）");
            LoadingPanel.Begin(target);
            return true;
        }

        /// <summary>
        /// 按关卡表解析「下一关」的完整场景路径；当前场景不在表里返回 null；
        /// 已是最后一关则返回选关界面路径（<see cref="ScenePaths.LevelSelect" />）。
        /// </summary>
        static string ResolveNextScenePath(out string targetName)
        {
            targetName = null;
            var sceneName = SceneManager.GetActiveScene().name;

            var hasCurrent = false;
            var currentId = int.MaxValue;
            LevelDef next = null;
            foreach (var def in GameContext.Instance.GetModel<LevelSelectModel>().Levels)
            {
                if (def.sceneName == sceneName)
                {
                    hasCurrent = true;
                    currentId = def.levelId;
                    continue;
                }

                // 容忍关卡表乱序：取 levelId 比当前大的最小者
                if (hasCurrent && def.levelId > currentId && (next == null || def.levelId < next.levelId))
                    next = def;
            }

            if (!hasCurrent) return null;

            if (next != null)
            {
                targetName = $"第 {next.levelId} 关";
                return "Assets/Scenes/" + next.sceneName + ".unity";
            }

            targetName = "选关界面（已是最后一关）";
            return ScenePaths.LevelSelect;
        }

        static string _timeElapsedText(BoardRunModel run)
        {
            var time = BoardContext.Instance.GetModel<BoardTimeModel>();
            return time != null ? time.FormatElapsed() : "?";
        }
    }
}
