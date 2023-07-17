using Bari.Core.Generic;
using Bari.Core.Tools;
using Bari.Core.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bari.Plugins.dotnet.Tools
{
    public class Dotnet : ExternalTool, Idotnet
    {
        private readonly IFileSystemDirectory targetDir;

        public Dotnet([TargetRoot] IFileSystemDirectory targetDir, IParameters parameters) : base("dotnet", parameters)
        {
            this.targetDir = targetDir;
        }

        protected override string ToolPath
        {
            get { return "dotnet"; }
        }

        protected override bool IsDotNETProcess
        {
            get { return false; }
        }

        public bool RunTests(IEnumerable<TargetRelativePath> testAssemblies)
        {
            List<string> ps = new List<string>() { "test" };
            ps.AddRange(testAssemblies.Select(p => (string)p).ToList());
            return Run(targetDir, ps.ToArray());
        }

        protected override void EnsureToolAvailable()
        {

        }
    }
}
