﻿using JetBrains.Annotations;
using JetBrains.ReSharper.Plugins.Unity.JsonNew.Psi.Tree;
using JetBrains.ReSharper.Psi.Tree;

#nullable enable

namespace JetBrains.ReSharper.Plugins.Unity.AsmDef
{
    public static class Extensions
    {
        [ContractAnnotation("node:null => false")]
        public static bool IsNamePropertyValue(this ITreeNode? node) =>
            node.AsStringLiteralValue().IsRootPropertyValue("name");

        [ContractAnnotation("node:null => false")]
        public static bool IsReferencesArrayEntry(this ITreeNode? node)
        {
            var value = node.AsStringLiteralValue();
            var array = JsonNewArrayNavigator.GetByValue(value);
            return array.IsRootPropertyValue("references");
        }

        public static bool IsDefineConstraintsArrayEntry(this ITreeNode? node)
        {
            var value = node.AsStringLiteralValue();
            var array = JsonNewArrayNavigator.GetByValue(value);
            return array.IsRootPropertyValue("defineConstraints");
        }

        public static bool IsVersionDefinesObjectDefineValue(this ITreeNode? node)
        {
            var value = node.AsStringLiteralValue();
            var defineProperty = value.GetNamedMemberByValue("define");
            var versionDefineObject = JsonNewObjectNavigator.GetByMember(defineProperty);
            var versionDefinesArray = JsonNewArrayNavigator.GetByValue(versionDefineObject);
            return versionDefinesArray.IsRootPropertyValue("versionDefines");
        }
    }
}