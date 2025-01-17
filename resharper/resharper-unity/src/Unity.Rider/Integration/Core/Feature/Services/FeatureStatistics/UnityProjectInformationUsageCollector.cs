using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Application.Threading;
using JetBrains.Collections.Viewable;
using JetBrains.Diagnostics;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.FeaturesStatistics;
using JetBrains.ReSharper.Feature.Services.Project;
using JetBrains.ReSharper.Plugins.Unity.Core.ProjectModel;
using JetBrains.ReSharper.Plugins.Unity.Core.Psi.Modules;
using JetBrains.ReSharper.Plugins.Unity.UnityEditorIntegration;
using JetBrains.ReSharper.Plugins.Unity.Yaml.Psi.Caches;
using JetBrains.ReSharper.Plugins.Yaml.Psi;
using JetBrains.ReSharper.Plugins.Yaml.Psi.Tree;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Resources.Shell;
using JetBrains.UsageStatistics.FUS.EventLog;
using JetBrains.UsageStatistics.FUS.EventLog.Events;
using JetBrains.UsageStatistics.FUS.EventLog.Fus;
using JetBrains.Util;

namespace JetBrains.ReSharper.Plugins.Unity.Rider.Integration.Core.Feature.Services.FeatureStatistics
{
    [SolutionComponent]
    public class UnityProjectInformationUsageCollector : SolutionUsagesCollector
    {
        private readonly ISolution mySolution;
        private readonly UnitySolutionTracker myUnitySolutionTracker;
        private readonly FeaturesStartupMonitor myMonitor;
        private readonly ILogger myLogger;
        private readonly IShellLocks myShellLocks;

        private enum UnityProjectKind
        {
            Generated,
            Library,
            Sidecar,
            Other
        }

        private enum Status
        {
            FailToRead,
            NotPresent,
            Present
        }
        
        [Flags]
        public enum EnterPlayModeOptions
        {
            None = 0,
            DisableDomainReload = 1 << 0,
            DisableSceneReload = 1 << 1,
            DisableSceneBackupUnlessDirty = 1 << 2
        }
        
        private EventLogGroup myGroup;
        private readonly EventId1<UnityProjectKind> myProjectKindEvent;
        private readonly EventId2<string, bool> myUnityVersionEvent;
        private readonly EventId3<Status, bool, bool> myEnterPlayModeOptionsEvent;
        private readonly UnityExternalFilesPsiModule myUnityModule;

        public UnityProjectInformationUsageCollector(ISolution solution, UnitySolutionTracker unitySolutionTracker, FeaturesStartupMonitor monitor, FeatureUsageLogger featureUsageLogger, ILogger logger, IShellLocks shellLocks)
        {
            mySolution = solution;
            myUnitySolutionTracker = unitySolutionTracker;
            myMonitor = monitor;
            myLogger = logger;
            myShellLocks = shellLocks;
            myUnityModule = UnityProjectSettingsUtils.GetUnityModule(solution);
            myGroup = new EventLogGroup("dotnet.unity.projects", "Unity Project Information", 2, featureUsageLogger);
            myProjectKindEvent = myGroup.RegisterEvent("projectKind", "Project Kind", 
                EventFields.Enum<UnityProjectKind>("type", "Type"));
            
            myUnityVersionEvent = myGroup.RegisterEvent("version", "Project Unity Version", 
                EventFields.StringValidatedByRegexp("version", "Unity Version", UnityVersion.VersionRegex),
                EventFields.Boolean("isCustom", "Custom Unity Build")); 
            myEnterPlayModeOptionsEvent = myGroup.RegisterEvent("enterPlayModeOptions", "Enter Play Mode Options",
                EventFields.Enum<Status>("exists", "EnterPlayModeOptionsEnabled exists in the project."),
                EventFields.Boolean("enterPlayModeOptionsEnabled", "Enter Play Mode Option"),
                EventFields.Boolean("disableDomainReload", "DisableDomainReload flag"));
        }
        
        public override EventLogGroup GetGroup()
        {
            return myGroup;
        }

        public static (string, bool) GetUnityVersion(string versionInfo)
        {
            const string unknownVersion = "0.0.0f0";
            versionInfo = versionInfo ?? unknownVersion;
            var match = Regex.Match(versionInfo, UnityVersion.VersionRegex);
            if (match.Success)
            {
                var matchedSubstring = match.Value;
                return (matchedSubstring, !matchedSubstring.Equals(versionInfo));
            }
            else
            {
                return (unknownVersion, false);
            }
        }

        public override async Task<ISet<MetricEvent>> GetMetricsAsync(Lifetime lifetime)
        {
            return await lifetime.StartMainReadAsync(async () =>
            {
                // https://github.com/JetBrains/rd/pull/330/files
                // todo: switch to NextTrueValueAsync after the fix lands
                await NextValueAsync2(myMonitor.FullStartupFinished, lifetime, b => b);

                return await mySolution.GetPsiServices().Files.CommitWithRetryBackgroundRead(lifetime, () =>
                {
                    var (verifiedVersion, isCustom) = GetUnityVersion(UnityVersion.GetProjectSettingsUnityVersion(mySolution.SolutionDirectory));
                    
                    var hashSet = new HashSet<MetricEvent>();
                    hashSet.Add(myProjectKindEvent.Metric(GetProjectType()));
                    hashSet.Add(myUnityVersionEvent.Metric(verifiedVersion, isCustom));
                    var (exists, isEnabled, options) = GetEnterPlayModeOptions();
                    hashSet.Add(myEnterPlayModeOptionsEvent.Metric(exists, isEnabled, options));
                    
                    return hashSet;
                });
            });
        }
        
        public static Task<T> NextValueAsync2<T>(ISource<T> source, Lifetime lifetime, Func<T, bool> condition)
        {
            var tcs = new TaskCompletionSource<T>();
            var definition = lifetime.CreateNested();
            definition.SynchronizeWith(tcs);

            source.Advise(definition.Lifetime, v =>
            {
                if (condition(v)) 
                    tcs.TrySetResult(v);
            });
      
            return tcs.Task;
        }

        private (Status exists, bool isEnabled, bool options) GetEnterPlayModeOptions()
        {
            try
            {
                var editorSettings = UnityProjectSettingsUtils.GetEditorSettings(myUnityModule);
                Assertion.Assert(editorSettings != null);
                myShellLocks.AssertReadAccessAllowed();
                var yamlFile = editorSettings.GetDominantPsiFile<YamlLanguage>() as IYamlFile;
                Assertion.Assert(yamlFile != null);
                var node = UnityProjectSettingsUtils.GetValue<INode>(yamlFile, "EditorSettings",
                    "m_EnterPlayModeOptionsEnabled");
                var optionsNode = UnityProjectSettingsUtils.GetValue<INode>(yamlFile, "EditorSettings", "m_EnterPlayModeOptions");
                if (node == null)
                    return (Status.NotPresent, false, false);
                if (optionsNode == null)
                {
                    myLogger.Warn("m_EnterPlayModeOptionsEnabled exists, but m_EnterPlayModeOptions doesn't.");
                    return (Status.FailToRead, false, false);
                }

                var options = Convert.ToInt32(optionsNode.GetText());
                var disableDomainReload = ((EnterPlayModeOptions)options).HasFlag(EnterPlayModeOptions.DisableDomainReload);
                return (Status.Present, Convert.ToBoolean(Convert.ToInt32(node.GetText())), disableDomainReload);
            }
            catch (Exception e)
            { 
                myLogger.Warn(e);
            }
            return (Status.FailToRead, false, false);
        }

        private UnityProjectKind GetProjectType()
        {
            if (myUnitySolutionTracker.IsUnityGeneratedProject.Value)
                return UnityProjectKind.Generated;
            else if (myUnitySolutionTracker.IsUnityProject.Value)
                return UnityProjectKind.Sidecar;
            else if (myUnitySolutionTracker.HasUnityReference.Value)
                return UnityProjectKind.Library;

            return UnityProjectKind.Other;
        }
    }
}