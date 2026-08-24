using System;
using System.Collections.Generic;
using System.Text;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Core.Model.Loader;
using Bari.Core.UI;
using YamlDotNet.RepresentationModel;

namespace Bari.Plugins.Nuget.Model.Loader
{
    /// <summary>
    /// Loads the <c>nupkg</c> block of the suite definition into <see cref="NupkgParameters"/>
    ///
    /// <para>The block cannot be called <c>nuget</c>: that name is already taken by
    /// <see cref="Bari.Plugins.Nuget.Packager.Loader.NugetPackagerParametersLoader"/>, and the model
    /// loader offers every key of the suite root to every registered loader.</para>
    /// </summary>
    public class NupkgParametersLoader : YamlProjectParametersLoaderBase<NupkgParameters>
    {
        private readonly IEnvironmentVariableContext environmentVariableContext;

        public NupkgParametersLoader(IUserOutput output, IEnvironmentVariableContext environmentVariableContext)
            : base(output)
        {
            this.environmentVariableContext = environmentVariableContext;
        }

        /// <summary>
        /// Gets the name of the yaml block the loader supports
        /// </summary>
        protected override string BlockName
        {
            get { return "nupkg"; }
        }

        /// <summary>
        /// Creates a new instance of the parameter model type
        /// </summary>
        /// <param name="suite">Current suite</param>
        /// <returns>Returns the new instance to be filled with loaded data</returns>
        protected override NupkgParameters CreateNewParameters(Suite suite)
        {
            return new NupkgParameters();
        }

        /// <summary>
        /// Gets the mapping table of the supported option keys
        /// </summary>
        /// <remarks>
        /// Only the hyphenated form has to be listed: the base class matches the hyphen-stripped
        /// variant as well, so both <c>api-key</c> and <c>apikey</c> are accepted.
        /// </remarks>
        protected override Dictionary<string, Action> GetActions(NupkgParameters target, YamlNode value, YamlParser parser)
        {
            return new Dictionary<string, Action>
            {
                { "output", () => target.Output = ParseString(value) },
                { "tfm", () => target.Tfm = ParseString(value) },
                { "feed", () => target.Feed = Resolve(ParseString(value)) },
                { "skip-duplicate", () => target.SkipDuplicate = ParseBool(parser, value) },
                { "strict-inputs", () => target.StrictInputs = ParseBool(parser, value) },
            };
        }

        /// <summary>
        /// Resolves the <c>$VARIABLE</c> references of a value to environment variables
        /// </summary>
        private string Resolve(string text)
        {
            if (String.IsNullOrEmpty(text))
                return text;

            var result = new StringBuilder(text);
            EnvironmentVariables.ResolveEnvironmentVariables(environmentVariableContext, result);

            return result.ToString();
        }
    }
}
