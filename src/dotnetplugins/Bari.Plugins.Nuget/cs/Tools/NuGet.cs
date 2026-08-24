using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bari.Core.Generic;
using Bari.Core.Tools;
using Bari.Core.UI;
using Bari.Plugins.Nuget.Generic;

namespace Bari.Plugins.Nuget.Tools
{
    /// <summary>
    /// Default implementation of the <see cref="INuGet"/> interface, uses the command line NuGet tool in a separate process.
    /// </summary>
    public class NuGet : DownloadableExternalTool, INuGet
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof (NuGet));

        private readonly IParameters parameters;

        /// <summary>
        /// Creates the external tool
        /// </summary>
        public NuGet(IParameters parameters)
            : base("NuGet", @"C:\Programs\", "NuGet.exe", new Uri("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"), true, parameters)
        {
            this.parameters = parameters;
        }

        /// <summary>
        /// Installs a package and returns the path to the DLLs to be linked
        /// </summary>
        /// <param name="name">Package name</param>
        /// <param name="version">Package version, if null or empty then the latest one will be used</param>
        /// <param name="root">Root directory for storing the downloaded packages</param>
        /// <param name="relativeTargetDirectory">Path relative to <c>root</c> where the downloaded package should be placed</param>
        /// <param name="dllsOnly">If <c>true</c>, only the DLLs will be returned, otherwise all the files in the package</param>
        /// <param name="maxProfile">Maximum allowed profile</param>
        /// <returns>Returns the <c>root</c> relative paths of the DLL files to be used</returns>
        public Tuple<string, IEnumerable<string>> InstallPackage(string name, string version, IFileSystemDirectory root, string relativeTargetDirectory, bool dllsOnly, NugetLibraryProfile maxProfile)
        {
            string dir = string.IsNullOrEmpty(relativeTargetDirectory) ? "." : relativeTargetDirectory;
            if (String.IsNullOrWhiteSpace(version))
                Run(root, "install", name, "-o", "\""+dir+"\"", "-Verbosity", Verbosity);
            else
                Run(root, "install", name, "-Version", version, "-o", "\"" + dir + "\"", "-Verbosity", Verbosity);

            var result = new List<string>(); // root relative paths
            string commonRoot = String.Empty; // root relative path

            var localRoot = root as LocalFileSystemDirectory;
            if (localRoot != null)
            {
                var pkgRoot = new DirectoryInfo(Path.Combine(localRoot.AbsolutePath, relativeTargetDirectory));

                var modRoot = FindDirectory(pkgRoot, name);

                if (modRoot != null)
                {
                    var libRoot = modRoot.GetDirectories("lib", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    var contentRoot = modRoot.GetDirectories("content", SearchOption.TopDirectoryOnly).FirstOrDefault();
                    commonRoot = GetRelativePath(modRoot.FullName, localRoot);

                    if (libRoot != null)
                    {
                        AddDlls(libRoot, result, localRoot, maxProfile);
                        commonRoot = GetRelativePath(libRoot.FullName, localRoot);
                    }
                    if (contentRoot != null && !dllsOnly)
                    {
                        AddContents(contentRoot, result, localRoot);

                        if (libRoot == null)
                            commonRoot = GetRelativePath(contentRoot.FullName, localRoot);
                    }
                }
            }

            log.DebugFormat("Returning common root {0}", commonRoot);
            return Tuple.Create(commonRoot, result.AsEnumerable());
        }
        private DirectoryInfo GetChild(DirectoryInfo parent, string name)
        {
            return parent.EnumerateDirectories().FirstOrDefault(child => child.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));
        }

        private DirectoryInfo FindDirectory(DirectoryInfo parent, string prefix)
        {
            return parent.EnumerateDirectories().FirstOrDefault(child => child.Name.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Finds the lib directory of a .NET (Core) target framework, accepting a platform specific
        /// variant of it as well
        /// </summary>
        /// <remarks>
        /// A package built from a project targeting <c>net10.0-windows</c> keeps its assemblies under
        /// <c>lib\net10.0-windows7.0</c>, not under <c>lib\net10.0</c> - MSBuild normalizes the moniker
        /// and NuGet uses it verbatim as the directory name. Looking for the exact name only would
        /// silently find nothing and fall through to the empty <c>lib\</c> root.
        ///
        /// <para>An exact match wins; after it a Windows specific one, because that is what a WPF or
        /// WinForms library ships as; and only then any other platform.</para>
        /// </remarks>
        /// <param name="parent">The package's <c>lib</c> directory</param>
        /// <param name="tfm">Target framework moniker without a platform, for example <c>net10.0</c></param>
        /// <returns>Returns the directory, or <c>null</c> if the package has nothing for this framework</returns>
        private DirectoryInfo GetNetChild(DirectoryInfo parent, string tfm)
        {
            var exact = GetChild(parent, tfm);
            if (exact != null)
                return exact;

            return parent.EnumerateDirectories()
                         .Where(child => child.Name.StartsWith(tfm + "-", StringComparison.InvariantCultureIgnoreCase))
                         .OrderBy(child => child.Name.StartsWith(tfm + "-windows", StringComparison.InvariantCultureIgnoreCase) ? 0 : 1)
                         .ThenBy(child => child.Name, StringComparer.InvariantCultureIgnoreCase)
                         .FirstOrDefault();
        }

        public void CreatePackage(IFileSystemDirectory targetRoot, string packageName, string nuspec)
        {
            var localRoot = targetRoot as LocalFileSystemDirectory;
            if (localRoot != null)
            {
                var nuSpecName = packageName + ".nuspec";
                using (var writer = localRoot.CreateTextFile(nuSpecName))
                    writer.WriteLine(nuspec);

                Run(targetRoot, "pack", nuSpecName, "-Verbosity", Verbosity);
            }
        }

        public void PublishPackage(IFileSystemDirectory targetRoot, string packageName, string version, string apiKey)
        {
            var localRoot = targetRoot as LocalFileSystemDirectory;
            if (localRoot != null)
            {
                var nuPkgName = string.Format("{0}.{1}.nupkg", packageName, version);
                Run(targetRoot, "push", nuPkgName, apiKey, "-NonInteractive", "-Verbosity", Verbosity, "-Source", "https://api.nuget.org/v3/index.json");
            }
        }

        /// <summary>
        /// Packs an existing nuspec file with an explicit base path, version and property set
        /// </summary>
        public bool Pack(IFileSystemDirectory workingDirectory, string nuspecPath, string basePath,
                         string outputDirectory, string version, string properties)
        {
            return Run(workingDirectory,
                       "pack", Quote(nuspecPath),
                       "-BasePath", Quote(basePath),
                       "-OutputDirectory", Quote(outputDirectory),
                       "-Version", Quote(version),
                       "-Properties", Quote(properties),
                       "-NonInteractive",
                       "-Verbosity", Verbosity);
        }

        /// <summary>
        /// Pushes an already created package to a feed given either by name or by URL
        /// </summary>
        public bool Push(IFileSystemDirectory workingDirectory, string packagePath, string feed,
                         string apiKey, bool skipDuplicate)
        {
            var args = new List<string>
                {
                    "push", Quote(packagePath),
                    "-Source", Quote(feed)
                };

            if (!String.IsNullOrEmpty(apiKey))
            {
                args.Add("-ApiKey");
                args.Add(Quote(apiKey));
            }

            if (skipDuplicate)
                args.Add("-SkipDuplicate");

            args.Add("-NonInteractive");
            args.Add("-Verbosity");
            args.Add(Verbosity);

            return Run(workingDirectory, args.ToArray());
        }

        /// <summary>
        /// Quotes a command line argument
        ///
        /// <para><see cref="ExternalTool.Run"/> joins the arguments with spaces without any quoting,
        /// so every path and property value has to be quoted by hand. The trailing backslashes have to
        /// go: a backslash before the closing quote escapes the quote itself.</para>
        /// </summary>
        private static string Quote(string value)
        {
            if (value == null)
                return "\"\"";

            return "\"" + value.TrimEnd('\\') + "\"";
        }

        private string GetRelativePath(string path, LocalFileSystemDirectory root)
        {
            return path.Substring(root.AbsolutePath.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        private void AddDlls(DirectoryInfo libRoot, List<string> result, LocalFileSystemDirectory localRoot, NugetLibraryProfile maxProfile)
        {
            var lib100 = GetNetChild(libRoot, "net10.0");
            var lib90 = GetNetChild(libRoot, "net9.0");
            var lib80 = GetNetChild(libRoot, "net8.0");
            var lib70 = GetNetChild(libRoot, "net7.0");
            var lib60 = GetNetChild(libRoot, "net6.0");
            var lib45 = GetChild(libRoot, "net45-full") ??
                        GetChild(libRoot, "net45");
            var lib40 = GetChild(libRoot, "net40-full") ??
                        GetChild(libRoot, "net40") ??
                        GetChild(libRoot, "net4");
            var lib40client = GetChild(libRoot, "net40-client");
            var lib35 = GetChild(libRoot, "net35");
            var lib35client = GetChild(libRoot, "net35-client");
            var lib20 = GetChild(libRoot, "net20") ??
                        GetChild(libRoot, "20");
            var lib20standard = GetChild(libRoot, "netstandard2.0");

            if (lib100 != null && maxProfile >= NugetLibraryProfile.Net100)
                result.AddRange(GetDllsIn(localRoot, lib100));
            else if (lib90 != null && maxProfile >= NugetLibraryProfile.Net90)
                result.AddRange(GetDllsIn(localRoot, lib90));
            else if (lib80 != null && maxProfile >= NugetLibraryProfile.Net80)
                result.AddRange(GetDllsIn(localRoot, lib80));
            else if (lib70 != null && maxProfile >= NugetLibraryProfile.Net70)
                result.AddRange(GetDllsIn(localRoot, lib70));
            else if (lib60 != null && maxProfile >= NugetLibraryProfile.Net60)
                result.AddRange(GetDllsIn(localRoot, lib60));
            else if (lib20standard != null && maxProfile >= NugetLibraryProfile.Net472)
                result.AddRange(GetDllsIn(localRoot, lib20standard));
            else if (lib45 != null && maxProfile == NugetLibraryProfile.Net45)
                result.AddRange(GetDllsIn(localRoot, lib45));
            else if (lib40 != null && maxProfile >= NugetLibraryProfile.Net4)
                result.AddRange(GetDllsIn(localRoot, lib40));
            else if (lib40client != null && maxProfile >= NugetLibraryProfile.Net4Client)
                result.AddRange(GetDllsIn(localRoot, lib40client));
            else if (lib35 != null && maxProfile != NugetLibraryProfile.Net35)
                result.AddRange(GetDllsIn(localRoot, lib35));
            else if (lib35client != null && maxProfile != NugetLibraryProfile.Net35Client)
                result.AddRange(GetDllsIn(localRoot, lib35client));
            else if (lib20 != null && maxProfile != NugetLibraryProfile.Net2)
                result.AddRange(GetDllsIn(localRoot, lib20));
            else
                result.AddRange(GetDllsIn(localRoot, libRoot));
        }

        private void AddContents(DirectoryInfo contentRoot, List<string> result, LocalFileSystemDirectory localRoot)
        {                
            result.AddRange(GetAllIn(localRoot, contentRoot));
        }

        private IEnumerable<string> GetDllsIn(LocalFileSystemDirectory root, DirectoryInfo dir)
        {
            log.DebugFormat("Getting DLLs from {0} relative to {1}...", dir.FullName, root.AbsolutePath);

            return from file in dir.GetFiles("*.dll")
                let relPath = GetRelativePath(file.FullName, root)
                select relPath;
        }

        private IEnumerable<string> GetAllIn(LocalFileSystemDirectory root, DirectoryInfo dir)
        {
            log.DebugFormat("Getting all files from {0} relative to {1}...", dir.FullName, root.AbsolutePath);

            return from file in dir.RecursiveGetFiles()
                let relPath = GetRelativePath(file.FullName, root)
                select relPath;
        }

        private string Verbosity
        {
            get { return parameters.VerboseOutput ? "detailed" : "quiet"; }
        }
    }
}