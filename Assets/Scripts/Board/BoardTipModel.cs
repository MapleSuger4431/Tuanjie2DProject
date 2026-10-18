using Runestone.AesirArchitecture;
using UnityEngine;

namespace Game.Board
{
    /// <summary>
    /// 小窗的单条消息（标题 + 正文）。
    /// <para>
    /// 刻意做成**引用类型**：<see cref="ObservableValue{T}" /> 用引用比较判等，
    /// 每次发送都 new 一个新实例，于是「同一句文案连发两次」也能各弹一次
    /// （值类型会因「赋同值不触发」把第二条悄悄吞掉）。
    /// </para>
    /// </summary>
    public sealed class BoardTipMessage
    {
        /// <summary>标题（如「成就解锁」）。</summary>
        public readonly string Title;

        /// <summary>正文（如成就名 / 新纪录用时）。</summary>
        public readonly string Body;

        public BoardTipMessage(string title, string body)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
        }
    }

    /// <summary>
    /// 关卡内消息小窗的数据层：只持一条「当前消息」，发送即覆盖。
    /// 播放节奏（5 秒、排队、淡入淡出）是视图职责，一律在 <see cref="LevelTipPanel" /> 里，不进 Model。
    /// </summary>
    public sealed class BoardTipModel : AbstractModel
    {
        [SerializeField] ObservableValue<BoardTipMessage> message = new ObservableValue<BoardTipMessage>(null);

        /// <summary>当前消息；面板订阅它。发送方可走 <see cref="BoardTip" /> 静态门面。</summary>
        public ObservableValue<BoardTipMessage> Message => message;

        /// <summary>发一条消息：标题 + 正文都已是最终文本（本地化在发送侧解析）。</summary>
        public void Send(string title, string body) => message.Value = new BoardTipMessage(title, body);
    }
}
