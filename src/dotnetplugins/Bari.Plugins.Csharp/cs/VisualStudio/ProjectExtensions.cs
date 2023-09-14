using Bari.Core.Model;
using Bari.Plugins.Csharp.Model;
using Bari.Plugins.VsCore.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bari.Plugins.Csharp.VisualStudio
{
    public static class ProjectExtensions
    {
        public static bool IsSDKProject(this Project project)
        {
            var csharpParams = project.GetInheritableParameters<CsharpProjectParameters, CsharpProjectParametersDef>("csharp");
            var frameworkVersion = csharpParams.IsTargetFrameworkVersionSpecified
                    ? csharpParams.TargetFrameworkVersion
                    : FrameworkVersion.v4;

            return frameworkVersion >= FrameworkVersion.v6;
        }
    }
}
