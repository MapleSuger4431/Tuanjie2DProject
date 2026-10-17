using System;
using Cysharp.Threading.Tasks;
using Game.Service;
using Runestone.AesirArchitecture;
using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Core
{
    /// <summary>
    /// 启动场景唯一引导脚本：初始化 Context、读档，等本地化资源（Addressables）就绪后进入主场景。
    /// <para>
    /// 等待本地化用 UniTask 异步完成（<c>await</c> 让出主线程，不阻塞、不轮询），
    /// 就绪的那一帧立刻切场景，因此启动流程不会被同步等待卡住。
    /// </para>
    /// 场景内除本脚本（与 AudioListener）外不放任何东西。
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        void Start()
        {
            // Forget()：启动流程是 fire-and-forget，异常由 UniTask 自己打日志，不阻塞 Start
            BootstrapAsync().Forget();
        }

        /// <summary>
        /// 启动主流程：初始化 Context → 读档 → 等本地化就绪 → 切主场景。
        /// </summary>
        async UniTaskVoid BootstrapAsync()
        {
            // Context 初始化（注册全部 Model / Service）。
            // SettingsService 在这里就会触发本地化初始化，读档与本地化加载因此是并行的
            var context = GameContext.Instance;

            // 读档：按模块顺序回填（无存档时各模块保持默认值）。
            // 设置值回填进 SettingsModel 后由 SettingsService 自动下发到 AudioModule / Localization / Screen，
            // 因此这里不需要再单独调用一次应用
            context.GetService<SaveService>().Load();

            await AwaitLocalizationAsync();

            // 进入主场景：完整资产路径（Build Settings 已登记）
            // （onFailed 为无参 Action，失败原因由 SceneModule 内部 LogError）
            SceneTransition.LoadSingle(ScenePaths.MainMenu,
                onFailed: () => Debug.LogError($"[GameBootstrap] 主菜单场景切换失败：{ScenePaths.MainMenu}"));
        }

        /// <summary>
        /// 异步等本地化（Locale / 字符串表，走 Addressables）就绪。
        /// <para>
        /// 坑：<see cref="LocalizationSettings.InitializationOperation" /> 一旦完成就会被 Addressables 释放，
        /// 之后再取到的句柄 <c>IsValid()</c> 为 false——UniTask 的 <c>ToUniTask</c> 对无效句柄直接抛
        /// "Attempting to use an invalid operation handle"（协程 <c>yield return</c> 不报，故长期没暴露）。
        /// 这里先判有效且未完成再 await，并兜底 catch：无论如何都不让启动流程卡在这里。
        /// </para>
        /// </summary>
        async UniTask AwaitLocalizationAsync()
        {
            var op = LocalizationSettings.InitializationOperation;
            if (!op.IsValid() || op.IsDone)
            {
                return;   // 已完成（或被释放）：无需等待，直接切场景
            }

            try
            {
                await op;
            }
            catch (Exception e)
            {
                // 失败也继续切场景——文本会回退为键名，玩法不受影响
                Debug.LogWarning($"[GameBootstrap] 等待本地化初始化异常，直接切场景：{e.Message}");
            }
        }
    }
}
