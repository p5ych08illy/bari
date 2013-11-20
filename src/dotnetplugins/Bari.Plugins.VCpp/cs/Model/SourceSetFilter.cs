﻿using System.IO;
using Bari.Core.Generic;
using Bari.Core.Model;

namespace Bari.Plugins.VCpp.Model
{
    /// <summary>
    /// Filters the <c>cpp</c> source set to skip items generated by the build process
    /// </summary>
    public static class SourceSetFilter
    {
        private const string dllDataCHeader = @"/*********************************************************
   DllData file -- generated by MIDL compiler 

        DO NOT ALTER THIS FILE

   This file is regenerated by MIDL on every IDL file compile.

   To completely reconstruct this file, delete it and rerun MIDL
   on all the IDL files in this DLL, specifying this file for the
   /dlldata command line option

*********************************************************/";

        private const string idlInterfaceHeader = @"/* this ALWAYS GENERATED file contains the definitions for the interfaces */";
        private const string guidHeader = @"/* this ALWAYS GENERATED file contains the IIDs and CLSIDs */";
        private const string proxyStubHeader = @"/* this ALWAYS GENERATED file contains the proxy stub code */";

        private class FilterImpl: FilteredSourceSet
        {
            private readonly IFileSystemDirectory sourceSetRoot;
            private readonly IFileSystemDirectory suiteRoot;

            public FilterImpl(ISourceSet baseSourceSet, IFileSystemDirectory sourceSetRoot, IFileSystemDirectory suiteRoot) 
                : base(baseSourceSet)
            {
                this.sourceSetRoot = sourceSetRoot;
                this.suiteRoot = suiteRoot;
            }

            protected override bool FilterOut(SuiteRelativePath path)
            {
                return IsTLBorIDL(path) || 
                    IsDllDataC(path) ||
                    IsIdlHeader(path) ||
                    IsGuidDefinition(path) ||
                    IsProxyStub(path);
            }

            private static bool IsTLBorIDL(SuiteRelativePath path)
            {
                string ext = Path.GetExtension(path.ToString());
                if (ext != null)
                {
                    var extl = ext.ToLowerInvariant();
                    return extl == ".tlb" ||
                           extl == ".idl";
                }
                return false;
            }

            private bool IsDllDataC(SuiteRelativePath path)
            {
                // DllData.c is a special file generated by MIDL

                var fileName = Path.GetFileName(path);
                if (fileName != null && fileName.ToLowerInvariant() == "dlldata.c")
                {
                    using (var reader = sourceSetRoot.ReadTextFile(GetSourceSetRelativePath(path)))
                    {
                        var contents = reader.ReadToEnd();
                        if (contents.StartsWith(dllDataCHeader))
                            return true;
                    }
                }

                return false;
            }

            private bool IsIdlHeader(SuiteRelativePath path)
            {
                return HasExtensionAndContainsLine(path, ".h", idlInterfaceHeader);
            }

            private bool IsGuidDefinition(SuiteRelativePath path)
            {
                return HasExtensionAndContainsLine(path, ".c", guidHeader);
            }

            private bool IsProxyStub(SuiteRelativePath path)
            {
                return HasExtensionAndContainsLine(path, ".c", proxyStubHeader);
            }

            private bool HasExtensionAndContainsLine(SuiteRelativePath path, string extension, string expectedLine)
            {
                var ext = Path.GetExtension(path);
                if (ext != null && ext.ToLowerInvariant() == extension)
                {
                    using (var reader = sourceSetRoot.ReadTextFile(GetSourceSetRelativePath(path)))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line == expectedLine)
                                return true;
                        }
                    }
                }

                return false;
            }

            private string GetSourceSetRelativePath(SuiteRelativePath path)
            {
                return suiteRoot.GetRelativePathFrom(sourceSetRoot, path);
            }
        }

         public static ISourceSet FilterCppSourceSet(this SourceSet sourceSet, IFileSystemDirectory sourceSetRoot, IFileSystemDirectory suiteRoot)
         {
             return new FilterImpl(sourceSet, sourceSetRoot, suiteRoot);
         }
    }
}