using System;
using System.IO;
using Bari.Core.Generic;
using Bari.Core.Tools;
using Bari.Core.UI;
using Bari.Plugins.VsCore.Exceptions;

namespace Bari.Plugins.VsCore.Tools
{
    /// <summary>
    /// Default MSBuild implementation, running the MSBuild command line tool in a separate process
    /// </summary>
    public abstract class MSBuild: ManuallyInstallableExternalTool, IMSBuild
    {
        private readonly IParameters parameters;

        /// <summary>
        /// Constructs the MSBuild runner
        /// </summary>
        /// <param name="parameters">User defined parameters for bari</param>
        /// <param name="path">Path to MSBuild.exe</param>
        protected MSBuild(IParameters parameters, string path)
            : base("msbuild", path, 
                   "MSBuild.exe", new Uri("http://www.microsoft.com/en-us/download/details.aspx?id=17851"), false, parameters)
        {
            this.parameters = parameters;
        }

        /// <summary>
        /// Runs MSBuild
        /// </summary>
        /// <param name="root">The root directory which will became MSBuild's root directory</param>
        /// <param name="relativePath">Relative path of the solution file (or MSBuild file) to be processed</param>
        public void Run(IFileSystemDirectory root, string relativePath, bool restore)
        {
            var localRoot = root as LocalFileSystemDirectory;
            if (localRoot == null)
                throw new NotSupportedException("Only local file system is supported for MSBuild!");

            var absPath = Path.Combine(localRoot.AbsolutePath, relativePath);
            var fileName = Path.GetFileName(absPath) ?? String.Empty;

            // Restore must not share an MSBuild invocation with the build. MSBuild evaluates
            // every project once per invocation and caches that evaluation, but restore is what
            // writes obj\*.nuget.g.props / obj\*.nuget.g.targets, which are imported *during*
            // evaluation. In a combined '/t:restore,build' run the build phase can therefore
            // consume a project state from before the restore. MSBuild's own '-restore' switch
            // exists for exactly this reason (it re-evaluates between the two submissions);
            // using two separate processes gives the same guarantee without requiring
            // MSBuild 15.5+, which matters because bari also supports MSBuild 4.0 / VS2013 / VS2015.
            if (restore)
                RunTarget(root, fileName, "restore");

            RunTarget(root, fileName, "build");
        }

        private void RunTarget(IFileSystemDirectory root, string fileName, string target)
        {
            if (!Run(root, fileName, "/m",
                                     "/nologo",
                                     "/verbosity:" + Verbosity,
                                     "/consoleloggerparameters:" + ConsoleLoggerParameters,
                                     "/t:" + target,
                                     "/nr:false"
                    ))
                throw new MSBuildFailedException();
        }

        private string ConsoleLoggerParameters
        {
            get { return "NoSummary"; }
        }

        private string Verbosity
        {
            get { return parameters.VerboseOutput ? "normal" : "minimal"; }
        }
    }
}