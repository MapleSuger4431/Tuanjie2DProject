using UnityEngine;
using Runestone.AesirArchitecture;

namespace GameRuntimeTest.Board
{
    /// <summary>
    /// 秒表驱动器：每帧把 deltaTime 喂给 <see cref="BoardTimeModel" />。
    /// 用缩放时间——暂停菜单把 timeScale 打成 0 时秒表自动停走，无需额外接线。
    /// </summary>
    public sealed class BoardTimeTicker : MonoViewController<BoardContext>
    {
        BoardTimeModel _time;

        void Start() => _time = this.GetModel<BoardTimeModel>();

        void Update() => _time?.Tick(Time.deltaTime);
    }
}
