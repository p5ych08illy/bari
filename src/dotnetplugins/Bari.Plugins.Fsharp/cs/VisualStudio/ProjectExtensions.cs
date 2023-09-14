using Bari.Core.Model;
using Bari.Plugins.Fsharp.Model;
using Bari.Plugins.VsCore.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bari.Plugins.Fsharp.VisualStudio
{
    public static class ProjectExtensions
    {
        public static bool IsSDKProject(this Project project)
        {
            var fsharpParams = project.GetInheritableParameters<FsharpProjectParameters, FsharpProjectParametersDef>("fsharp");
            var frameworkVersion = fsharpParams.IsTargetFrameworkVersionSpecified
                    ? fsharpParams.TargetFrameworkVersion
                    : FrameworkVersion.v4;

            return frameworkVersion >= FrameworkVersion.v6;
        }
    }
}
