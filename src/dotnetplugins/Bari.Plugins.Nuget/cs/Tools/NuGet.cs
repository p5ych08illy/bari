﻿using System;
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

        private string GetRelativePath(string path, LocalFileSystemDirectory root)
        {
            return path.Substring(root.AbsolutePath.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        private void AddDlls(DirectoryInfo libRoot, List<string> result, LocalFileSystemDirectory localRoot, NugetLibraryProfile maxProfile)
        {
            var lib80 = GetChild(libRoot, "net8.0");
            var lib70 = GetChild(libRoot, "net7.0");
            var lib60 = GetChild(libRoot, "net6.0");
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

            if (lib80 != null && maxProfile >= NugetLibraryProfile.Net80)
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