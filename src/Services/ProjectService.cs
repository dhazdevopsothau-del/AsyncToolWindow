using System;
using System.Collections.Generic;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using VSLangProj;
using Task = System.Threading.Tasks.Task;

namespace AsyncToolWindowSample.Services
{
    /// <summary>
    /// Wraps DTE Project &amp; Solution APIs (Section 5).
    /// Provides solution info, project enumeration, references, build, config.
    /// All public methods must be called on the UI thread.
    /// </summary>
    public sealed class ProjectService
    {
        private readonly AsyncPackage _package;
        private readonly IServiceProvider _serviceProvider;

        public ProjectService(AsyncPackage package)
        {
            _package         = package ?? throw new ArgumentNullException(nameof(package));
            _serviceProvider = package;
        }

        // ------------------------------------------------------------------ //
        //  Internal helpers                                                    //
        // ------------------------------------------------------------------ //

        private DTE2 GetDte()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return _serviceProvider.GetService(typeof(DTE)) as DTE2;
        }

        // ================================================================== //
        //  Solution                                                            //
        // ================================================================== //

        /// <summary>
        /// Returns a snapshot of the current solution's properties,
        /// or <c>null</c> when no solution is open.
        /// </summary>
        public SolutionInfo GetSolutionInfo()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = GetDte();
            var sol = dte?.Solution;
            if (sol == null || !sol.IsOpen) return null;

            SolutionBuild sb = sol.SolutionBuild;
            string activeConfig = null;
            try { activeConfig = sb?.ActiveConfiguration?.Name; } catch { }

            return new SolutionInfo
            {
                FullName        = sol.FullName,
                IsOpen          = sol.IsOpen,
                IsDirty         = sol.IsDirty,
                ProjectCount    = sol.Projects.Count,
                ActiveConfig    = activeConfig,
                LastBuildInfo   = sb?.LastBuildInfo ?? -1
            };
        }

        /// <summary>
        /// Triggers a build of the whole solution.
        /// <paramref name="waitForFinish"/>: block until done (use with caution on UI thread).
        /// </summary>
        public void BuildSolution(bool waitForFinish = false)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetDte()?.Solution?.SolutionBuild?.Build(waitForFinish);
        }

        /// <summary>Cleans the solution.</summary>
        public void CleanSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GetDte()?.Solution?.SolutionBuild?.Clean(false);
        }

        // ================================================================== //
        //  Projects                                                            //
        // ================================================================== //

        /// <summary>Returns info for all top-level projects in the solution.</summary>
        public IReadOnlyList<ProjectInfo> GetAllProjects()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var result = new List<ProjectInfo>();
            var dte    = GetDte();
            if (dte?.Solution == null) return result;

            foreach (Project proj in dte.Solution.Projects)
            {
                result.Add(BuildProjectInfo(proj));
            }

            return result;
        }

        /// <summary>Returns info for the project that owns the active document, or <c>null</c>.</summary>
        public ProjectInfo GetActiveDocumentProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte  = GetDte();
            var proj = dte?.ActiveDocument?.ProjectItem?.ContainingProject;
            return proj == null ? null : BuildProjectInfo(proj);
        }

        private static ProjectInfo BuildProjectInfo(Project proj)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string assemblyName = null;
            string rootNs       = null;
            string outputType   = null;
            string targetFw     = null;
            string outputPath   = null;

            try
            {
                assemblyName = proj.Properties?.Item("AssemblyName")?.Value as string;
                rootNs       = proj.Properties?.Item("RootNamespace")?.Value as string;
                outputType   = proj.Properties?.Item("OutputType")?.Value?.ToString();
                targetFw     = proj.Properties?.Item("TargetFrameworkMoniker")?.Value as string;
            }
            catch { /* some project types don't expose these */ }

            try
            {
                outputPath = proj.ConfigurationManager?.ActiveConfiguration?
                                 .Properties?.Item("OutputPath")?.Value as string;
            }
            catch { }

            return new ProjectInfo
            {
                Name         = proj.Name,
                UniqueName   = proj.UniqueName,
                FullName     = proj.FullName,
                Kind         = proj.Kind,
                AssemblyName = assemblyName,
                RootNamespace= rootNs,
                OutputType   = outputType,
                TargetFw     = targetFw,
                OutputPath   = outputPath
            };
        }

        // ================================================================== //
        //  References (VSProject)                                              //
        // ================================================================== //

        /// <summary>
        /// Returns the reference list of the project that owns the active document.
        /// Returns an empty list when no project is found or project is not C#/VB.
        /// </summary>
        public IReadOnlyList<ReferenceInfo> GetReferencesOfActiveProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var result = new List<ReferenceInfo>();

            var dte  = GetDte();
            var proj = dte?.ActiveDocument?.ProjectItem?.ContainingProject;
            if (proj == null) return result;

            VSProject vsProj = proj.Object as VSProject;
            if (vsProj == null) return result;

            foreach (Reference r in vsProj.References)
            {
                result.Add(new ReferenceInfo
                {
                    Name    = r.Name,
                    Path    = r.Path,
                    Version = $"{r.MajorVersion}.{r.MinorVersion}.{r.BuildNumber}.{r.RevisionNumber}",
                    Type    = r.Type.ToString()
                });
            }

            return result;
        }

        // ================================================================== //
        //  ProjectItems (file tree)                                            //
        // ================================================================== //

        /// <summary>
        /// Recursively enumerates all <see cref="ProjectItemInfo"/> in the project
        /// that owns the active document.
        /// </summary>
        public IReadOnlyList<ProjectItemInfo> GetProjectItemsOfActiveProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var result = new List<ProjectItemInfo>();
            var dte    = GetDte();
            var proj   = dte?.ActiveDocument?.ProjectItem?.ContainingProject;
            if (proj == null) return result;

            CollectItems(proj.ProjectItems, result, 0);
            return result;
        }

        private static void CollectItems(ProjectItems items, List<ProjectItemInfo> result, int depth)
        {
            if (items == null) return;
            ThreadHelper.ThrowIfNotOnUIThread();

            foreach (ProjectItem item in items)
            {
                string path        = null;
                string buildAction = null;
                bool   isFolder    = false;

                try { path = item.FileNames[1]; } catch { }
                try { buildAction = item.Properties?.Item("BuildAction")?.Value?.ToString(); } catch { }
                try { isFolder = item.Kind == Constants.vsProjectItemKindPhysicalFolder; } catch { }

                result.Add(new ProjectItemInfo
                {
                    Name        = item.Name,
                    Path        = path,
                    BuildAction = buildAction,
                    IsFolder    = isFolder,
                    Depth       = depth
                });

                CollectItems(item.ProjectItems, result, depth + 1);
            }
        }

        // ================================================================== //
        //  ConfigurationManager                                                //
        // ================================================================== //

        /// <summary>Returns the active build configuration of the active-document project.</summary>
        public BuildConfigInfo GetActiveBuildConfig()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte  = GetDte();
            var proj = dte?.ActiveDocument?.ProjectItem?.ContainingProject;
            if (proj == null) return null;

            Configuration cfg = null;
            try { cfg = proj.ConfigurationManager?.ActiveConfiguration; } catch { }
            if (cfg == null) return null;

            bool   isBuildable = false;
            string outputPath  = null;
            bool   optimize    = false;
            string defines     = null;

            try { isBuildable = cfg.IsBuildable; }                                      catch { }
            try { outputPath  = cfg.Properties?.Item("OutputPath")?.Value as string; }  catch { }
            try { optimize    = (bool)(cfg.Properties?.Item("Optimize")?.Value ?? false); } catch { }
            try { defines     = cfg.Properties?.Item("DefineConstants")?.Value as string; } catch { }

            return new BuildConfigInfo
            {
                ConfigName  = cfg.ConfigurationName,
                Platform    = cfg.PlatformName,
                IsBuildable = isBuildable,
                OutputPath  = outputPath,
                Optimize    = optimize,
                Defines     = defines
            };
        }
    }

    // ====================================================================== //
    //  DTOs                                                                   //
    // ====================================================================== //

    public sealed class SolutionInfo
    {
        public string FullName      { get; set; }
        public bool   IsOpen        { get; set; }
        public bool   IsDirty       { get; set; }
        public int    ProjectCount  { get; set; }
        public string ActiveConfig  { get; set; }
        public int    LastBuildInfo { get; set; }
    }

    public sealed class ProjectInfo
    {
        public string Name          { get; set; }
        public string UniqueName    { get; set; }
        public string FullName      { get; set; }
        public string Kind          { get; set; }
        public string AssemblyName  { get; set; }
        public string RootNamespace { get; set; }
        public string OutputType    { get; set; }
        public string TargetFw      { get; set; }
        public string OutputPath    { get; set; }
    }

    public sealed class ReferenceInfo
    {
        public string Name    { get; set; }
        public string Path    { get; set; }
        public string Version { get; set; }
        public string Type    { get; set; }
    }

    public sealed class ProjectItemInfo
    {
        public string Name        { get; set; }
        public string Path        { get; set; }
        public string BuildAction { get; set; }
        public bool   IsFolder    { get; set; }
        public int    Depth       { get; set; }
    }

    public sealed class BuildConfigInfo
    {
        public string ConfigName  { get; set; }
        public string Platform    { get; set; }
        public bool   IsBuildable { get; set; }
        public string OutputPath  { get; set; }
        public bool   Optimize    { get; set; }
        public string Defines     { get; set; }
    }
}
