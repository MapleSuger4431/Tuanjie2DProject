using UnityEngine;
using Runestone.AesirModules;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 场景引导：一行把运行时面板从 Resources 拉起来挂到 UIRoot 下。
    /// 场景不再自带 Canvas / 占位白板——那些都在面板预制体里。
    /// </summary>
    public sealed class BoardRuntimeDriver : MonoBehaviour
    {
        [SerializeField] string panelPath = "UIPrefabs/BoardRuntimePanel";

        void Start() => UIModule.Show<BoardRuntimePanel>(path: panelPath);
    }
}
