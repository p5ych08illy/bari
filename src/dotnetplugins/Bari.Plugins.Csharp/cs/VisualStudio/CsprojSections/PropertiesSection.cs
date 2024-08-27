using System;
using System.IO;
using System.Linq;
using System.Xml;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.Csharp.Exceptions;
using Bari.Plugins.Csharp.Model;
using Bari.Plugins.VsCore.Exceptions;
using Bari.Plugins.VsCore.VisualStudio;
using Bari.Plugins.VsCore.VisualStudio.ProjectSections;

namespace Bari.Plugins.Csharp.VisualStudio.CsprojSections
{
    /// <summary>
    /// .csproj section for generic project properties
    /// </summary>
    public class PropertiesSection : MSBuildProjectSectionBase
    {
        private readonly IProjectGuidManagement projectGuidManagement;
        private readonly IFileSystemDirectory targetDir;

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="suite">Active suite</param>
        /// <param name="projectGuidManagement">Project GUID management service</param>
        /// <param name="targetDir">Target directory where the compiled files will be placed</param>
        public PropertiesSection(Suite suite, IProjectGuidManagement projectGuidManagement, [TargetRoot] IFileSystemDirectory targetDir)
            : base(suite)
        {
            this.projectGuidManagement = projectGuidManagement;
            this.targetDir = targetDir;
        }

        public void WriteOutputPath(XmlWriter writer, Project project)
        {
            var tmpFolder = ToProjectRelativePath(project,
                  Path.Combine(Suite.SuiteRoot.GetRelativePath(targetDir),
                              "tmp",
                              project.Module.Name,
                              project.Name,
                              "obj"),
                              "cs");
            writer.WriteStartElement("PropertyGroup");
            writer.WriteElementString("BaseIntermediateOutputPath", tmpFolder);
            writer.WriteEndElement();
        }

        /// <summary>
        /// Writes the section using an XML writer
        /// </summary>
        /// <param name="writer">XML writer to use</param>
        /// <param name="project">The project to generate .csproj for</param>
        /// <param name="context">Current .csproj generation context</param>
        public override void Write(XmlWriter writer, Project project, IMSBuildProjectGeneratorContext context)
        {
            if (!project.IsSDKProject())
            {
                writer.WriteStartElement("PropertyGroup");
                writer.WriteAttributeString("Condition", " '$(Configuration)|$(Platform)' == 'Bari|Bari' ");
                WriteConfigurationSpecificPart(writer, project);
                writer.WriteEndElement();
            }

            writer.WriteStartElement("PropertyGroup");

            // Writing out configuration specific part to the non conditional block as well
            WriteConfigurationSpecificPart(writer, project);

            writer.WriteElementString("OutputType", GetOutputType(project.Type));
            writer.WriteElementString("AssemblyName", project.Name);
            writer.WriteElementString("ProjectGuid", projectGuidManagement.GetGuid(project).ToString("B"));

            CsharpProjectParameters parameters = project.GetInheritableParameters<CsharpProjectParameters, CsharpProjectParametersDef>("csharp");

            if (project.IsSDKProject())
            {
                writer.WriteElementString("Configurations", "Bari");
                writer.WriteElementString("Platforms", "Bari");

                if (project is TestProject)
                    writer.WriteElementString("IsTestProject", "true");
                writer.WriteElementString("AppendTargetFrameworkToOutputPath", "false");
                writer.WriteElementString("AppendRuntimeIdentifierToOutputPath", "false");
                writer.WriteElementString("EnableDefaultApplicationDefinition", "false");
                writer.WriteElementString("RestoreProjectStyle", "PackageReference");
                writer.WriteElementString("CopyLocalLockFileAssemblies", "true");
                writer.WriteElementString("NoDefaultLaunchSettingsFile", "true");
                writer.WriteElementString("UseIJWHost", "true");


                if ((parameters.IsUseWinFormsSpecified && parameters.UseWinForms) || (parameters.IsUseWPFSpecified && parameters.UseWPF))
                    writer.WriteElementString("RuntimeIdentifier", "win-" + (Suite.ActiveGoal.Has("x64") ? "x64" : "x86"));
                else if (parameters.IsTargetOSSpecified)
                {
                    var identifier = parameters.TargetOS.ToLower();
                    writer.WriteElementString("RuntimeIdentifier", (identifier.StartsWith("win") ? "win" : identifier) + "-" + (Suite.ActiveGoal.Has("x64") ? "x64" : "x86"));
                }

                writer.WriteElementString("ValidateExecutableReferencesMatchSelfContained", "false");
            }

            parameters.FillProjectSpecificMissingInfo(project);
            parameters.ToCsprojProperties(project, writer);

            WriteAppConfig(writer, project);
            if (!WriteWin32Resource(writer, project))
            {
                WriteManifest(writer, project);
                WriteApplicationIcon(writer, project, parameters);
            }

            writer.WriteEndElement();


            // Proto
            if (parameters.IsProtoFileSpecified && parameters.IsGrpcServicesSpecified)
            {
                writer.WriteStartElement("ItemGroup");
                writer.WriteStartElement("Protobuf");
                writer.WriteAttributeString("Include", parameters.ProtoFile);
                writer.WriteAttributeString("GrpcServices", parameters.GrpcServices);
                writer.WriteElementString("Link", "Protos\\" + Path.GetFileName(parameters.ProtoFile));
                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            if (parameters.IsLinksSpecified && parameters.Links.Any())
            {
                writer.WriteStartElement("ItemGroup");

                foreach (var link in parameters.Links)
                {
                    writer.WriteStartElement("Compile");
                    writer.WriteAttributeString("Include", link.Item1);
                    writer.WriteAttributeString("Link", link.Item2);
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();//ItemGroup
            }
        }

        private void WriteConfigurationSpecificPart(XmlWriter writer, Project project)
        {
            writer.WriteElementString("OutputPath",
                ToProjectRelativePath(project, GetOutputPath(targetDir, project), "cs"));
            var tmpFolder = ToProjectRelativePath(project,
                  Path.Combine(Suite.SuiteRoot.GetRelativePath(targetDir),
                              "tmp",
                              project.Module.Name,
                              project.Name),
                              "cs");

            writer.WriteElementString("IntermediateOutputPath", tmpFolder);
        }

        private string GetOutputType(ProjectType type)
        {
            switch (type)
            {
                case ProjectType.Executable:
                    return "Exe";
                case ProjectType.WindowsExecutable:
                    return "WinExe";
                case ProjectType.Library:
                    return "Library";
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        private void WriteAppConfig(XmlWriter writer, Project project)
        {
            // Must be called within an open PropertyGroup 

            if (project.HasNonEmptySourceSet("appconfig"))
            {
                var sourceSet = project.GetSourceSet("appconfig");
                var configs = sourceSet.Files.ToList();

                if (configs.Count > 1)
                    throw new TooManyAppConfigsException(project);

                var appConfigPath = configs.FirstOrDefault();
                if (appConfigPath != null)
                {
                    writer.WriteElementString("AppConfig", ToProjectRelativePath(project, appConfigPath, "cs"));
                }
            }
        }

        private void WriteManifest(XmlWriter writer, Project project)
        {
            // Must be called within an open PropertyGroup

            if (project.HasNonEmptySourceSet("manifest"))
            {
                var sourceSet = project.GetSourceSet("manifest");
                var manifests = sourceSet.Files.ToList();

                if (manifests.Count > 1)
                    throw new TooManyManifestsException(project);

                var manifestPath = manifests.FirstOrDefault();
                if (manifestPath != null)
                {
                    writer.WriteElementString("ApplicationManifest", ToProjectRelativePath(project, manifestPath, "cs"));
                }
            }
        }

        private void WriteApplicationIcon(XmlWriter writer, Project project, CsharpProjectParameters parameters)
        {
            // Must be called within an open PropertyGroup

            if (project.Type == ProjectType.Executable ||
                project.Type == ProjectType.WindowsExecutable)
            {
                if (parameters.IsApplicationIconSpecified && !String.IsNullOrWhiteSpace(parameters.ApplicationIcon))
                {
                    string iconPath = Path.Combine(project.RelativeRootDirectory, "resources", parameters.ApplicationIcon);
                    writer.WriteElementString("ApplicationIcon", ToProjectRelativePath(project, iconPath, "cs"));
                }
            }
        }

        private bool WriteWin32Resource(XmlWriter writer, Project project)
        {
            // Must be called within an open PropertyGroup

            var resources = project.GetSourceSet("resources");

            foreach (var suiteRelativePath in resources.Files)
            {
                var resrelativePath = ToProjectRelativePath(project, suiteRelativePath, "resources");
                if (resrelativePath.StartsWith("win32" + Path.DirectorySeparatorChar))
                {
                    writer.WriteElementString("Win32Resource", ToProjectRelativePath(project, suiteRelativePath, "cs"));
                    return true;
                }
            }
            return false;
        }
    }
}
