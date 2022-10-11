﻿// using JetBrains.Collections.Viewable;
// using JetBrains.Lifetimes;
// using JetBrains.ProjectModel;
// using JetBrains.ReSharper.Plugins.Json.Psi;
// using JetBrains.ReSharper.Plugins.Unity.Core.ProjectModel;
// using JetBrains.ReSharper.Plugins.Unity.InputActions.Psi.Caches;
// using JetBrains.ReSharper.Plugins.Unity.UnityEditorIntegration;
// using JetBrains.ReSharper.Psi;
// using JetBrains.ReSharper.Psi.Caches;
// using JetBrains.ReSharper.Psi.Resolve;
// using JetBrains.ReSharper.Psi.Tree;
//
// namespace JetBrains.ReSharper.Plugins.Unity.InputActions.Psi.Resolve
// {
//     // Creates references from the "references" array to the "name" declaration
//     [ReferenceProviderFactory]
//     public class InputActionsNameReferenceProviderFactory : IReferenceProviderFactory
//     {
//         private readonly UnitySolutionTracker myUnitySolutionTracker;
//
//         public InputActionsNameReferenceProviderFactory(Lifetime lifetime, UnitySolutionTracker unitySolutionTracker)
//         {
//             myUnitySolutionTracker = unitySolutionTracker;
//
//             // ReSharper disable once AssignNullToNotNullAttribute
//             Changed = new DataFlow.Signal<IReferenceProviderFactory>(lifetime, GetType().FullName);
//         }
//
//         public IReferenceFactory CreateFactory(IPsiSourceFile sourceFile, IFile file, IWordIndex wordIndexForChecks)
//         {
//             // Make sure the source file is still valid. It's not clear why it would be invalid, but we get it
//             // See DEXP-672670
//             if (!sourceFile.IsValid())
//                 return null;
//
//             // The source file might be in the external files module, so we can't look at what project it belongs to
//             if (!sourceFile.GetSolution().HasUnityReference() && !myUnitySolutionTracker.IsUnityProject.HasTrueValue())
//                 return null;
//
//             if (sourceFile.IsInputActions() && sourceFile.PrimaryPsiLanguage.Is<JsonNewLanguage>())
//                 return new InputActionsNameReferenceFactory(sourceFile);
//
//             return null;
//         }
//
//         public DataFlow.ISignal<IReferenceProviderFactory> Changed { get; }
//     }
// }