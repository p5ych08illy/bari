using System;
using System.IO;
using System.Xml;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.Fsharp.Model;
using Bari.Plugins.VsCore.VisualStudio;
using Bari.Plugins.VsCore.VisualStudio.ProjectSections;

namespace Bari.Plugins.Fsharp.VisualStudio.FsprojSections
{
    public class PropertiesSection : MSBuildProjectSectionBase
    {
        private readonly IProjectGuidManagement projectGuidManagement;
        private readonly IFileSystemDirectory targetDir;

        public PropertiesSection(Suite suite, IProjectGuidManagement projectGuidManagement, [TargetRoot] IFileSystemDirectory targetDir)
            : base(suite)
        {
            this.projectGuidManagement = projectGuidManagement;
            this.targetDir = targetDir;
        }

        public void WriteOutputPath(XmlWriter writer, Project project)
        {
            // The path must be absolute. Microsoft.Common.props captures
            // $(_InitialBaseIntermediateOutputPath) from the raw value written here, but later hands out
            // $(BaseIntermediateOutputPath) and $(MSBuildProjectExtensionsPath) rooted against the project
            // directory. With a relative value the three describe the same directory through different
            // strings, which makes this guard in Microsoft.WinFX.targets (target GenerateTemporaryTargetAssembly)
            // believe the intermediate directory was redirected away from the NuGet one:
            //
            //     Condition="... and '$(_InitialBaseIntermediateOutputPath)' != '$(BaseIntermediateOutputPath)'"
            //
            // It then adds project.assets.json and project.nuget.cache to the files it copies into the
            // "other" directory and deletes afterwards - except source and destination are the same file
            // here, so the copy is a no-op and the delete destroys the project's own restore state. Every
            // WPF markup compilation pass that needs a temporary target assembly wipes the assets file, and
            // the next build of that project fails with NETSDK1004.
            writer.WriteStartElement("PropertyGroup");
            writer.WriteElementString("BaseIntermediateOutputPath", GetNuGetIntermediateOutputPath(project, "fs"));
            writer.WriteEndElement();
        }

        /// <summary>
        /// Gets the absolute path of the directory NuGet restore writes its outputs to for a project.
        /// </summary>
        private string GetNuGetIntermediateOutputPath(Project project, string sourceSetName)
        {
            var localTargetDir = targetDir as LocalFileSystemDirectory;
            if (localTargetDir == null)
            {
                // Non local target directories cannot be expressed as an absolute path
                return ToProjectRelativePath(project,
                    Path.Combine(Suite.SuiteRoot.GetRelativePath(targetDir),
                                 "tmp", project.Module.Name, project.Name, "obj"),
                    sourceSetName);
            }

            return Path.Combine(localTargetDir.AbsolutePath,
                                "tmp", project.Module.Name, project.Name, "obj")
                   + Path.DirectorySeparatorChar;
        }

        public override void Write(XmlWriter writer, Project project, IMSBuildProjectGeneratorContext context)
        {
            if (!project.IsSDKProject())
            {
                // TODO: merge common code with C# PropertiesSection
                writer.WriteStartElement("PropertyGroup");
                writer.WriteAttributeString("Condition", " '$(Configuration)|$(Platform)' == 'Bari|Bari' ");
                WriteConfigurationSpecificPart(writer, project);
                writer.WriteEndElement();
            }

            writer.WriteStartElement("PropertyGroup");
            WriteConfigurationSpecificPart(writer, project);

            writer.WriteElementString("OutputType", GetOutputType(project.Type));
            writer.WriteElementString("AssemblyName", project.Name);
            writer.WriteElementString("ProjectGuid", projectGuidManagement.GetGuid(project).ToString("B"));


            if (project.IsSDKProject())
            {
                writer.WriteElementString("Configurations", "Bari");
                writer.WriteElementString("Platforms", "Bari");
                writer.WriteElementString("Deterministic", "false");
                writer.WriteElementString("AppendTargetFrameworkToOutputPath", "false");
                writer.WriteElementString("AppendRuntimeIdentifierToOutputPath", "false");
                writer.WriteElementString("ProduceReferenceAssemblyInOutDir", "true");
                writer.WriteElementString("RestoreProjectStyle", "PackageReference");
                writer.WriteElementString("CopyLocalLockFileAssemblies", "true");
                writer.WriteElementString("DisableImplicitFSharpCoreReference", "true");

                writer.WriteElementString("ValidateExecutableReferencesMatchSelfContained", "false");
            }

            FsharpProjectParameters parameters =
                project.GetInheritableParameters<FsharpProjectParameters, FsharpProjectParametersDef>("fsharp");

            parameters.FillProjectSpecificMissingInfo(project);
            parameters.ToFsprojProperties(project, writer);

            writer.WriteEndElement();
        }

        private void WriteConfigurationSpecificPart(XmlWriter writer, Project project)
        {
            writer.WriteElementString("OutputPath", ToProjectRelativePath(project, GetOutputPath(targetDir, project), "fs"));
            var tmpFolder = ToProjectRelativePath(project,
                    Path.Combine(Suite.SuiteRoot.GetRelativePath(targetDir),
                        "tmp",
                        project.Module.Name,
                        project.Name),
                    "fs");

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
    }
}