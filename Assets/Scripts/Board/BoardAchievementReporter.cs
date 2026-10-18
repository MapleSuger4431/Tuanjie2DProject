using Game.Core;
using Game.Model;
using Game.Service;
using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 关卡侧的成就上报器：把棋盘玩法事件翻译成成就事件上报给 <see cref="AchievementService" />，
    /// 并在解锁时弹右下角小窗（复用 <see cref="BoardTip" />）。
    /// <para>
    /// 由 <see cref="BoardRuntimeDriver" /> 在关卡启动时现场挂上（**不改场景**），依赖方向单向：Board → Service。
    /// 判定与持久化都在服务与 Model 里，这里只做「发生了什么」的上报。
    /// </para>
    /// </summary>
    public sealed class BoardAchievementReporter : MonoBehaviour
    {
        BoardRunModel _run;
        AchievementService _achievements;
        bool _hooked;

        void Start()
        {
            _run = BoardContext.Instance.GetModel<BoardRunModel>();
            _achievements = GameContext.Instance.GetService<AchievementService>();

            if (_run == null || _achievements == null)
            {
                Debug.LogWarning("[成就上报] BoardRunModel 或 AchievementService 缺失，本关不统计成就");
                return;
            }

            // 同一实例只挂一次：面板与驱动器都可能触发本组件的初始化
            if (_hooked) return;
            _hooked = true;

            _run.StepCount.AddListener(OnStepCountChanged)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _run.Element.AddListener(_ => OnElementChanged())
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
            _run.RunFinished.AddListener(OnRunFinished)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);

            _achievements.Unlocked.AddListener(OnAchievementUnlocked)
                .RemoveListenerWhenGameObjectOnDestroyed(gameObject);
        }

        /// <summary>每走一步上报当前步数（30 步成就靠它累计）。</summary>
        void OnStepCountChanged(int steps) => _achievements.Report(AchievementEvents.StepsInLevel, steps);

        /// <summary>元素属性反转：只在行进中算（出生与重开复位不算）。</summary>
        void OnElementChanged()
        {
            if (_run == null || !_run.IsRunning || _run.StepCount.Value <= 0) return;

            _achievements.Report(AchievementEvents.ElementSwitched, 1);
        }

        /// <summary>通关：首通 + 累计通关次数（次数由关卡进度记录汇总，重打同一关也计数）。</summary>
        void OnRunFinished()
        {
            if (_run == null || _run.Result.Value != BoardRunModel.RunResult.Win) return;

            _achievements.Report(AchievementEvents.LevelCleared, 1);
            _achievements.Report(AchievementEvents.TotalClears, TotalClearCount());
        }

        /// <summary>解锁即弹右下角小窗：标题「成就解锁」+ 成就名（走本地化键名取值）。</summary>
        void OnAchievementUnlocked(GameAchievement achievement)
        {
            if (achievement == null) return;

            BoardTip.Send(Localization.Key.TipAchievement, Localization.Get(achievement.TitleKey));
        }

        /// <summary>历史累计通关次数：各关通关次数求和（关卡进度记录是真源）。</summary>
        static int TotalClearCount()
        {
            var progress = GameContext.Instance.GetModel<LevelProgressModel>();
            if (progress == null) return 1;

            var total = 0;
            foreach (var record in progress.Records) total += record.clearCount;

            return Mathf.Max(total, 1);
        }
    }
}
