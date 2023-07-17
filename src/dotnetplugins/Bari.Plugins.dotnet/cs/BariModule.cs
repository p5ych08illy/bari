using Bari.Core.Commands.Test;
using Bari.Plugins.dotnet.Commands.Test;
using Ninject.Modules;
using Bari.Plugins.dotnet.Tools;

namespace Bari.Plugins.dotnet
{
    /// <summary>
    /// The module definition of this bari plugin
    /// </summary>
    public class BariModule : NinjectModule
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(BariModule));

        /// <summary>
        /// Loads the module into the kernel.
        /// </summary>
        public override void Load()
        {
            log.Info("dotnet plugin loaded");

            Bind<Idotnet>().To<Dotnet>();
            Bind<ITestRunner>().To<DotnetTestRunner>();
        }
    }
}
