using JetBrains.DocumentManagers;
using JetBrains.Metadata.Reader.API;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules.ExternalFileModules;
using JetBrains.Util;

#nullable enable

namespace JetBrains.ReSharper.Plugins.Unity.Core.Psi.Modules
{
    [SolutionComponent]
    public class UnityExternalPsiSourceFileFactory
    {
        private readonly IProjectFileExtensions myProjectFileExtensions;
        private readonly PsiProjectFileTypeCoordinator myProjectFileTypeCoordinator;
        private readonly DocumentManager myDocumentManager;

        public UnityExternalPsiSourceFileFactory(IProjectFileExtensions projectFileExtensions,
                                                 PsiProjectFileTypeCoordinator projectFileTypeCoordinator,
                                                 DocumentManager documentManager)
        {
            myProjectFileExtensions = projectFileExtensions;
            myProjectFileTypeCoordinator = projectFileTypeCoordinator;
            myDocumentManager = documentManager;
        }

        public IExternalPsiSourceFile CreateExternalPsiSourceFile(UnityExternalFilesPsiModule psiModule,
                                                                  VirtualFileSystemPath path,
                                                                  ProjectFileType projectFileType,
                                                                  IPsiSourceFileProperties properties)
        {
            var file = new UnityExternalPsiSourceFile(path, psiModule, projectFileType,
                file => psiModule.ContainsPath(file.Location), _ => properties,
                myProjectFileExtensions, myProjectFileTypeCoordinator, myDocumentManager,
                UniversalModuleReferenceContext.Instance);
            // Prime the file system cache
            file.GetCachedFileSystemData();
            return file;
        }
    }
}