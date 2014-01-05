﻿using System.Collections.Generic;
using System.IO;
using System.Xml;
using Bari.Core.Model;
using Bari.Plugins.VsCore.VisualStudio.ProjectSections;

namespace Bari.Plugins.Csharp.VisualStudio.CsprojSections
{
    /// <summary>
    /// .csproj section listing all the source files
    /// </summary>
    public class SourceItemsSection: SourceItemsSectionBase
    {
        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="suite">Active suite</param>
        public SourceItemsSection(Suite suite) : base(suite)
        {
        }

        protected override IEnumerable<ISourceSet> GetSourceSets(Project project)
        {
            return new[] {project.GetSourceSet("cs")};
        }

        private static readonly ISet<string> ignoredExtensions = new HashSet<string>
            {
                ".csproj",
                ".csproj.user"
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

        /// <summary>
        /// Gets the element name for a given compilation item.
        /// 
        /// <para>The default implementation always returns <c>Compile</c></para>
        /// </summary>
        /// <param name="file">File name from the source set</param>
        /// <returns>Returns a valid XML element name</returns>
        protected override string GetElementNameFor(string file)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".xaml")
                return "Page";
            else
                return base.GetElementNameFor(file);
        }

        protected override void WriteAdditionalOptions(XmlWriter writer, Project project, string projectRelativePath)
        {
            // Extra options for XAML pages
            var ext = Path.GetExtension(projectRelativePath).ToLowerInvariant();
            if (ext == ".xaml")
            {
                writer.WriteElementString("SubType", "Designer");
                writer.WriteElementString("Generator", "MSBuild:Compile");
            }

            // Extra options for XAML page code-behind files
            if (projectRelativePath.ToLowerInvariant().EndsWith(".xaml.cs"))
            {
                writer.WriteElementString("DependentUpon",
                    projectRelativePath.Substring(0, projectRelativePath.Length - 3));
            }

            base.WriteAdditionalOptions(writer, project, projectRelativePath);
        }
    }
}