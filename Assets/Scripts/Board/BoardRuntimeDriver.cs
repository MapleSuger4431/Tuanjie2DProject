using Game.Core;
using Runestone.AesirModules;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 关卡场景引导：一行把运行时面板从 Resources 拉起来挂到 UIRoot 下。
    /// 场景不再自带 Canvas / 占位白板——那些都在面板预制体里。
    /// 面板路径走 <see cref="UIPaths" /> 常量，改预制体位置只改常量。
    /// </summary>
    public sealed class BoardRuntimeDriver : MonoBehaviour
    {
        [SerializeField] string panelPath = UIPaths.BoardRuntimePanel;

        void Start()
        {
            // 每次进入关卡都重置运行态：BoardContext 跨场景常驻，重进同一关时 BoardRunModel 仍停在上一局的
            // Settled（阶段 / 结果 / 步数 / AI 位置），不重置则「Play」都点不动——进得去却玩不了。
            // 地形由场景自身的 ApplyPreset 重建，这里只管运行态与秒表。
            BoardContext.Instance.GetModel<BoardRunModel>().ResetRun();

            UIModule.Show<BoardRuntimePanel>(path: panelPath);

            // 右下角成就 / 消息小窗随关卡常驻：它只订阅 BoardTipModel，没消息时整块停用
            UIModule.Show<LevelTipPanel>(path: UIPaths.LevelTipPanel);
        }
    }
}
