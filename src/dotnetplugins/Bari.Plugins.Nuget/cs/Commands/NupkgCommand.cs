using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bari.Core.Commands;
using Bari.Core.Commands.Helper;
using Bari.Core.Exceptions;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Core.UI;
using Bari.Plugins.Nuget.Model;
using Bari.Plugins.Nuget.Tools;

namespace Bari.Plugins.Nuget.Commands
{
    /// <summary>
    /// Implements the `nupkg` command, which creates NuGet packages from the hand written
    /// <c>nuspec\*.nuspec</c> files of the projects, and optionally pushes them to a feed.
    ///
    /// <para>The command never builds anything: it packs what `bari build` has already put into
    /// <c>target\&lt;Module&gt;</c>. Hence no build context and no <see cref="IHasBuildTarget"/>.</para>
    /// </summary>
    public class NupkgCommand : ICommand
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(NupkgCommand));

        /// <summary>
        /// Name of the source set holding the nuspec files of a project
        /// </summary>
        private const string NuspecSourceSet = "nuspec";

        private static readonly Regex idRegex =
            new Regex(@"<id>\s*([^<]+?)\s*</id>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex fileSrcRegex =
            new Regex("<file\\s[^>]*src\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// The same pattern the model loader resolves environment variable references with
        /// </summary>
        private static readonly Regex variableRefRegex =
            new Regex(@"\$[a-zA-Z0-9_]+", RegexOptions.Compiled);

        private static readonly Regex metadataEndRegex =
            new Regex(@"</metadata\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex versionRegex =
            new Regex(@"<version>\s*([^<]+?)\s*</version>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly ICommandTargetParser targetParser;
        private readonly IFileSystemDirectory suiteRoot;
        private readonly IFileSystemDirectory targetRoot;
        private readonly INuGet nuget;
        private readonly IUserOutput output;

        /// <summary>
        /// Gets the name of the command. This is the string which can be used on the command line interface
        /// to access the particular command.
        /// </summary>
        public string Name
        {
            get { return "nupkg"; }
        }

        /// <summary>
        /// Gets a short, one-liner description of the command
        /// </summary>
        public string Description
        {
            get { return "creates NuGet packages from the nuspec/*.nuspec files of the already built target"; }
        }

        /// <summary>
        /// Gets a detailed, multiline description of the command and its possible parameters, usage examples
        /// </summary>
        public string Help
        {
            get
            {
                return
@"=Nupkg command=

Creates a NuGet package from every nuspec file of the given target, and optionally pushes them
to a NuGet feed.

The nuspec files live in the `nuspec` subdirectory of a project, beside `cs`. They need no
declaration in the suite definition, and no builder consumes them, so they never take part in
a build.

This command *never builds anything*. It packs what `bari build` has already produced, so the
build has to be run first. If the output directory is missing, the command fails instead of
building it.

A bare `file src` in a nuspec is relative to the *base path*, and the base path follows the target:

- for a *product* target it is `target/<product>`, which holds everything the product ships - the
  output of every module in it, and their content files;
- for a module, project or whole-suite target it is `target/<module>` of the project the nuspec
  belongs to, which only has what that one module compiled.

Prefer the product form. A nuspec often refers to a file built by another module of the same
product, and only the product directory has all of them together. The `$targetdir$` token always
expands to whichever base path is in effect.

It is not the same as `bari pack`: that one packs a whole *product* into a *single* package from a
generated nuspec, driven by the `packager: type: nuget` block of the product.

The target is optional and can be given in three forms, exactly like for `bari build`:

- nothing, which means the whole suite: `bari nupkg`
- a module or a product name: `bari nupkg MyModule`
- a module and a project name: `bari nupkg MyModule.MyProject`

The supported options are:

- `--publish` pushes the packages after packing them
- `--version <v>` overrides the version coming from the model
- `--feed <url>` overrides the feed URL of the `nupkg` block
- `--output <dir>` overrides the target relative output directory

Everything else is configured in the `nupkg` block of the suite definition:

    nupkg:
      feed: https://pkgs.dev.azure.com/<org>/<project>/_packaging/<feed>/nuget/v3/index.json

The other keys are `output` (default `nupkg`), `tfm` (default `net10.0-windows7.0`),
`skip-duplicate` (default true) and `strict-inputs` (default true).

The command handles *no credentials at all*. Pushing relies on the machine being authenticated to
the feed already: on a developer machine the NuGet credential provider caches that after the first
interactive sign-in, and on a build agent an Azure DevOps `NuGetAuthenticate` step sets it up for
the build identity. Nothing is ever written to the machine's `NuGet.Config`.

Whether a run publishes is decided on the command line, not in the suite definition, so that the
same suite can be built without publishing anywhere.
";
            }
        }

        /// <summary>
        /// If <c>true</c>, the target goal is important for this command and must be explicitly specified by the user
        /// (if the available goal set is not the default)
        /// </summary>
        /// <remarks>
        /// Nothing is built and <c>target\</c> is not goal dependent, so the goal does not matter here.
        /// </remarks>
        public bool NeedsExplicitTargetGoal
        {
            get { return false; }
        }

        /// <summary>
        /// Initializes the command
        /// </summary>
        /// <param name="targetParser">Parser used for parsing the target parameter</param>
        /// <param name="suiteRoot">Suite's root directory</param>
        /// <param name="targetRoot">Build target root directory</param>
        /// <param name="nuget">Interface to the NuGet command line tool</param>
        /// <param name="output">Output interface</param>
        public NupkgCommand(ICommandTargetParser targetParser, [SuiteRoot] IFileSystemDirectory suiteRoot,
                            [TargetRoot] IFileSystemDirectory targetRoot, INuGet nuget, IUserOutput output)
        {
            this.targetParser = targetParser;
            this.suiteRoot = suiteRoot;
            this.targetRoot = targetRoot;
            this.nuget = nuget;
            this.output = output;
        }

        /// <summary>
        /// Runs the command
        /// </summary>
        /// <param name="suite">The current suite model the command is applied to</param>
        /// <param name="parameters">Parameters given to the command (in unprocessed form)</param>
        /// <returns>Returns <c>true</c> if the command succeeded</returns>
        public bool Run(Suite suite, string[] parameters)
        {
            var args = new Arguments(parameters);

            var np = suite.HasParameters("nupkg")
                         ? suite.GetParameters<NupkgParameters>("nupkg")
                         : new NupkgParameters();

            CommandTarget target;
            try
            {
                target = targetParser.ParseTarget(args.Target);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidCommandParameterException("nupkg", ex.Message);
            }

            var failures = new List<string>();
            var jobs = CollectJobs(target, args, np, failures);

            if (jobs.Count == 0 && failures.Count == 0)
            {
                output.Message("no nuspec files were found - nothing to pack");
                return true;
            }

            // Only now, so that a target without packages does not leave an empty directory behind
            string outputRelativePath = args.Output ?? np.Output;
            targetRoot.CreateDirectory(outputRelativePath);
            string outputPath = Path.Combine(AbsolutePathOf(targetRoot), outputRelativePath);

            var packages = new List<string>();
            foreach (var job in jobs)
                Pack(job, np, outputPath, packages, failures);

            if (failures.Count > 0)
            {
                output.Error("NuGet packaging failed:");
                output.Indent();
                foreach (var failure in failures)
                    output.Error(failure);
                output.Unindent();

                return false;
            }

            if (args.Publish)
                return Publish(packages, args, np);

            return true;
        }

        /// <summary>
        /// Collects a pack job for every nuspec file of every non-test project of the target
        /// </summary>
        private List<PackJob> CollectJobs(CommandTarget target, Arguments args, NupkgParameters np, List<string> failures)
        {
            var result = new List<PackJob>();

            string suitePath = AbsolutePathOf(suiteRoot);
            string targetPath = AbsolutePathOf(targetRoot);

            // When the target is a product, the base path is the product's own output directory.
            // A product build collects everything the product ships into target\<Product>: the
            // output of every module it contains, plus their content source sets. target\<Module>
            // holds only what that one module compiled, which is not enough for a nuspec that
            // refers to a file coming from a different module of the same product.
            var productTarget = target as ProductTarget;
            string productDir = null;

            if (productTarget != null)
            {
                productDir = Path.Combine(targetPath, productTarget.Product.Name);

                if (!Directory.Exists(productDir))
                {
                    failures.Add(String.Format(
                        "the output directory of the {0} product ({1}) does not exist - run `bari build {0}` first",
                        productTarget.Product.Name, productDir));

                    return result;
                }
            }

            foreach (var project in target.Projects.Where(project => !(project is TestProject)))
            {
                if (!project.HasNonEmptySourceSet(NuspecSourceSet))
                    continue;

                string moduleTargetDir = productDir;

                if (moduleTargetDir == null)
                {
                    // Every project of a module compiles into target\<Module>
                    moduleTargetDir = Path.Combine(targetPath, project.RelativeTargetPath);
                    if (!Directory.Exists(moduleTargetDir))
                    {
                        failures.Add(String.Format(
                            "{0}.{1}: the build output directory {2} does not exist - run `bari build` first",
                            project.Module.Name, project.Name, moduleTargetDir));
                        continue;
                    }
                }

                foreach (var file in project.GetSourceSet(NuspecSourceSet).Files)
                {
                    string relativePath = file;
                    if (!relativePath.EndsWith(".nuspec", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    string nuspecPath = Path.Combine(suitePath, relativePath);

                    string contents = null;
                    string readError = null;
                    try
                    {
                        contents = File.ReadAllText(nuspecPath);
                    }
                    catch (IOException ex)
                    {
                        readError = ex.Message;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        readError = ex.Message;
                    }

                    if (readError != null)
                    {
                        failures.Add(String.Format("{0}: could not be read ({1})", nuspecPath, readError));
                        continue;
                    }

                    string version = PinnedVersionOf(contents) ?? args.Version ?? project.EffectiveVersion;
                    if (String.IsNullOrWhiteSpace(version))
                    {
                        output.Warning(
                            String.Format("{0}.{1} has no version, {2} is skipped",
                                          project.Module.Name, project.Name, Path.GetFileName(nuspecPath)),
                            new[]
                                {
                                    "Add a `version:` key to the project, its module or the suite",
                                    "Or write the version into the nuspec, or use `--version <v>`"
                                });
                        continue;
                    }

                    var job = new PackJob(project, nuspecPath, contents,
                                          Path.GetDirectoryName(nuspecPath), moduleTargetDir,
                                          version, np.Tfm,
                                          project.EffectiveCompany, project.EffectiveCopyright);
                    job.PackageId = GetPackageId(job);

                    log.DebugFormat("Collected {0} from {1}", job.PackageId, nuspecPath);
                    result.Add(job);
                }
            }

            return result;
        }

        /// <summary>
        /// Packs a single job, adding either the created package's path to <c>packages</c> or a message
        /// to <c>failures</c>
        /// </summary>
        private void Pack(PackJob job, NupkgParameters np, string outputPath, List<string> packages, List<string> failures)
        {
            var missingInputs = GetMissingInputs(job);
            if (missingInputs.Count > 0)
            {
                string message = String.Format("{0}: the nuspec refers to files which do not exist: {1}",
                                               job.PackageId, String.Join(", ", missingInputs.ToArray()));

                if (np.StrictInputs)
                {
                    failures.Add(message);
                }
                else
                {
                    output.Warning(message, new[] { "The package is skipped" });
                }

                return;
            }

            string properties = String.Format("tfm={0};targetdir={1};nuspecdir={2};module={3};project={4};version={5}",
                                              job.Tfm, job.TargetDirectory, job.NuspecDirectory,
                                              job.Project.Module.Name, job.Project.Name, job.Version);

            string packageFileName = job.PackageId + "." + job.Version + ".nupkg";
            output.Message("{0}.{1} -> {2}", job.Project.Module.Name, job.Project.Name, packageFileName);

            if (nuget.Pack(suiteRoot, PrepareNuspec(job), job.TargetDirectory, outputPath, job.Version, properties))
                packages.Add(Path.Combine(outputPath, packageFileName));
            else
                failures.Add(String.Format("{0}: `nuget pack` failed for {1}", job.PackageId, job.NuspecPath));
        }

        /// <summary>
        /// Pushes the created packages to the feed
        ///
        /// <para>No credential is handled here. `nuget push` authenticates through the machine's
        /// NuGet credential provider, which a developer machine has after its first interactive
        /// sign-in and a build agent has after a `NuGetAuthenticate` step.</para>
        /// </summary>
        private bool Publish(List<string> packages, Arguments args, NupkgParameters np)
        {
            string feed = args.Feed ?? np.Feed;

            if (String.IsNullOrWhiteSpace(feed))
            {
                output.Error("There is no feed to publish to: set `feed` in the `nupkg` block, or use `--feed <url>`");
                return false;
            }

            if (!CheckResolved("feed", feed))
                return false;

            bool success = true;
            foreach (var package in packages)
            {
                string packageFileName = Path.GetFileName(package);

                if (!File.Exists(package))
                {
                    output.Warning(String.Format("{0} has not been created, it is not published", packageFileName));
                    continue;
                }

                output.Message("Publishing {0}...", packageFileName);

                // Azure Artifacts ignores the value but insists that a key is present; `az` is the
                // conventional placeholder for it.
                if (!nuget.Push(suiteRoot, package, feed, "az", np.SkipDuplicate))
                {
                    output.Error(String.Format("Failed to publish {0}", packageFileName));
                    success = false;
                }
            }

            return success;
        }

        /// <summary>
        /// A version the nuspec pins for itself, or <c>null</c> to take it from the model
        ///
        /// <para>Almost every package of a suite should carry the version of the build that produced
        /// it, which is why the model wins by default. A vendored third party binary is the
        /// exception: its version is a property of the binary, it does not change when the suite is
        /// rebuilt, and the reference to it should not have to be edited on every build. Such a
        /// nuspec states its own version and that one wins - over the model and over
        /// <c>--version</c> alike, because it is part of the package's identity.</para>
        ///
        /// <para><c>$version$</c> - or any other token - and the conventional <c>0.0.0</c>
        /// placeholder both mean "take it from the model".</para>
        /// </summary>
        private static string PinnedVersionOf(string contents)
        {
            var match = versionRegex.Match(contents);
            if (!match.Success)
                return null;

            string value = match.Groups[1].Value;

            if (value.Contains("$") || value == "0.0.0")
                return null;

            return value;
        }

        /// <summary>
        /// Returns the path of the nuspec to hand to <c>nuget pack</c>
        ///
        /// <para>The suite model already knows the company and the copyright, and every package of
        /// the suite should carry the same ones. So when the nuspec does not state them, they are
        /// filled in from <see cref="Project.EffectiveCompany"/> and
        /// <see cref="Project.EffectiveCopyright"/>, which cascade project → module → suite.</para>
        ///
        /// <para>A nuspec which does state them is left alone: an explicit value in the file always
        /// wins. If nothing has to be added, the original file is packed and no copy is made.</para>
        ///
        /// <para><c>-BasePath</c> is passed explicitly and <c>$nuspecdir$</c> points at the original
        /// directory, so packing a copy from somewhere else changes nothing about how the
        /// <c>file src</c> entries resolve.</para>
        /// </summary>
        private string PrepareNuspec(PackJob job)
        {
            var additions = new List<string>();

            if (!String.IsNullOrWhiteSpace(job.Company))
            {
                if (!HasElement(job.Contents, "authors"))
                    additions.Add("<authors>" + Escape(job.Company) + "</authors>");

                if (!HasElement(job.Contents, "owners"))
                    additions.Add("<owners>" + Escape(job.Company) + "</owners>");
            }

            if (!String.IsNullOrWhiteSpace(job.Copyright) && !HasElement(job.Contents, "copyright"))
                additions.Add("<copyright>" + Escape(job.Copyright) + "</copyright>");

            if (additions.Count == 0)
                return job.NuspecPath;

            var end = metadataEndRegex.Match(job.Contents);
            if (!end.Success)
            {
                // No metadata element at all - leave it to nuget.exe to complain about it
                log.WarnFormat("{0} has no <metadata> element, nothing was filled in", job.NuspecPath);
                return job.NuspecPath;
            }

            string indent = "    ";
            string generated = job.Contents.Insert(
                end.Index,
                String.Join("", additions.Select(a => a + Environment.NewLine + indent).ToArray()));

            string generatedDir = Path.Combine(AbsolutePathOf(targetRoot), "tmp", "nupkg");
            Directory.CreateDirectory(generatedDir);

            string generatedPath = Path.Combine(generatedDir, job.PackageId + ".nuspec");
            File.WriteAllText(generatedPath, generated, Encoding.UTF8);

            log.DebugFormat("Packing {0} from {1}, with {2} element(s) filled in from the model",
                            job.PackageId, generatedPath, additions.Count);

            return generatedPath;
        }

        /// <summary>
        /// Checks whether the nuspec states an element, so that it does not get filled in
        /// </summary>
        private static bool HasElement(string nuspec, string name)
        {
            return Regex.IsMatch(nuspec, "<" + name + @"\b[^>]*>", RegexOptions.IgnoreCase);
        }

        private static string Escape(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        /// <summary>
        /// Catches a <c>$VARIABLE</c> reference the model loader could not resolve
        ///
        /// <para>When the environment variable is not set, the reference is left in the value
        /// verbatim. Without this check the feed would be registered with the literal text
        /// <c>$AZ_ARTIFACTS_PAT</c> as its password, and the push would only fail later with a
        /// meaningless 401.</para>
        ///
        /// <para>Only the name of the variable is reported, never the value of a resolved one.</para>
        /// </summary>
        private bool CheckResolved(string key, string value)
        {
            if (value == null)
                return true;

            var match = variableRefRegex.Match(value);
            if (!match.Success)
                return true;

            output.Error(String.Format(
                "`{0}` refers to the environment variable {1}, which is not set",
                key, match.Value));

            return false;
        }

        /// <summary>
        /// Gets the package's id from the <c>id</c> element of the nuspec
        ///
        /// <para>It cannot come from the file name: a single nuspec directory may hold more than one
        /// package definition.</para>
        /// </summary>
        private static string GetPackageId(PackJob job)
        {
            var match = idRegex.Match(job.Contents);
            if (match.Success)
                return Expand(match.Groups[1].Value, job);

            return Path.GetFileNameWithoutExtension(job.NuspecPath);
        }

        /// <summary>
        /// Collects the <c>file src</c> entries of the nuspec which do not exist
        /// </summary>
        private static List<string> GetMissingInputs(PackJob job)
        {
            var result = new List<string>();

            foreach (Match match in fileSrcRegex.Matches(job.Contents))
            {
                string src = Expand(match.Groups[1].Value, job).Replace('/', '\\');

                // Wildcards are left to nuget.exe to resolve
                if (src.Contains("*") || src.Contains("?"))
                    continue;

                string path = Path.IsPathRooted(src) ? src : Path.Combine(job.TargetDirectory, src);

                if (!File.Exists(path) && !Directory.Exists(path))
                    result.Add(src);
            }

            return result;
        }

        /// <summary>
        /// Applies the same token substitutions which nuget.exe will apply from <c>-Properties</c>
        /// </summary>
        private static string Expand(string text, PackJob job)
        {
            return text
                .Replace("$tfm$", job.Tfm)
                .Replace("$targetdir$", job.TargetDirectory)
                .Replace("$nuspecdir$", job.NuspecDirectory)
                .Replace("$module$", job.Project.Module.Name)
                .Replace("$project$", job.Project.Name)
                .Replace("$version$", job.Version);
        }

        private static string AbsolutePathOf(IFileSystemDirectory directory)
        {
            var local = directory as LocalFileSystemDirectory;
            if (local == null)
                throw new NotSupportedException("The nupkg command only supports the local file system.");

            return local.AbsolutePath;
        }

        /// <summary>
        /// Everything needed to pack one nuspec file
        /// </summary>
        private class PackJob
        {
            private readonly Project project;
            private readonly string nuspecPath;
            private readonly string contents;
            private readonly string nuspecDirectory;
            private readonly string targetDirectory;
            private readonly string version;
            private readonly string tfm;
            private readonly string company;
            private readonly string copyright;
            private string packageId;

            public PackJob(Project project, string nuspecPath, string contents, string nuspecDirectory,
                           string targetDirectory, string version, string tfm,
                           string company, string copyright)
            {
                this.project = project;
                this.nuspecPath = nuspecPath;
                this.contents = contents;
                this.nuspecDirectory = nuspecDirectory;
                this.targetDirectory = targetDirectory;
                this.version = version;
                this.tfm = tfm;
                this.company = company;
                this.copyright = copyright;
            }

            /// <summary>
            /// The project's effective company (project → module → suite), or <c>null</c>
            /// </summary>
            public string Company
            {
                get { return company; }
            }

            /// <summary>
            /// The project's effective copyright (project → module → suite), or <c>null</c>
            /// </summary>
            public string Copyright
            {
                get { return copyright; }
            }

            public Project Project
            {
                get { return project; }
            }

            public string NuspecPath
            {
                get { return nuspecPath; }
            }

            public string Contents
            {
                get { return contents; }
            }

            /// <summary>
            /// Value of the <c>$nuspecdir$</c> token
            /// </summary>
            public string NuspecDirectory
            {
                get { return nuspecDirectory; }
            }

            /// <summary>
            /// Value of the <c>$targetdir$</c> token, also the base path of the packaging
            /// </summary>
            public string TargetDirectory
            {
                get { return targetDirectory; }
            }

            public string Version
            {
                get { return version; }
            }

            public string Tfm
            {
                get { return tfm; }
            }

            public string PackageId
            {
                get { return packageId; }
                set { packageId = value; }
            }
        }

        /// <summary>
        /// The parsed command line of the command
        /// </summary>
        private class Arguments
        {
            private readonly string target = String.Empty;
            private readonly bool publish;
            private readonly string version;
            private readonly string feed;
            private readonly string output;

            public Arguments(string[] parameters)
            {
                bool hasTarget = false;

                for (int i = 0; i < parameters.Length; i++)
                {
                    string arg = parameters[i];

                    switch (arg.ToLowerInvariant())
                    {
                        case "--publish":
                            publish = true;
                            break;
                        case "--version":
                            version = GetValue(parameters, ref i, arg);
                            break;
                        case "--feed":
                            feed = GetValue(parameters, ref i, arg);
                            break;
                        case "--output":
                            output = GetValue(parameters, ref i, arg);
                            break;
                        default:
                            if (arg.StartsWith("-"))
                                throw new InvalidCommandParameterException(
                                    "nupkg", String.Format("Unknown option: {0}", arg));

                            if (hasTarget)
                                throw new InvalidCommandParameterException(
                                    "nupkg", "Must be called with at most one target");

                            target = arg;
                            hasTarget = true;
                            break;
                    }
                }
            }

            /// <summary>
            /// Target of the command, an empty string meaning the whole suite
            /// </summary>
            public string Target
            {
                get { return target; }
            }

            public string Version
            {
                get { return version; }
            }

            public string Feed
            {
                get { return feed; }
            }

            public string Output
            {
                get { return output; }
            }

            /// <summary>
            /// Whether the packages have to be pushed after packing
            ///
            /// <para>This comes from the command line only. Whether a given run publishes is a
            /// property of the pipeline invoking it, not of the suite, so there is no matching key
            /// in the `nupkg` block - and therefore nothing to override with a `--no-publish`
            /// either.</para>
            /// </summary>
            public bool Publish
            {
                get { return publish; }
            }

            private static string GetValue(string[] parameters, ref int i, string option)
            {
                i++;

                if (i >= parameters.Length)
                    throw new InvalidCommandParameterException(
                        "nupkg", String.Format("The {0} option requires a value", option));

                return parameters[i];
            }
        }
    }
}
