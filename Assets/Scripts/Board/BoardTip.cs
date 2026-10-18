using Game.Core;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 关卡内消息小窗的静态门面：一行 `BoardTip.Send(...)` 就能从任何玩法代码里弹消息，
    /// 不需要拿到面板实例（面板常驻在 UIRoot 上，由 <see cref="BoardRuntimeDriver" /> 拉起）。
    /// <para>
    /// 只负责把消息送进 <see cref="BoardTipModel" />；BoardContext 尚未初始化（关卡之外）时静默跳过并给一条警告。
    /// </para>
    /// </summary>
    public static class BoardTip
    {
        /// <summary>发一条消息（文本已定稿）。</summary>
        public static void Send(string title, string body)
        {
            var model = BoardContext.Instance.GetModel<BoardTipModel>();
            if (model == null)
            {
                Debug.LogWarning("[BoardTip] BoardTipModel 未注册，消息被丢弃：" + title);
                return;
            }

            model.Send(title, body);
        }

        /// <summary>标题走本地化键，正文是已定稿文本。</summary>
        public static void Send(Localization.Key title, string body) => Send(Localization.Get(title), body);

        /// <summary>标题与正文都走本地化键。</summary>
        public static void Send(Localization.Key title, Localization.Key body) =>
            Send(Localization.Get(title), Localization.Get(body));

        /// <summary>标题走本地化键，正文是带占位符的本地化文本（{0} 由 args 填）。</summary>
        public static void Send(Localization.Key title, Localization.Key body, params object[] args) =>
            Send(Localization.Get(title), Localization.Get(body, args));
    }
}
