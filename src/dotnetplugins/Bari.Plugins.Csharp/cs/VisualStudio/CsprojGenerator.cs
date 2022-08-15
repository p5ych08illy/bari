using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.VsCore.VisualStudio;
using Bari.Plugins.VsCore.VisualStudio.ProjectSections;

namespace Bari.Plugins.Csharp.VisualStudio
{
    /// <summary>
    /// Class for generating a Visual C# project file from a bari project model
    /// </summary>
    public class CsprojGenerator: IMSBuildProjectGeneratorContext
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
        /// Gets the text writer used to generate version information C# file
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
        /// <param name="sections">Csproj section writers to be used</param>
        public CsprojGenerator(IEnumerable<IMSBuildProjectSection> sections)
        {            
            this.sections = sections;
        }

        /// <summary>
        /// Writes the output
        /// </summary>
        /// <param name="project">The project to generate csproj file for</param>
        /// <param name="references">Paths to the external references to be included in the project</param>
        /// <param name="output">Output where the csproj file will be written</param>
        /// <param name="versionOutput">Output where the version info should be generated</param>
        /// <param name="versionFileName">File name relative to the csproj file for the version info</param>        
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

            writer.WriteStartElement("Project");
            writer.WriteAttributeString("Sdk", "Microsoft.NET.Sdk");

            foreach (var section in sections)
                section.Write(writer, project, this);

            writer.WriteEndElement();
            writer.Flush();
        }                 
    }
}