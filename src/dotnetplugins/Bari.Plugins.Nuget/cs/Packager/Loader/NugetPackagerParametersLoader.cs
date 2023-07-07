﻿using System;
using System.Collections.Generic;
using System.Text;
using Bari.Core.Generic;
using Bari.Core.Model;
using Bari.Core.Model.Loader;
using Bari.Core.UI;
using YamlDotNet.RepresentationModel;

namespace Bari.Plugins.Nuget.Packager.Loader
{
    public class NugetPackagerParametersLoader: YamlProjectParametersLoaderBase<NugetPackagerParameters>
    {
        private IEnvironmentVariableContext environmentVariableContext;

        public NugetPackagerParametersLoader(IUserOutput output, IEnvironmentVariableContext environmentVariableContext) : base(output)
        {
            this.environmentVariableContext = environmentVariableContext;
        }

        protected override string BlockName
        {
            get { return "nuget"; }
        }

        protected override NugetPackagerParameters CreateNewParameters(Suite suite)
        {
            return new NugetPackagerParameters();
        }

        protected override Dictionary<string, Action> GetActions(NugetPackagerParameters target, YamlNode value, YamlParser parser)
        {
            return new Dictionary<string, Action>
            {
                { "id", () => target.Id = ParseString(value)},
                { "title", () => target.Title = ParseString(value)},
                { "authors", () => target.Authors = ParseStringArray(parser, value) },
                { "author", () => target.Authors = new [] { ParseString(value)}},
                { "tags", () => target.Tags = ParseStringArray(parser, value)},
                { "description", () => target.Description = ParseString(value)},
                { "project-url", () => target.ProjectUrl = new Uri(ParseString(value))},
                { "license-url", () => target.LicenseUrl = new Uri(ParseString(value))},
                { "icon-url", () => target.IconUrl = new Uri(ParseString(value)) },
                { "package-as-tool", () => target.PackageAsTool = ParseBool(parser, value)},
                { "api-key", () => target.ApiKey = ParseApiKey(ParseString(value)) },
                { "package-reference", () => target.PackageReference = ParseBool(parser, value) },
            };
        }

        private string ParseApiKey(String apiKey)
        {
            var result = new StringBuilder(apiKey);
            EnvironmentVariables.ResolveEnvironmentVariables(environmentVariableContext, result);

            return result.ToString();
        }
    }
}