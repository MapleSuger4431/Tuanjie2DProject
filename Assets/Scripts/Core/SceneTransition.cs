using System;
using System.Collections.Generic;
using Runestone.AesirModules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 场景切换门面：先收起当前场景的全部面板，再走 <see cref="SceneModule" /> 加载目标场景。
    /// <para>
    /// 为什么必须显式收 UI：<see cref="UIRoot" /> 默认跨场景常驻（DontDestroyOnLoad），
    /// 挂在它分层 Canvas 下的面板**不会随旧场景卸载而消失**。
    /// 直接 LoadSceneSingle 的结果是旧界面原样叠在新场景画面上（主菜单盖在关卡里）。
    /// </para>
    /// <para>
    /// 收 UI 有两个触发点，缺一不可：
    /// ①<see cref="LoadSingle" /> 加载前主动收一次（本工程所有跳转都应走它）；
    /// ②<see cref="SceneManager.sceneLoaded" /> 钩子——任何绕过本门面的 Single 加载
    /// （编辑器里直接双击场景、第三方/框架内部调用 SceneModule 等）也一律收一次。
    /// 钩子排在场景加载完成之后、新场景脚本 Start 之前，因此新场景在 Start 里 Show 的面板不受影响。
    /// </para>
    /// <para>
    /// 只处理面板：本项目全 UI 一律 Panel（含弹窗），不使用 UIModule 的窗口 Open/Close 接口。
    /// </para>
    /// </summary>
    public static class SceneTransition
    {
        // 复用缓冲区：关闭过程中 HidePanel 可能销毁物体并改动层级，先快照再统一关闭
        static readonly List<IUIPanel> Panels = new List<IUIPanel>();
        static readonly List<Type> PanelTypes = new List<Type>();

        /// <summary>
        /// 订阅场景加载钩子。域加载时重新挂一次，避免重复订阅（先减后加）。
        /// 选 <see cref="RuntimeInitializeLoadType.AfterSceneLoad" />：早于任何场景脚本的 Start，
        /// 因此启动链（Bootstrap → 主菜单）与后续跳转都在钩子生效之后。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallSceneHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 关闭当前全部面板并单场景切换到 <paramref name="scenePath" />。
        /// </summary>
        /// <param name="scenePath">目标场景完整资产路径。</param>
        /// <param name="onFailed">失败回调（无参，失败原因由 SceneModule 内部 LogError）。</param>
        public static void LoadSingle(string scenePath, Action onFailed = null)
        {
            CloseAllPanels();
            SceneModule.LoadSceneSingle(scenePath,
                onFailed: onFailed ?? (() => Debug.LogError($"[SceneTransition] 场景切换失败：{scenePath}")));
        }

        /// <summary>
        /// 任意 Single 模式场景加载完成后收起面板：兜住所有不经 <see cref="LoadSingle" /> 的切换路径。
        /// 单场景语义下旧场景的界面必须一并消失；叠加加载（Additive）不动现有界面。
        /// </summary>
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
            {
                return;
            }

            CloseAllPanels();
        }

        /// <summary>
        /// 收起当前打开的全部面板。未创建 UIRoot 时（如启动链首次切场景）无副作用直接返回。
        /// </summary>
        public static void CloseAllPanels()
        {
            // 不访问 UIRoot.Instance——那会在没有 UI 时顺带创建一套；此处只清理既有的
            // 遍历全部 UIRoot 实例：单取一个（FindAnyObjectByType）时若存在多套层级会漏收
            var roots = UnityEngine.Object.FindObjectsOfType<UIRoot>(true);
            if (roots == null || roots.Length == 0)
            {
                return;
            }

            Panels.Clear();
            PanelTypes.Clear();

            foreach (var root in roots)
            {
                if (root != null)
                {
                    CollectPanels(root.transform);
                }
            }

            if (Panels.Count == 0)
            {
                return;
            }

            foreach (var panel in Panels)
            {
                var type = panel.GetType();
                if (!PanelTypes.Contains(type))
                {
                    PanelTypes.Add(type);
                }
            }

            // 先走框架：由面板自己的 destroyOnHide 决定销毁还是隐藏，注册表一并维护
            foreach (var type in PanelTypes)
            {
                UIModule.Instance.HidePanel(type);
            }

            // 兜底：不在 UIModule 注册表里的实例（旧模块实例的孤儿面板、绕过 UIModule 直接挂上的界面）
            // HidePanel 对它无能为力，这里直接隐藏，保证「换场景 = 旧界面必消失」
            foreach (var panel in Panels)
            {
                if (!(panel is MonoBehaviour mono) || mono == null || !mono.gameObject.activeSelf)
                {
                    continue;   // 已销毁或框架已收起
                }

                if (panel.IsOpen)
                {
                    panel.Hide();
                }
                else
                {
                    mono.gameObject.SetActive(false);
                }

                Debug.LogWarning($"[SceneTransition] 面板 {mono.name}（{panel.GetType().Name}）不在 UIModule 注册表中，" +
                                 "已由场景切换兜底隐藏——请排查是否绕过了 UIModule.Show 打开界面");
            }
        }

        /// <summary>递归收集某个节点子树下面板实例（面板挂在分层 Canvas 下，也可能嵌套在别的面板里）。</summary>
        static void CollectPanels(Transform parent)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                var panel = child.GetComponent<IUIPanel>();
                if (panel != null && !Panels.Contains(panel))
                {
                    Panels.Add(panel);
                }

                CollectPanels(child);
            }
        }
    }
}
