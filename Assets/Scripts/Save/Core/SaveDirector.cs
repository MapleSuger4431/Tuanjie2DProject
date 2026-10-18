using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Save
{
    /// <summary>
    /// 存档总控：按注册顺序依次让每个模块的 Step 把 Model 写进根存档（读档时反向回填），
    /// 全部模块处理完后统一序列化落盘。
    /// 扩展：新增模块 = 写一个 ISaveStep + Register 一次，已有模块与流程不动。
    /// </summary>
    public sealed class SaveDirector
    {
        readonly List<ISaveStep> _steps = new List<ISaveStep>();

        /// <summary>当前已注册的模块顺序。</summary>
        public IReadOnlyList<ISaveStep> Steps => _steps;

        public SaveDirector Register(ISaveStep step)
        {
            if (step == null) return this;
            _steps.Add(step);
            return this;
        }

        /// <summary>按顺序收集各模块数据 → 统一落盘。</summary>
        public void Save(string path)
        {
            var root = new SaveGame();
            root.Stamp(DateTime.UtcNow.ToString("o"));

            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].Collect(root);
            }

            SaveStorage.Write(path, root);
            Debug.Log($"[Save] 落盘完成：{path}（{_steps.Count} 个模块）");
        }

        /// <summary>读盘 → 按顺序回填各模块。返回是否读到存档。</summary>
        public bool Load(string path)
        {
            var root = SaveStorage.Read(path);
            if (root == null)
            {
                Debug.Log($"[Save] 没有可读存档：{path}（按默认值继续）");
                return false;
            }

            if (root.Version != SaveGame.CurrentVersion)
            {
                Debug.LogWarning(
                    $"[Save] 存档版本 {root.Version} ≠ 当前 {SaveGame.CurrentVersion}，按当前结构尽力读取（迁移逻辑在此扩展）");
            }

            for (int i = 0; i < _steps.Count; i++)
            {
                _steps[i].Apply(root);
            }

            Debug.Log($"[Save] 读档完成：{path}（{_steps.Count} 个模块，存档时间 {root.SavedAtUtc}）");
            return true;
        }
    }
}
