using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.Threading;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Plugins.Json.Psi;
using JetBrains.ReSharper.Plugins.Json.Psi.Tree;
using JetBrains.ReSharper.Plugins.Unity.UnityEditorIntegration;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.Util;

#nullable enable

namespace JetBrains.ReSharper.Plugins.Unity.InputActions.Psi.Caches
{
    [PsiComponent]
    public class InputActionCache : SimpleICache<List<InputActionCacheItem>>
    {
        private readonly IShellLocks myShellLocks;
        private readonly OneToListMap<IPsiSourceFile, List<InputActionCacheItem>> myLocalCache = new();

        public InputActionCache(Lifetime lifetime,
            IShellLocks shellLocks,
            IPersistentIndexManager persistentIndexManager)
            : base(lifetime, shellLocks, persistentIndexManager, InputActionCacheItem.Marshaller)
        {
            myShellLocks = shellLocks;
        }

        public override object? Build(IPsiSourceFile sourceFile, bool isStartup)
        {
            if (!IsApplicable(sourceFile))
                return null;
            
            var file = sourceFile.GetDominantPsiFile<JsonNewLanguage>() as IJsonNewFile;
            var rootObject = file?.GetRootObject();

            var results = new List<InputActionCacheItem>();

            if (rootObject != null)
            {
                foreach (var member in rootObject.MembersEnumerable)
                {
                    if (member.Key == "maps")
                    {
                        if (member.Value is IJsonNewArray val)
                        {
                            foreach (var jsonNewValue in val.Values)
                            {
                                var jsonNewObject = (IJsonNewObject)jsonNewValue;
                                foreach (var newMember in jsonNewObject.MembersEnumerable)
                                {
                                    if (newMember is { Key: "actions" })
                                    {
                                        if (newMember.Value is IJsonNewArray actions)
                                        {
                                            foreach (var actionsValue in actions.Values)
                                            {
                                                foreach (var actionMembers in ((IJsonNewObject) actionsValue).Members)
                                                {
                                                    if (actionMembers.Key == "name")
                                                    {
                                                        var nameProperty = (IJsonNewLiteralExpression)actionMembers.Value;
                                                        results.Add(new InputActionCacheItem(nameProperty.GetStringValue(),
                                                            nameProperty.GetTreeStartOffset().Offset));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            return results;
        }

        public override void Merge(IPsiSourceFile sourceFile, object builtPart)
        {
            myLocalCache.RemoveKey(sourceFile);
            AddToLocalCache(sourceFile, builtPart as List<InputActionCacheItem>);
            base.Merge(sourceFile, builtPart);
        }

        private void AddToLocalCache(IPsiSourceFile sourceFile, List<InputActionCacheItem>? item)
        {
            if (item == null) return;
            myLocalCache.Add(sourceFile, item);
        }

        public override void MergeLoaded(object data)
        {
            foreach (var inputActionCacheItem  in Map)
                AddToLocalCache(inputActionCacheItem.Key, inputActionCacheItem.Value);
            base.MergeLoaded(data);
        }

        public override void Drop(IPsiSourceFile sourceFile)
        {
            myLocalCache.RemoveKey(sourceFile);
            base.Drop(sourceFile);
        }

        protected override bool IsApplicable(IPsiSourceFile sf)
        {
            return base.IsApplicable(sf) && sf.IsInputActions() && sf.IsLanguageSupported<JsonNewLanguage>();
        }

        public bool ContainsName(string name)
        {
            // ConcurrentDictionary<>
            // lock
            
            return myLocalCache.SelectMany(item => item.Value)
                .SelectMany(a=>a)
                .Any(inputActionCacheItem => inputActionCacheItem.Name == name);
        }
    }
}