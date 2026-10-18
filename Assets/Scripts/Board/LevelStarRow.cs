using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Board
{
    /// <summary>
    /// 结算面板的星级行：三颗星**逐颗点亮**（缩放弹入 + 由暗转金），未拿到的星留在暗底。
    /// 纯表现组件——拿几星由 <see cref="LevelStarRules" /> 算，本类只负责把数字演出来。
    /// <para>节点由 <c>BoardResultPanelBuilder</c> 生成；星星图标走项目美术（选关用图 UIimg/star.png）。</para>
    /// </summary>
    public sealed class LevelStarRow : MonoBehaviour
    {
        [SerializeField, Tooltip("三颗星（数组顺序即点亮顺序）")]
        Image[] stars;

        [SerializeField, Tooltip("星星图标；为空时退化成纯色方块，仍能看清点亮过程")]
        Sprite starSprite;

        [SerializeField, Tooltip("未点亮的颜色")]
        Color offColor = new Color(1f, 1f, 1f, 0.22f);

        [SerializeField, Tooltip("点亮后的颜色")]
        Color onColor = new Color(1f, 0.84f, 0.32f);

        [SerializeField, Tooltip("逐颗点亮的间隔（秒）")]
        float stepDelay = 0.32f;

        [SerializeField, Tooltip("单颗弹出动画时长（秒）")]
        float popDuration = 0.3f;

        [SerializeField, Tooltip("弹出时的峰值缩放")]
        float popScale = 1.5f;

        Coroutine _playing;

        /// <summary>点亮 <paramref name="count" /> 颗星（超过星位数的数值按满星处理）。</summary>
        public void Play(int count)
        {
            if (stars == null || stars.Length == 0) return;

            if (_playing != null) StopCoroutine(_playing);

            // 面板 OnInit 时 GameObject 还没激活（协程起不来）：那一次只归位，
            // 真正的点亮演出等 OnShow（激活后）再来一遍
            if (!gameObject.activeInHierarchy)
            {
                _playing = null;
                ResetStars();
                return;
            }

            _playing = StartCoroutine(Animate(Mathf.Clamp(count, 0, stars.Length)));
        }

        /// <summary>全部熄灭（面板复用 / 语言刷新时的过渡态）。</summary>
        public void Reset()
        {
            if (_playing != null)
            {
                StopCoroutine(_playing);
                _playing = null;
            }

            ResetStars();
        }

        IEnumerator Animate(int count)
        {
            ResetStars();

            for (int i = 0; i < count; i++)
            {
                if (i > 0) yield return new WaitForSecondsRealtime(stepDelay);
                yield return Pop(i);
            }

            _playing = null;
        }

        /// <summary>单颗弹出：缩放走 sin 半周（起止都是 1），颜色同步由暗转金。</summary>
        IEnumerator Pop(int index)
        {
            var image = stars[index];
            if (image == null) yield break;

            var duration = Mathf.Max(0.016f, popDuration);
            var time = 0f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(time / duration);
                image.rectTransform.localScale =
                    Vector3.one * (1f + (popScale - 1f) * Mathf.Sin(t * Mathf.PI));
                image.color = Color.Lerp(offColor, onColor, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            image.rectTransform.localScale = Vector3.one;
            image.color = onColor;
        }

        void ResetStars()
        {
            if (stars == null) return;

            for (int i = 0; i < stars.Length; i++)
            {
                var image = stars[i];
                if (image == null) continue;

                image.rectTransform.localScale = Vector3.one;
                image.color = offColor;
                if (starSprite != null) image.sprite = starSprite;
            }
        }
    }
}
