using UnityEngine;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;

namespace Game.Core
{
    /// <summary>
    /// 启动场景唯一引导脚本：初始化 Context，随后经 SceneModule 直接切换到主场景。
    /// 场景内除本脚本（与 AudioListener）外不放任何东西。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        void Start()
        {
            // 触发 Context 初始化（执行 Configure，注册全部 Model / Service）
            _ = GameContext.Instance;

            // Bootstrap 之后直接接主场景：完整资产路径（Build Settings 已登记），
            // 首次调用自动创建 [Aesir Modules] 宿主与 SceneModule（主菜单场景保留为独立资产，后续按需接入）
            // （onFailed 为无参 Action，失败原因由 SceneModule 内部 LogError）
            SceneModule.LoadSceneSingle(ScenePaths.MainScene,
                onFailed: () => Debug.LogError($"[GameBootstrap] 主场景切换失败：{ScenePaths.MainScene}"));
        }
    }
}
