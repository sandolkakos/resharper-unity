using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Application.changes;
using JetBrains.DataFlow;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Plugins.Unity.AsmDef.Psi.Caches;
using JetBrains.ReSharper.Plugins.Unity.Packages;
using JetBrains.ReSharper.Plugins.Unity.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.Util;

#nullable enable

namespace JetBrains.ReSharper.Plugins.Unity.CSharp.Feature.Services.ExternalSources
{
    [SolutionComponent]
    public class PreProcessingDirectiveCache
    {
        private readonly ISolution mySolution;
        private readonly AsmDefNameCache myAsmDefNameCache;
        private readonly UnitySolutionTracker myUnitySolutionTracker;
        private readonly UnityVersion myUnityVersion;
        private readonly PackageManager myPackageManager;
        private readonly IPsiServices myPsiServices;
        private readonly ILogger myLogger;
        private readonly Dictionary<VirtualFileSystemPath, PreProcessingDirective[]> myAssemblyNameToDirectiveCache = new();
        private readonly VirtualFileSystemPath myScriptAssembliesPath;

        public PreProcessingDirectiveCache(Lifetime lifetime,
                                           ISolution solution,
                                           AsmDefNameCache asmDefNameCache,
                                           UnitySolutionTracker unitySolutionTracker,
                                           UnityVersion unityVersion,
                                           ChangeManager changeManager,
                                           PackageManager packageManager,
                                           IPsiServices psiServices,
                                           ILogger logger)
        {
            mySolution = solution;
            myAsmDefNameCache = asmDefNameCache;
            myUnitySolutionTracker = unitySolutionTracker;
            myUnityVersion = unityVersion;
            myPackageManager = packageManager;
            myPsiServices = psiServices;
            myLogger = logger;

            myScriptAssembliesPath = solution.SolutionDirectory.Combine("Library/ScriptAssemblies");

            changeManager.Changed2.Advise(lifetime, OnChange);
            packageManager.Updating.Change.Advise_NoAcknowledgement(lifetime, OnPackagesUpdated);
            asmDefNameCache.CacheUpdated.Advise(lifetime, OnAsmDefCacheUpdated);
            myUnityVersion.ActualVersionForSolution.Advise(lifetime, OnApplicationVersionChanged);
        }

        public PreProcessingDirective[] GetPreProcessingDirectives(IPsiAssembly assembly)
        {
            var location = assembly.Location?.AssemblyPhysicalPath;
            if (myUnitySolutionTracker.IsUnityProject.Value && location != null)
            {
                return myAssemblyNameToDirectiveCache.GetOrCreateValue(location, () =>
                {
                    // Packages and modules will be compiled to Library/ScriptAssemblies. Framework code won't, and
                    // core Unity reference assemblies (e.g. UnityEngine.CoreModule.dll) aren't copied there
                    if (myScriptAssembliesPath.IsPrefixOf(location))
                    {
                        myLogger.Verbose("Request define symbols for {0}", location);

                        // Fortunately, Unity seems to use the same base set of defines for all assemblies, from
                        // packages to user code - this includes UNITY_EDITOR and the current platform defines. We can
                        // just take the defines for Assembly-CSharp and use that for all packages.
                        // We need to add any extra conditional defines set up in .asmdef per external assembly
                        // We can verify all this with CompilationPipeline.GetDefinesFromAssemblyName
                        var directives = GetBaseDefines();
                        AddConditionalAsmdefDefines(assembly.AssemblyName.Name, directives);
                        return directives.ToArray();
                    }

                    return EmptyArray<PreProcessingDirective>.Instance;
                });
            }

            return EmptyArray<PreProcessingDirective>.Instance;
        }

        private List<PreProcessingDirective> GetBaseDefines()
        {
            var directives = new List<PreProcessingDirective>();
            var mainProject = mySolution.GetProjectsByName("Assembly-CSharp").FirstOrDefault();
            if (mainProject == null)
            {
                myLogger.Warn("Cannot find Assembly-CSharp project!");
                return directives;
            }

            foreach (var psiModule in mainProject.GetPsiModules())
            {
                // We're only interested in the defines set up in the main project module, so skip any others, such
                // as UnityShaderModule
                if (psiModule is IProjectPsiModule)
                {
                    // GetAllDefines will get the defines for the first active configuration. Generated projects only
                    // have a single configuration - Debug
                    directives.AddRange(psiModule.GetAllDefines());
                }
            }

            return directives;
        }

        private void AddConditionalAsmdefDefines(string assemblyName, List<PreProcessingDirective> directives)
        {
            foreach (var versionDefine in myAsmDefNameCache.GetVersionDefines(assemblyName))
            {
                var resourceVersion = GetVersionOfResource(versionDefine.ResourceName);
                if (resourceVersion != null && versionDefine.VersionRange.IsValid(resourceVersion))
                    directives.Add(new PreProcessingDirective(versionDefine.Symbol, string.Empty));
            }
        }

        private JetSemanticVersion? GetVersionOfResource(string resourceName)
        {
            var packageData = myPackageManager.GetPackageById(resourceName);
            if (packageData != null)
            {
                return JetSemanticVersion.TryParse(packageData.PackageDetails.Version, out var version)
                    ? version
                    : null;
            }

            // Undocumented resource that represents the application version
            if (resourceName == "Unity")
            {
                return JetSemanticVersion.TryParse(myUnityVersion.ActualVersionForSolution.Value.ToString(3),
                    out var version)
                    ? version
                    : null;
            }

            return null;
        }

        private void OnChange(ChangeEventArgs args)
        {
            if (!myUnitySolutionTracker.IsUnityProject.Value)
                return;

            var projectModelChange = args.ChangeMap.GetChange<ProjectModelChange>(mySolution);
            if (projectModelChange == null || projectModelChange.IsOpeningSolution ||
                projectModelChange.IsClosingSolution)
            {
                return;
            }

            projectModelChange.VisitItemDeltasRecursively(change =>
            {
                if (change.IsPropertiesChanged && change.ProjectItem is IProject project && project.IsAssemblyCSharp())
                    Invalidate("Properties change for Assembly-CSharp");
            });
        }

        private void OnPackagesUpdated(PropertyChangedEventArgs<bool?> args)
        {
            if (args.HasNew && args.New is false)
                Invalidate("Packages updated");
        }

        private void OnAsmDefCacheUpdated(bool _)
        {
            Invalidate("AsmDefCache updated");
        }

        private void OnApplicationVersionChanged(Version _)
        {
            Invalidate("Application version changed");
        }

        private void Invalidate(string reason)
        {
            myLogger.Verbose($"Invalidating PreProcessingDirectiveCache. Reason: {reason}");

            myAssemblyNameToDirectiveCache.Clear();

            IList<IProjectFile> projectFiles;
            using (ReadLockCookie.Create())
                projectFiles = myPsiServices.Solution.MiscFilesProject.GetAllProjectFiles().ToList();

            using (WriteLockCookie.Create())
            {
                foreach (var projectFile in projectFiles)
                {
                    if (projectFile.LanguageType.Is<CSharpProjectFileType>())
                        myPsiServices.MarkAsDirty(projectFile);
                }
            }
        }
    }
}