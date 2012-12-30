﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bari.Core.Build;
using Bari.Core.Build.Dependencies;
using Bari.Core.Exceptions;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.Csharp.VisualStudio;
using Ninject.Extensions.ChildKernel;
using Ninject.Parameters;
using Ninject.Syntax;

namespace Bari.Plugins.Csharp.Build
{
    /// <summary>
    /// Builder generating a Visual C# project file from a source code set
    /// 
    /// <para>Uses the <see cref="CsprojGenerator"/> class internally.</para>
    /// </summary>
    public class CsprojBuilder : IBuilder, IEquatable<CsprojBuilder>
    {
        private readonly IResolutionRoot root;
        private readonly Project project;
        private readonly Suite suite;
        private readonly IFileSystemDirectory targetDir;
        private readonly CsprojGenerator generator;
        private ISet<IBuilder> referenceBuilders;

        /// <summary>
        /// Creates the builder
        /// </summary>
        /// <param name="root">Path to resolve instances</param>
        /// <param name="project">The project for which the csproj file will be generated</param>
        /// <param name="suite">The suite the project belongs to </param>
        /// <param name="targetDir">The build target directory </param>
        /// <param name="generator">The csproj generator class to be used</param>
        public CsprojBuilder(IResolutionRoot root, Project project,
                             Suite suite, [TargetRoot] IFileSystemDirectory targetDir, CsprojGenerator generator)
        {
            this.root = root;
            this.project = project;
            this.suite = suite;
            this.targetDir = targetDir;
            this.generator = generator;
        }

        /// <summary>
        /// Dependencies required for running this builder
        /// </summary>
        public IDependencies Dependencies
        {
            get
            {
                var deps = new List<IDependencies>();

                if (project.HasNonEmptySourceSet("cs"))
                    deps.Add(new SourceSetDependencies(root, project.GetSourceSet("cs")));
                if (project.HasNonEmptySourceSet("appconfig"))
                    deps.Add(new SourceSetDependencies(root, project.GetSourceSet("appconfig")));
                if (project.HasNonEmptySourceSet("manifest"))
                    deps.Add(new SourceSetDependencies(root, project.GetSourceSet("manifest")));

                deps.Add(new ProjectPropertiesDependencies(project, "Name", "Type"));

                if (referenceBuilders != null)
                    deps.AddRange(referenceBuilders.OfType<IReferenceBuilder>().Select(CreateReferenceDependency));

                if (deps.Count == 0)
                    return new NoDependencies();
                else if (deps.Count == 1)
                    return deps.First();
                else
                    return new MultipleDependencies(deps);                    
            }
        }

        private IDependencies CreateReferenceDependency(IReferenceBuilder refBuilder)
        {
            return new MultipleDependencies(
                new SubtaskDependency(refBuilder),
                new ReferenceDependency(refBuilder.Reference));
        }

        /// <summary>
        /// Gets an unique identifier which can be used to identify cached results
        /// </summary>
        public string Uid
        {
            get { return project.Module.Name + "." + project.Name; }
        }

        /// <summary>
        /// Prepares a builder to be ran in a given build context.
        /// 
        /// <para>This is the place where a builder can add additional dependencies.</para>
        /// </summary>
        /// <param name="context">The current build context</param>
        public void AddToContext(IBuildContext context)
        {
            referenceBuilders = new HashSet<IBuilder>(project.References.Select(CreateReferenceBuilder));

            var childKernel = new ChildKernel(root);
            childKernel.Bind<Project>().ToConstant(project);

            foreach (var refBuilder in referenceBuilders)
                refBuilder.AddToContext(context);

            context.AddBuilder(this, referenceBuilders);
        }


        private IBuilder CreateReferenceBuilder(Reference reference)
        {
            var builder = root.GetReferenceBuilder<IReferenceBuilder>(reference, new ConstructorArgument("project", project, shouldInherit: true));
            if (builder != null)
            {
                return builder;
            }
            else
                throw new InvalidReferenceTypeException(reference.Uri.Scheme);
        }

        /// <summary>
        /// Runs this builder
        /// </summary>
        /// <param name="context"> </param>
        /// <returns>Returns a set of generated files, in suite relative paths</returns>
        public ISet<TargetRelativePath> Run(IBuildContext context)
        {
            var csprojPath = project.Name + ".csproj";
            const string csversionPath = "version.cs";

            using (var csproj = project.RootDirectory.CreateTextFile(csprojPath))
            using (var csversion = project.RootDirectory.CreateTextFile(csversionPath))
            {
                var references = new HashSet<TargetRelativePath>();
                foreach (var refBuilder in context.GetDependencies(this))
                {
                    var builderResults = context.GetResults(refBuilder);
                    references.UnionWith(builderResults);
                }

                generator.Generate(project, references, csproj, csversion, csversionPath);
            }

            return new HashSet<TargetRelativePath>(
                new[]
                    {
                        new TargetRelativePath(
                            suite.SuiteRoot.GetRelativePathFrom(targetDir, 
                                Path.Combine(suite.SuiteRoot.GetRelativePath(project.RootDirectory), csprojPath))),
                        new TargetRelativePath(
                            suite.SuiteRoot.GetRelativePathFrom(targetDir, 
                                Path.Combine(suite.SuiteRoot.GetRelativePath(project.RootDirectory), csversionPath)))
                    });
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return string.Format("[{0}.{1}.csproj]", project.Module.Name, project.Name);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(CsprojBuilder other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(project, other.project);
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CsprojBuilder)obj);
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            return (project != null ? project.GetHashCode() : 0);
        }

        /// <summary>
        /// Equality operator
        /// </summary>
        public static bool operator ==(CsprojBuilder left, CsprojBuilder right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Inequality operator
        /// </summary>
        public static bool operator !=(CsprojBuilder left, CsprojBuilder right)
        {
            return !Equals(left, right);
        }
    }
}