using System.Globalization;
using System.IO;
using System.Linq;
using Bari.Core.Generic;
using Mercurial;
using System;

namespace Bari.Plugins.Vcs.Hg
{
    public class MercurialSuite
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof (MercurialSuite));

        private readonly IFileSystemDirectory suiteRoot;
        private readonly IEnvironmentVariableContext environmentVariableContext;

        public MercurialSuite([SuiteRoot] IFileSystemDirectory suiteRoot, IEnvironmentVariableContext environmentVariableContext)
        {
            this.suiteRoot = suiteRoot;
            this.environmentVariableContext = environmentVariableContext;
        }

        public bool IsAvailable
        {
            get
            {
                try
                {
                    var localRoot = suiteRoot as LocalFileSystemDirectory;
                    // Do not initialize the Mercurial client for non-Mercurial suites.
                    if (localRoot != null && Directory.Exists(Path.Combine(localRoot.AbsolutePath, ".hg")))
                    {
                        if (Client.CouldLocateClient)
                        {
                            log.InfoFormat("Mercurial support initialized, client version {0}", Client.GetVersion());
                            return true;
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    log.WarnFormat("Could not initialize Mercurial support: {0}", ex.Message);
                }
                catch (ArgumentException ex)
                {
                    // Mercurial.Net copies the process environment into a case-insensitive
                    // dictionary, which rejects duplicate Windows keys such as Path/PATH.
                    log.WarnFormat("Could not initialize Mercurial support: {0}", ex.Message);
                }

                return false;
            }
        }

        public void AddEnvironmentVariables()
        {
            var localRoot = suiteRoot as LocalFileSystemDirectory;
            if (localRoot != null)
            {
                var logCommand = new LogCommand().WithRevision(RevSpec.WorkingDirectoryParent);
                Client.Execute(localRoot.AbsolutePath, logCommand);
                var changeSet = logCommand.Result.First();
                string revNo = changeSet.RevisionNumber.ToString(CultureInfo.InvariantCulture);
                log.DebugFormat("Mercurial current revision number is {0}", revNo);
                environmentVariableContext.Define("HG_REVNO", revNo);
            }
        }
    }
}