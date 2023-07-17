using System.Collections.Generic;
using System.Linq;
using Bari.Core.Commands.Test;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.dotnet.Tools;

namespace Bari.Plugins.dotnet.Commands.Test
{
    public class DotnetTestRunner : ITestRunner
    {
        private readonly Idotnet dotnet;

        public DotnetTestRunner(Idotnet dotnet)
        {
            this.dotnet = dotnet;
        }

        public string Name
        {
            get { return "dotnet"; }
        }

        public bool Run(IEnumerable<TestProject> projects, IEnumerable<TargetRelativePath> buildOutputs)
        {
            return dotnet.RunTests(projects.Select(project => new TargetRelativePath(project.Module.Name + ".tests", project.Name + ".dll")));
        }
    }
}