using Bari.Core.Model.Parameters;

namespace Bari.Plugins.Nuget.Model
{
    /// <summary>
    /// Model of the <c>nupkg</c> block of the suite definition, the configuration of the
    /// <c>bari nupkg</c> command.
    ///
    /// <para>There is deliberately no <c>enabled</c> key: if the command is invoked, it runs. And
    /// deliberately no <c>publish</c> key either: whether a run publishes is a property of the
    /// pipeline that invokes it, not of the suite, so it lives on the command line.</para>
    ///
    /// <para>There is no credential of any kind here. Pushing relies on the machine being
    /// authenticated to the feed already - interactively through the NuGet credential provider on a
    /// developer machine, or through a <c>NuGetAuthenticate</c> step on a build agent. See the
    /// <c>Help</c> of the command.</para>
    /// </summary>
    public class NupkgParameters : IProjectParameters
    {
        private string output;
        private string tfm;
        private string feed;
        private bool skipDuplicate;
        private bool strictInputs;

        /// <summary>
        /// Creates the parameter block with its default values
        /// </summary>
        public NupkgParameters()
        {
            output = "nupkg";

            // Not `net10.0-windows`: `nuget pack` rejects a lib\<tfm> folder whose platform has no
            // version with NU1012. This is the normalized form MSBuild would produce for it anyway.
            tfm = "net10.0-windows7.0";

            feed = null;
            skipDuplicate = true;
            strictInputs = true;
        }

        /// <summary>
        /// Target-root relative output directory of the generated <c>.nupkg</c> files
        /// </summary>
        public string Output
        {
            get { return output; }
            set { output = value; }
        }

        /// <summary>
        /// Value of the <c>$tfm$</c> nuspec token
        /// </summary>
        public string Tfm
        {
            get { return tfm; }
            set { tfm = value; }
        }

        /// <summary>
        /// URL of the NuGet feed the packages are pushed to
        /// </summary>
        public string Feed
        {
            get { return feed; }
            set { feed = value; }
        }

        /// <summary>
        /// If <c>true</c>, <c>-SkipDuplicate</c> is passed to <c>nuget push</c>
        /// </summary>
        public bool SkipDuplicate
        {
            get { return skipDuplicate; }
            set { skipDuplicate = value; }
        }

        /// <summary>
        /// If <c>true</c>, a nuspec referring to files which do not exist fails the command.
        /// Otherwise such a package is only warned about and skipped.
        /// </summary>
        public bool StrictInputs
        {
            get { return strictInputs; }
            set { strictInputs = value; }
        }
    }
}
