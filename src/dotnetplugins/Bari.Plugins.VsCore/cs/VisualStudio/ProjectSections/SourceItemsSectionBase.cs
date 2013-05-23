﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Bari.Core.Model;

namespace Bari.Plugins.VsCore.VisualStudio.ProjectSections
{
    /// <summary>
    /// Base class for MSBuild project file section listing the source files of the project
    /// </summary>
    public abstract class SourceItemsSectionBase: MSBuildProjectSectionBase
    {
        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="suite">Active suite</param>
        protected SourceItemsSectionBase(Suite suite) : base(suite)
        {
        }

        /// <summary>
        /// Writes the section using an XML writer
        /// </summary>
        /// <param name="writer">XML writer to use</param>
        /// <param name="project">The project to generate .csproj for</param>
        /// <param name="context">Current .csproj generation context</param>
        public override void Write(XmlWriter writer, Project project, IMSBuildProjectGeneratorContext context)
        {
            writer.WriteStartElement("ItemGroup");
            foreach (var sourceSet in GetSourceSets(project))
            {
                foreach (var file in sourceSet.Files)
                {
                    var relativePath = ToProjectRelativePath(project, file);

                    // We have to skip .csproj files, which are generated by bari to the source set because otherwise
                    // VisualStudio does not work as expected:
                    if (!IgnoredExtensions.Any(ext => relativePath.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        writer.WriteStartElement("Compile");
                        writer.WriteAttributeString("Include", relativePath);
                        writer.WriteEndElement();
                    }
                }
            }
            writer.WriteEndElement();
        }

        /// <summary>
        /// Gets the source sets to include 
        /// </summary>
        /// <param name="project">The project to get its source sets</param>
        /// <returns>Returns an enumeration of source sets, all belonging to the given project</returns>
        protected abstract IEnumerable<SourceSet> GetSourceSets(Project project);

        /// <summary>
        /// Gets a set of filename postfixes to be ignored when generating the source references
        /// </summary>
        protected abstract ISet<string> IgnoredExtensions { get; }
    }
}