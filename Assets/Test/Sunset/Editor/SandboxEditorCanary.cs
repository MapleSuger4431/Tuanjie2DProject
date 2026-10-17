namespace Sunset.Test.Editor
{
    /// <summary>
    /// 编辑器金丝雀脚本：引用 Sunset.Test 的类型，
    /// 用于证明 Sunset.Test.Editor -&gt; Sunset.Test 引用链正常编译。可随时安全删除。
    /// </summary>
    public static class SandboxEditorCanary
    {
        public static string Describe(Sunset.Test.SandboxCanary canary)
        {
            return typeof(Sunset.Test.SandboxCanary).FullName;
        }
    }
}
