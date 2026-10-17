using UnityEngine;
using Runestone.AesirModules;

/// <summary>
/// 选关界面测试入口：挂在 LevelSelect.unity 场景里，
/// Play 时一行 Show 装配面板（框架零预放置——场景全空也能跑）。
/// </summary>
public class TestTFSelectLauncher : MonoBehaviour
{
    private void Start()
    {
        UIModule.Show<LevelSelectPanel>(path: TestTFUIPaths.LevelSelectPanel);
    }
}
