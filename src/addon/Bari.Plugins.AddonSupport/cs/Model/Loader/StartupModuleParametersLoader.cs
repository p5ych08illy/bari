using System;
using System.Collections.Generic;
using System.Linq;
using Bari.Core.Model;
using Bari.Core.Model.Loader;
using Bari.Core.Model.Parameters;
using Bari.Core.UI;
using Ninject.Infrastructure.Language;
using YamlDotNet.RepresentationModel;

namespace Bari.Plugins.AddonSupport.Model.Loader
{
    public class StartupModuleParametersLoader : YamlProjectParametersLoaderBase<StartupModuleParameters>
    {
        public StartupModuleParametersLoader(IUserOutput output) : base(output)
        {
        }

        protected override string BlockName
        {
            get { return "startup"; }
        }

        protected override StartupModuleParameters CreateNewParameters(Suite suite)
        {
            return new StartupModuleParameters();
        }

        public override IProjectParameters Load(Suite suite, string name, YamlNode value, YamlParser parser)
        {
            var startUpNames = new List<string>();
            if (value is YamlScalarNode)
            {
                startUpNames.Add(ParseString(value));
            }
            else if (value is YamlSequenceNode)
            {
                parser.EnumerateNodesOf(value as YamlSequenceNode)
                      .OfType<YamlScalarNode>()
                      .Select(ParseString)
                      .Map(startUpNames.Add);
            }

            var projects = startUpNames.Select(s => GetProject(s, suite)).OfType<Project>();

            return new StartupModuleParameters(projects);
        }

        private Project GetProject(string name, Suite suite)
        {
            if (suite.HasModule(name))
            {
                var module = suite.GetModule(name);
                return module.Projects.FirstOrDefault(
                        prj => prj.Type == ProjectType.Executable || prj.Type == ProjectType.WindowsExecutable);

            }

            return (from module in suite.Modules
                    where module.HasProject(name)
                    select module.GetProject(name)).FirstOrDefault();
        }

        protected override Dictionary<string, Action> GetActions(StartupModuleParameters target, YamlNode value, YamlParser parser)
        {
            return new Dictionary<string, Action>();
        }
    }
}