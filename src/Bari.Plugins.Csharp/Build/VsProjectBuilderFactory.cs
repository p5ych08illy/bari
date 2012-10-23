﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bari.Core.Build;
using Bari.Core.Generic;
using Bari.Core.Model;
using Ninject;
using Ninject.Parameters;
using Ninject.Syntax;

namespace Bari.Plugins.Csharp.Build
{
    /// <summary>
    /// The <see cref="IProjectBuilderFactory"/> implementation supporting C# projects compiled using MSBuild
    /// </summary>
    public class VsProjectBuilderFactory: IProjectBuilderFactory
    {
        private readonly IResolutionRoot root;

        /// <summary>
        /// Constructs the project builder factory
        /// </summary>
        /// <param name="root">Interface for creating new instances</param>
        public VsProjectBuilderFactory(IResolutionRoot root)
        {
            this.root = root;
        }

        /// <summary>
        /// Adds the builders (<see cref="IBuilder"/>) to the given build context which process
        /// the given set of projects (<see cref="Project"/>)
        /// </summary>
        /// <param name="context">Current build context</param>
        /// <param name="projects">Projects to be built</param>
        public void AddToContext(IBuildContext context, IEnumerable<Project> projects)
        {
            var prjs = projects.ToList();
            var slnFactory = root.Get<SlnBuilderFactory>();
            var slnBuilder = slnFactory.AddToContext(context, prjs);
            var msbuild = root.GetBuilder<MSBuildRunner>(
                new ConstructorArgument("slnBuilder", slnBuilder),
                new ConstructorArgument("slnPath", new TargetRelativePath("generated.sln")), // TODO: this should not be hard-coded here 
                new ConstructorArgument("outputs", prjs.Select(GetExpectedOutput))); 

            context.AddBuilder(msbuild, new[] { slnBuilder });
        }

        private TargetRelativePath GetExpectedOutput(Project project)
        {
            var module = project.Module;
            return new TargetRelativePath(
                Path.Combine(module.Name, project.Name) +
                (project.Type == ProjectType.Executable
                     ? ".exe"
                     : ".dll"));
        }
    }
}