﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.Fsharp.Model;
using Bari.Plugins.Fsharp.VisualStudio.FsprojSections;
using Bari.Plugins.VsCore.Model;
using Bari.Plugins.VsCore.VisualStudio;
using Bari.Plugins.VsCore.VisualStudio.ProjectSections;

namespace Bari.Plugins.Fsharp.VisualStudio
{
    public class FsprojGenerator : IMSBuildProjectGeneratorContext
    {
        private readonly IEnumerable<IMSBuildProjectSection> sections;

        private TextWriter versionOutput;
        private string versionFileName;
        private ISet<TargetRelativePath> references;

        /// <summary>
        /// Gets the set of references for the given project
        /// </summary>
        public ISet<TargetRelativePath> References
        {
            get { return references; }
        }

        /// <summary>
        /// Gets the text writer used to generate version information F# file
        /// </summary>
        public TextWriter VersionOutput
        {
            get { return versionOutput; }
        }

        /// <summary>
        /// Gets the name of the file the <see cref="IMSBuildProjectGeneratorContext.VersionOutput"/> writer generates
        /// </summary>
        public string VersionFileName
        {
            get { return versionFileName; }
        }

        /// <summary>
        /// Initializes the project file generator
        /// </summary>
        /// <param name="sections">Fsproj section writers to be used</param>
        public FsprojGenerator(IEnumerable<IMSBuildProjectSection> sections)
        {            
            this.sections = sections;
        }

        /// <summary>
        /// Writes the output
        /// </summary>
        /// <param name="project">The project to generate fsproj file for</param>
        /// <param name="references">Paths to the external references to be included in the project</param>
        /// <param name="output">Output where the fsproj file will be written</param>
        /// <param name="versionOutput">Output where the version info should be generated</param>
        /// <param name="versionFileName">File name relative to the fsproj file for the version info</param>        
        public void Generate(Project project, IEnumerable<TargetRelativePath> references, TextWriter output, TextWriter versionOutput, string versionFileName)
        {
            this.versionOutput = versionOutput;
            this.versionFileName = versionFileName;
            this.references = new HashSet<TargetRelativePath>(references);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineOnAttributes = true,
                NamespaceHandling = NamespaceHandling.Default,
                Encoding = Encoding.UTF8
            };
            var writer = XmlWriter.Create(output, settings);

            if (project.IsSDKProject())
            {
                writer.WriteStartElement("Project");
                var propSection = sections.OfType<PropertiesSection>().FirstOrDefault();
                if (propSection != null)
                {
                    propSection.WriteOutputPath(writer, project);
                }

                writer.WriteStartElement("Import");
                writer.WriteAttributeString("Project", "Sdk.props");
                writer.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");
                writer.WriteEndElement();
            }
            else
            {
                writer.WriteStartElement("Project", "http://schemas.microsoft.com/developer/msbuild/2003");
                writer.WriteAttributeString("ToolsVersion", "4.0");
                writer.WriteAttributeString("DefaultTargets", "Build");

                writer.WriteStartElement("Import");
                writer.WriteAttributeString("Project", @"$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props");
                writer.WriteAttributeString("Condition", @"Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')");
                writer.WriteEndElement();
            }

            foreach (var section in sections)
                section.Write(writer, project, this);

            if (!project.IsSDKProject())
            {
                writer.WriteStartElement("Choose");
                writer.WriteStartElement("When");
                writer.WriteAttributeString("Condition", "'$(VisualStudioVersion)' == '11.0'");
                writer.WriteStartElement("PropertyGroup");
                writer.WriteElementString("FSharpTargetsPath", "$(MSBuildExtensionsPath32)\\..\\Microsoft SDKs\\F#\\3.0\\Framework\\v4.0\\Microsoft.FSharp.Targets");
                writer.WriteEndElement(); // PropertyGroup
                writer.WriteEndElement(); // When
                writer.WriteStartElement("Otherwise");
                writer.WriteStartElement("PropertyGroup");
                writer.WriteElementString("FSharpTargetsPath", "$(MSBuildExtensionsPath32)\\Microsoft\\VisualStudio\\v$(VisualStudioVersion)\\FSharp\\Microsoft.FSharp.Targets");
                writer.WriteEndElement(); // PropertyGroup
                writer.WriteEndElement(); // Otherwise
                writer.WriteEndElement(); // Choose

                writer.WriteStartElement("Import");
                writer.WriteAttributeString("Project", @"$(FSharpTargetsPath)");
                writer.WriteAttributeString("Condition", @"Exists('$(FSharpTargetsPath)')");
                writer.WriteEndElement();
            }
            else
            {
                writer.WriteStartElement("Import");
                writer.WriteAttributeString("Project", "Sdk.targets");
                writer.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.Flush();
        }                 
    }
}