using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using Bari.Core.Build;
using Bari.Core.Build.Dependencies;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Core.UI;

namespace Bari.Plugins.VsCore.Build
{
    /// <summary>
    /// Represents dependency on the NuGet restore state of a set of projects.
    ///
    /// <para>The build cache only stores the files below <c>target/[module]</c>. The NuGet restore
    /// outputs of an SDK style project live in <c>target/tmp/[module]/[project]/obj</c>, so they are
    /// neither captured nor restored by it. Without this dependency a cache hit lets bari report a
    /// successful build while <c>project.assets.json</c> is missing, and the failure only surfaces
    /// in a later build as NETSDK1004 (thrown by the SDK's GenerateDepsFile task) - which then
    /// succeeds when repeated, because that run finally ran the restore.</para>
    ///
    /// <para>Making the restore state part of the fingerprint turns such a half restored target
    /// directory into a cache miss, so MSBuild - and with it the restore - is actually executed.</para>
    /// </summary>
    public class NuGetRestoreStateDependencies : DependenciesBase
    {
        private const string AssetsFileName = "project.assets.json";

        private readonly IFileSystemDirectory targetRoot;
        private readonly IList<Project> projects;

        /// <summary>
        /// Constructs the dependency object
        /// </summary>
        /// <param name="targetRoot">The suite's target directory</param>
        /// <param name="projects">The projects whose restore state is relevant</param>
        public NuGetRestoreStateDependencies(IFileSystemDirectory targetRoot, IEnumerable<Project> projects)
        {
            Contract.Requires(targetRoot != null);
            Contract.Requires(projects != null);

            this.targetRoot = targetRoot;
            this.projects = projects.ToList();
        }

        /// <summary>
        /// Gets the target relative paths of the NuGet intermediate directories which exist but
        /// have no assets file in them.
        ///
        /// <para>It is empty both for a healthy target directory and for a completely cleaned one:
        /// only projects which already have an <c>obj</c> directory are expected to have an assets
        /// file, so C++ projects and never built projects never show up here.</para>
        /// </summary>
        public string MissingAssetsFiles
        {
            get
            {
                var result = new StringBuilder();

                foreach (var objDir in projects
                    .Select(GetNuGetIntermediateDirectory)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(dir => dir, StringComparer.OrdinalIgnoreCase))
                {
                    if (targetRoot.Exists(objDir) &&
                        !targetRoot.Exists(Path.Combine(objDir, AssetsFileName)))
                    {
                        result.Append(objDir);
                        result.Append(';');
                    }
                }

                return result.ToString();
            }
        }

        /// <summary>
        /// Gets the target relative path of a project's NuGet intermediate directory, matching the
        /// <c>BaseIntermediateOutputPath</c> written into the generated project files.
        /// </summary>
        private static string GetNuGetIntermediateDirectory(Project project)
        {
            return Path.Combine("tmp", project.Module.Name, project.Name, "obj");
        }

        /// <summary>
        /// Creates fingerprint of the dependencies represented by this object, which can later be compared
        /// to other fingerprints.
        /// </summary>
        /// <returns>Returns the fingerprint of the dependent item's current state.</returns>
        protected override IDependencyFingerprint CreateFingerprint()
        {
            return new ObjectPropertiesFingerprint(this, new[] { "MissingAssetsFiles" });
        }

        /// <summary>
        /// Dumps debug information about this dependency to the output
        /// </summary>
        public override void Dump(IUserOutput output)
        {
            var missing = MissingAssetsFiles;

            output.Message(String.IsNullOrEmpty(missing)
                ? "NuGet restore state is complete"
                : String.Format("NuGet restore state is incomplete, assets file is missing from: {0}", missing));
        }
    }
}
