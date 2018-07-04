﻿using JetBrains.ReSharper.Plugins.Unity.CSharp.Daemon.Errors;
using NUnit.Framework;

namespace JetBrains.ReSharper.Plugins.Unity.Tests.Daemon.Stages.Analysis
{
    [TestUnity]
    public class UnityNullCoalescingWarningTests : CSharpHighlightingTestBase<UnityObjectNullCoalescingWarning>
    {
        protected override string RelativeTestDataPath => @"CSharp\Daemon\Stages\Analysis";

        [Test] public void TestUnityNullCoalescingWarning() { DoNamedTest2(); }
    }
}
