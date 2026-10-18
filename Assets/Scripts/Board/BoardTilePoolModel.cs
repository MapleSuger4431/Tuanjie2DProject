using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Runestone.AesirArchitecture;

namespace Game.Board
{
    /// <summary>
    /// 本关的随机表：进入关卡时从 Resources 读一次 <see cref="BoardTilePoolConfig" />，按当前场景名取出该关那一份。
    /// 必须最先注册——<see cref="PieceModel" /> 初始化抽第一块时就要向它要逐格状态。
    /// </summary>
    [Serializable]
    public sealed class BoardTilePoolModel : AbstractModel
    {
        BoardTilePoolConfig _config;
        BoardTilePoolConfig.LevelPool _pool;
        string _sceneName;

        public BoardTilePoolConfig Config => _config;

        /// <summary>当前解析到的关卡场景名。</summary>
        public string SceneName => _sceneName;

        /// <summary>配置资产是否读到了（读不到会走内置默认表）。</summary>
        public bool HasConfig => _config != null;

        /// <summary>本关是否配了随机表（没配走默认表）。</summary>
        public bool HasLevelPool => _pool != null;

        protected override void OnInitialize() => ApplyScene(SceneManager.GetActiveScene().name);

        /// <summary>按场景名重新解析（关卡切换、调试时手动指定都用这里）。</summary>
        public void ApplyScene(string sceneName)
        {
            _sceneName = sceneName;
            _config = Resources.Load<BoardTilePoolConfig>(BoardTilePoolConfig.ResourcesPath);

            if (_config == null)
            {
                Debug.LogWarning($"[BoardTilePoolModel] Resources 下找不到 {BoardTilePoolConfig.ResourcesPath}，方块状态走内置默认表");
                _pool = null;
                return;
            }

            _pool = _config.PoolFor(sceneName);
            Debug.Log($"[BoardTilePoolModel] 关卡 {sceneName} 随机表：{BoardTilePoolConfig.Describe(_pool)}");
        }

        /// <summary>抽一个状态。</summary>
        public TileType RandomType() => RollTypes(1)[0];

        /// <summary>一口气抽 count 个（方块每一格独立抽，块内可混出多种状态）。</summary>
        public TileType[] RollTypes(int count) =>
            _config != null
                ? _config.RandomTypes(_pool, count)
                : BoardTilePoolConfig.DefaultTypes(count);

        /// <summary>本关随机表的文字描述，供日志与调试面板使用。</summary>
        public string Describe() => BoardTilePoolConfig.Describe(_pool);
    }
}
