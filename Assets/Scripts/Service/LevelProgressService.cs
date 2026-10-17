using Game.Model;
using Runestone.AesirArchitecture;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Service
{
    /// <summary>
    /// 关卡通关记录的写入门面：把「某场景的关卡」翻译成 levelId，再写入
    /// <see cref="LevelProgressModel" />（唯一真源，随存档持久化）。
    /// <para>
    /// 关卡场景内（BoardContext）没有 GameContext 的 Model，因此由本服务做桥：
    /// 玩法侧只需给出「本次用时与步数」，场景名 → levelId 的反查在这里完成。
    /// </para>
    /// <para>失败不写记录：只有通关（Win）才调用。</para>
    /// </summary>
    public sealed class LevelProgressService : AbstractService
    {
        LevelProgressModel _progress;
        LevelSelectModel _levels;

        protected override void OnInitialize()
        {
            _progress = this.GetModel<LevelProgressModel>();
            _levels = this.GetModel<LevelSelectModel>();
        }

        public LevelProgressModel Progress => _progress;

        /// <summary>按场景名反查关卡编号（与 <see cref="LevelDef.sceneName" /> 对齐）。</summary>
        public bool TryResolveLevelId(string sceneName, out int levelId)
        {
            levelId = 0;
            if (string.IsNullOrEmpty(sceneName)) return false;

            foreach (var def in _levels.Levels)
            {
                if (def.sceneName != sceneName) continue;

                levelId = def.levelId;
                return true;
            }

            return false;
        }

        /// <summary>记录一次通关，返回是否刷新了最短用时纪录。</summary>
        public bool RecordClear(int levelId, float seconds, int steps)
        {
            var isNewRecord = _progress.RecordClear(levelId, seconds, steps);

            Debug.Log($"[LevelProgress] 第 {levelId} 关通关：本次 {seconds:F1}s（{steps} 步），" +
                      $"破纪录={isNewRecord}，历史最短 {_progress.GetBestTime(levelId):F1}s，" +
                      $"第 {_progress.GetClearCount(levelId)} 次通关");

            return isNewRecord;
        }

        /// <summary>记录当前关卡场景的一次通关（场景名不在关卡表里时只告警，不写记录）。</summary>
        public bool RecordClearForCurrentScene(float seconds, int steps)
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!TryResolveLevelId(sceneName, out var levelId))
            {
                Debug.LogWarning($"[LevelProgress] 当前场景 \"{sceneName}\" 不在关卡表里，本次通关未记录");
                return false;
            }

            return RecordClear(levelId, seconds, steps);
        }
    }
}
