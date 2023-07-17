using Bari.Core.Generic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bari.Plugins.dotnet.Tools
{
    public interface Idotnet
    {
        bool RunTests(IEnumerable<TargetRelativePath> testAssemblies);
    }
}
