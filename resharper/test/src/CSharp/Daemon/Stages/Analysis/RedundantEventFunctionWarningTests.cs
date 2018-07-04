﻿using JetBrains.ReSharper.Plugins.Unity.CSharp.Daemon.Errors;
using NUnit.Framework;

namespace JetBrains.ReSharper.Plugins.Unity.Tests.Daemon.Stages.Analysis
{
    [TestUnity]
    public class RedundantEventFunctionWarningTests : CSharpHighlightingTestBase<RedundantEventFunctionWarning>
    {
        protected override string RelativeTestDataPath => @"CSharp\Daemon\Stages\Analysis";

        [Test] public void TestRedundantEventFunctionWarning() { DoNamedTest2(); }
    }
}