﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bari.Core.Build;
using Bari.Core.Build.Dependencies;
using Bari.Core.Exceptions;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Plugins.Csharp.Build.Dependencies;
using Bari.Plugins.Csharp.VisualStudio;
using Bari.Plugins.VsCore.Build;
using YamlDotNet.Core;

namespace Bari.Plugins.Csharp.Build
{
    /// <summary>
    /// Builder generating a Visual C# project file from a source code set
    /// 
    /// <para>Uses the <see cref="CsprojGenerator"/> class internally.</para>
    /// </summary>
    public class CsprojBuilder : BuilderBase<CsprojBuilder>, ISlnProjectBuilder, IEquatable<CsprojBuilder>
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(CsprojBuilder));

        private readonly IReferenceBuilderFactory referenceBuilderFactory;
        private readonly ISourceSetDependencyFactory sourceSetDependencyFactory;
        private IDependencies dependencies;
        private IDependencies fullSourceDependencies;

        private readonly Project project;
        private readonly Suite suite;
        private readonly IFileSystemDirectory targetDir;
        private readonly CsprojGenerator generator;
        private IList<IReferenceBuilder> referenceBuilders;

        /// <summary>
        /// Gets the project this builder is working on
        /// </summary>
        public Project Project
        {
            get { return project; }
        }

        /// <summary>
        /// Creates the builder
        /// </summary>
        /// <param name="referenceBuilderFactory">Interface to create new reference builder instances</param>
        /// <param name="sourceSetDependencyFactory">Interface to create new source set dependencies</param>
        /// <param name="project">The project for which the csproj file will be generated</param>
        /// <param name="suite">The suite the project belongs to </param>
        /// <param name="targetDir">The build target directory </param>
        /// <param name="generator">The csproj generator class to be used</param>
        public CsprojBuilder(IReferenceBuilderFactory referenceBuilderFactory, ISourceSetDependencyFactory sourceSetDependencyFactory,
                             Project project, Suite suite, [TargetRoot] IFileSystemDirectory targetDir, CsprojGenerator generator)
        {
            this.referenceBuilderFactory = referenceBuilderFactory;
            this.sourceSetDependencyFactory = sourceSetDependencyFactory;
            this.project = project;
            this.suite = suite;
            this.targetDir = targetDir;
            this.generator = generator;
        }

        /// <summary>
        /// Dependencies required for running this builder
        /// </summary>
        public override IDependencies Dependencies
        {
            get
            {
                if (dependencies == null)
                    dependencies = CreateDependencies();
                return dependencies;
            }
        }

        private IDependencies CreateDependencies()
        {
            var deps = new List<IDependencies>();

            if (project.HasNonEmptySourceSet("cs"))
                deps.Add(sourceSetDependencyFactory.CreateSourceSetStructureDependency(project.GetSourceSet("cs"),
                    fn => fn.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase) ||
                          fn.EndsWith(".csproj.user", StringComparison.InvariantCultureIgnoreCase)));
            if (project.HasNonEmptySourceSet("appconfig"))
                deps.Add(sourceSetDependencyFactory.CreateSourceSetStructureDependency(project.GetSourceSet("appconfig"), null));
            if (project.HasNonEmptySourceSet("manifest"))
                deps.Add(sourceSetDependencyFactory.CreateSourceSetStructureDependency(project.GetSourceSet("manifest"), null));
            if (project.HasNonEmptySourceSet("resources"))
                deps.Add(sourceSetDependencyFactory.CreateSourceSetStructureDependency(project.GetSourceSet("resources"), null));

            deps.Add(new ProjectPropertiesDependencies(project, "Name", "Type", "EffectiveVersion"));

            WPFParametersDependencies.Add(project, deps);
            CsharpParametersDependencies.Add(project, deps);

            if (referenceBuilders != null)
            {
                deps.AddRange(
                    referenceBuilders.Select(
                        (refBuilder, idx) => CreateReferenceDependency(refBuilder, referenceBuilders[idx])));
            }

            return MultipleDependenciesHelper.CreateMultipleDependencies(new HashSet<IDependencies>(deps));
        }

        /// <summary>
        /// Gets the builder's full source code dependencies
        /// </summary>
        public IDependencies FullSourceDependencies
        {
            get
            {
                if (fullSourceDependencies == null)
                    fullSourceDependencies = CreateFullSourceDependencies();
                return fullSourceDependencies;
            }
        }

        private IDependencies CreateFullSourceDependencies()
        {
            var deps = new List<IDependencies>();

            if (project.HasNonEmptySourceSet("cs"))
                deps.Add(sourceSetDependencyFactory.CreateSourceSetDependencies(project.GetSourceSet("cs"),
                    fn => fn.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase) ||
                          fn.EndsWith(".csproj.user", StringComparison.InvariantCultureIgnoreCase)));
            if (project.HasNonEmptySourceSet("appconfig"))
                deps.Add(sourceSetDependencyFactory.CreateSourceSetDependencies(project.GetSourceSet("appconfig"), null));
            if (project.HasNonEmptySourceSet("manifest"))
                deps.Add(sourceSetDependencyFactory.CreateSourceSetDependencies(project.GetSourceSet("manifest"), null));
            if (project.HasNonEmptySourceSet("resources"))
                deps.Add(sourceSetDependencyFactory.CreateSourceSetDependencies(project.GetSourceSet("resources"), null));

            return MultipleDependenciesHelper.CreateMultipleDependencies(new HashSet<IDependencies>(deps));
        }

        private IDependencies CreateReferenceDependency(IReferenceBuilder refBuilder, IBuilder effectiveRefBuilder)
        {
            return new MultipleDependencies(
                new SubtaskDependency(refBuilder),
                new ReferenceDependency(refBuilder.Reference),
                new BuilderUidDependency(effectiveRefBuilder));
        }

        /// <summary>
        /// Gets an unique identifier which can be used to identify cached results
        /// </summary>
        public override string Uid
        {
            get { return project.Module.Name + "." + project.Name; }
        }

        /// <summary>
        /// Get the builders to be executed before this builder
        /// </summary>
        public override IEnumerable<IBuilder> Prerequisites
        {
            get
            {
                if (referenceBuilders == null)
                {
                    referenceBuilders = project.References.Where(r => r.Type == ReferenceType.Build).Select(CreateReferenceBuilder).ToList();
                }

                return referenceBuilders;
            }
        }

        private IReferenceBuilder CreateReferenceBuilder(Reference reference)
        {
            var builder = referenceBuilderFactory.CreateReferenceBuilder(reference, project);
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
        public override ISet<TargetRelativePath> Run(IBuildContext context)
        {
            var csprojPath = project.Name + ".csproj";
            const string csversionPath = "version.cs";
            TextWriter csversion = null;

            using (var csproj = project.RootDirectory.GetChildDirectory("cs").CreateTextFile(csprojPath))
            {
                if (!project.IsSDKProject())
                    csversion = project.RootDirectory.CreateTextFile(csversionPath);

                var references = new HashSet<TargetRelativePath>();
                foreach (var refBuilder in context.GetDependencies(this).OfType<IReferenceBuilder>().Where(r => r.Reference.Type == ReferenceType.Build))
                {
                    try
                    {
                        var builderResults = context.GetResults(refBuilder);
                        references.UnionWith(builderResults);
                    }
                    catch (InvalidOperationException ex)
                    {
                        log.ErrorFormat("Failed to get results of reference {0}: {1}", refBuilder, ex.Message);
                        throw;
                    }
                }

                generator.Generate(project, references, csproj, csversion, csversionPath);
                if (csversion != null)
                    csversion.Dispose();
            }

            var ret = new HashSet<TargetRelativePath>(
                new[]
                    {
                        new TargetRelativePath(String.Empty,
                            suite.SuiteRoot.GetRelativePathFrom(targetDir,
                                Path.Combine(suite.SuiteRoot.GetRelativePath(project.RootDirectory), "cs", csprojPath))),
                });

            if (csversion != null)
                ret.Add(new TargetRelativePath(String.Empty,
                            suite.SuiteRoot.GetRelativePathFrom(targetDir,
                                Path.Combine(suite.SuiteRoot.GetRelativePath(project.RootDirectory), csversionPath))));

            return ret;
        }

        public override void AddPrerequisite(IBuilder target)
        {
            if (referenceBuilders != null)
            {
                referenceBuilders.Add((IReferenceBuilder)target);
                dependencies = null;
                fullSourceDependencies = null;
            }

            base.AddPrerequisite(target);
        }

        public override void RemovePrerequisite(IBuilder target)
        {
            if (referenceBuilders != null)
            {
                if (referenceBuilders.Contains(target))
                {
                    referenceBuilders.Remove((IReferenceBuilder)target);
                    dependencies = null;
                    fullSourceDependencies = null;
                }
            }

            base.RemovePrerequisite(target);
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