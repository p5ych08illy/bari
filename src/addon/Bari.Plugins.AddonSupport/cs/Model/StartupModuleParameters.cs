using System.Collections.Generic;
using System.Linq;
using Bari.Core.Model;
using Bari.Core.Model.Parameters;

namespace Bari.Plugins.AddonSupport.Model
{
    /// <summary>
    /// Parameter block defining the startup <see cref="Module"/> or <see cref="Project"/> for a <see cref="Product"/>
    /// </summary>
    public class StartupModuleParameters : IProjectParameters
    {
        public IEnumerable<Project> Projects
        {
            get; private set;
        }

        public StartupModuleParameters(IEnumerable<Project> projects)
        {
            this.Projects = projects;
        }
        
        public StartupModuleParameters()
        {
        }
    }
}