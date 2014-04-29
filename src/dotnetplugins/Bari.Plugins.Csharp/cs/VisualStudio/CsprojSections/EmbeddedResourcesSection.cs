﻿using System.Collections.Generic;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.Csharp.Model;
using Bari.Plugins.VsCore.VisualStudio.ProjectSections;

namespace Bari.Plugins.Csharp.VisualStudio.CsprojSections
{        
    /// <summary>
    /// .csproj section listing all the embedded resources
    /// </summary>
    public class EmbeddedResourcesSection: SourceItemsSectionBase
    {
        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="suite">Active suite</param>
        public EmbeddedResourcesSection(Suite suite)
            : base(suite)
        {
        }

        /// <summary>
        /// Gets the source sets to include 
        /// </summary>
        /// <param name="project">The project to get its source sets</param>
        /// <returns>Returns an enumeration of source sets, all belonging to the given project</returns>
        protected override IEnumerable<ISourceSet> GetSourceSets(Project project)
        {
            return new[] {project.GetSourceSet("resources")};
        }

        /// <summary>
        /// Gets the element name for a given compilation item.
        /// 
        /// <para>The default implementation always returns <c>Compile</c></para>
        /// </summary>
        /// <param name="file">File name from the source set</param>
        /// <returns>Returns a valid XML element name</returns>
        protected override string GetElementNameFor(Project project, string file)
        {
            var relativePath = ToProjectRelativePath(project, file, "resources");

            if (relativePath.StartsWith("wpf\\"))
                return "Resource";
            else
                return "EmbeddedResource";
        }

        protected override string GetLogicalPath(Project project, SuiteRelativePath file, string sourceSetType)
        {
            var path = base.GetLogicalPath(project, file, sourceSetType);
            if (path.StartsWith("wpf\\"))
                return path.Substring(4);
            else
                return PrefixWithRootNamespace(project, path);
        }

        private string PrefixWithRootNamespace(Project project, string path)
        {
            CsharpProjectParameters parameters = project.HasParameters("csharp")
                                                     ? project.GetParameters<CsharpProjectParameters>("csharp")
                                                     : new CsharpProjectParameters(Suite);

            return parameters.RootNamespace + "." + path;
        }


        private static readonly ISet<string> ignoredExtensions = new HashSet<string>
            {
            };

        /// <summary>
        /// Gets a set of filename postfixes to be ignored when generating the source references
        /// </summary>
        protected override ISet<string> IgnoredExtensions
        {
            get { return ignoredExtensions; }
        }

        /// <summary>
        /// Source set name where the project file is placed
        /// </summary>
        protected override string ProjectSourceSetName
        {
            get { return "cs"; }
        }
    }
}