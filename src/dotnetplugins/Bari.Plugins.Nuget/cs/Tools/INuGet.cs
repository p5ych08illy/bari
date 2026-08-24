using System;
using System.Collections.Generic;
using Bari.Core.Generic;

namespace Bari.Plugins.Nuget.Tools
{
    /// <summary>
    /// Provides an interface to the NuGet package manager
    /// </summary>
    public interface INuGet
    {
        /// <summary>
        /// Installs a package and returns the path to the files to be linked
        /// </summary>
        /// <param name="name">Package name</param>
        /// <param name="version">Package version, if null or empty then the latest one will be used</param>
        /// <param name="root">Root directory for storing the downloaded packages</param>
        /// <param name="relativeTargetDirectory">Path relative to <c>root</c> where the downloaded package should be placed</param>
        /// <param name="dllsOnly">If <c>true</c>, only the DLLs will be returned, otherwise all the files in the package</param>
        /// <param name="maxProfile">Maximum allowed profile</param>
        /// <returns>Returns the <c>root</c> relative paths of the files to be used, and the common root for them 
        /// to help preserving the package's directory structure</returns>
        Tuple<string, IEnumerable<string>> InstallPackage(string name, string version, IFileSystemDirectory root, string relativeTargetDirectory, bool dllsOnly, NugetLibraryProfile maxProfile);

        /// <summary>
        /// Creates a package based on a nuspec
        /// </summary>
        /// <param name="targetRoot">Target root directory</param>
        /// <param name="packageName">Name of the package to be generated</param>
        /// <param name="nuspec">The package's nuspec in XML</param>
        void CreatePackage(IFileSystemDirectory targetRoot, string packageName, string nuspec);

        /// <summary>
        /// Publishes an already created NuGet package
        /// </summary>
        /// <param name="targetRoot">Target root directory</param>
        /// <param name="packageName">Name of te package to be published</param>
        /// <param name="version">Package version</param>
        /// <param name="apiKey">NuGet API key</param>
        void PublishPackage(IFileSystemDirectory targetRoot, string packageName, string version, string apiKey);

        /// <summary>
        /// Packs an existing nuspec file with an explicit base path, version and property set
        ///
        /// <para>Unlike <see cref="CreatePackage"/> this does not generate the nuspec: it is used by the
        /// <c>nupkg</c> command to pack the hand written nuspec files of the projects.</para>
        /// </summary>
        /// <param name="workingDirectory">Working directory of the tool</param>
        /// <param name="nuspecPath">Absolute path of the nuspec file to be packed</param>
        /// <param name="basePath">Directory the <c>src</c> attributes of the nuspec are relative to</param>
        /// <param name="outputDirectory">Directory the generated package is written to</param>
        /// <param name="version">Package version, overriding the one in the nuspec</param>
        /// <param name="properties">Semicolon separated <c>name=value</c> pairs for the nuspec's tokens</param>
        /// <returns>Returns <c>true</c> if the package has been created</returns>
        bool Pack(IFileSystemDirectory workingDirectory, string nuspecPath, string basePath,
                  string outputDirectory, string version, string properties);

        /// <summary>
        /// Pushes an already created package to a feed given either by name or by URL
        ///
        /// <para>Unlike <see cref="PublishPackage"/> this does not hard code the nuget.org feed.
        /// It does not authenticate either: the caller is expected to be on a machine which is
        /// already authenticated to the feed.</para>
        /// </summary>
        /// <param name="workingDirectory">Working directory of the tool</param>
        /// <param name="packagePath">Absolute path of the package to be pushed</param>
        /// <param name="feed">Name of a registered source, or the URL of a feed</param>
        /// <param name="apiKey">API key of the feed</param>
        /// <param name="skipDuplicate">If <c>true</c>, an already published version is not an error</param>
        /// <returns>Returns <c>true</c> if the package has been pushed</returns>
        bool Push(IFileSystemDirectory workingDirectory, string packagePath, string feed,
                  string apiKey, bool skipDuplicate);
    }
}