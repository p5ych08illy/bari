﻿using Bari.Core.Generic;

namespace Bari.Plugins.VsCore.Tools
{
    /// <summary>
    /// Interface for running MSBuild
    /// </summary>
    public interface IMSBuild
    {
        /// <summary>
        /// Runs MSBuild
        /// </summary>
        /// <param name="root">The root directory which will became MSBuild's root directory</param>
        /// <param name="relativePath">Relative path of the solution file (or MSBuild file) to be processed</param>
        /// <param name="restore">Is nuget resotre needed?</param>
        void Run(IFileSystemDirectory root, string relativePath, bool restore);
    }
}