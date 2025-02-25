﻿using System;
using System.Collections.Generic;
using System.Linq;
using Bari.Core.Exceptions;
using Bari.Core.Model;
using Bari.Core.Model.Loader;
using Bari.Core.UI;
using Bari.Plugins.VsCore.Model;
using YamlDotNet.RepresentationModel;

namespace Bari.Plugins.Csharp.Model.Loader
{
    /// <summary>
    /// Loads <see cref="CsharpProjectParameters"/> parameter block from YAML files
    /// </summary>
    public class CsharpParametersLoader : YamlProjectParametersLoaderBase<CsharpProjectParameters>
    {
        public CsharpParametersLoader(IUserOutput output) : base(output)
        {
        }

        protected override string BlockName
        {
            get { return "csharp"; }
        }

        protected override CsharpProjectParameters CreateNewParameters(Suite suite)
        {
            return new CsharpProjectParameters(suite);
        }

        protected override Dictionary<string, Action> GetActions(CsharpProjectParameters target, YamlNode value, YamlParser parser)
        {
            return new Dictionary<string, Action>
                {
                    {"base-address", () => { target.BaseAddress = ParseUint32(value); }},
                    {"checked", () => { target.Checked = ParseBool(parser, value); }},
                    {"code-page", () => { target.CodePage = ParseString(value); }},
                    {"debug", () => { target.Debug = ParseDebugLevel(value); }},
                    {"defines", () => { target.Defines = ParseDefines(parser, value); }},
                    {"delay-sign", () => { target.DelaySign = ParseBool(parser, value); }},
                    {"doc-output", () => { target.DocOutput = ParseString(value); }},
                    {"file-align", () => { target.FileAlign = ParseUint32(value); }},
                    {"high-entropy-virtual-address-space", () => { target.HighEntropyVirtualAddressSpace = ParseBool(parser, value); }},
                    {"key-container", () => { target.KeyContainer = ParseString(value); }},
                    {"key-file", () => { target.KeyFile = ParseString(value); }},
                    {"language-version", () => { target.LanguageVersion = ParseString(value); }},
                    {"main-class", () => { target.MainClass = ParseString(value); }},
                    {"no-std-lib", () => { target.NoStdLib = ParseBool(parser, value); }},
                    {"suppressed-warnings", () => { target.SuppressedWarnings = ParseWarnings(parser, value); }},
                    {"no-win23-manifest", () => { target.NoWin32Manifest = ParseBool(parser, value); }},
                    {"optimize", () => { target.Optimize = ParseBool(parser, value); }},
                    {"platform", () => { target.Platform = ParsePlatform(value); }},
                    {"preferred-ui-lang", () => { target.PreferredUILang = ParseString(value); }},
                    {"subsystem-version", () => { target.SubsystemVersion = ParseString(value); }},
                    {"unsafe", () => { target.Unsafe = ParseBool(parser, value); }},
                    {"warning-level", () => { target.WarningLevel = ParseWarningLevel(value); }},
                    {"warnings-as-error", () => ParseWarningsAsError(parser, target, value) },
                    {"root-namespace", () => { target.RootNamespace = ParseString(value); }},
                    {"application-icon", () => { target.ApplicationIcon = ParseString(value); }},
                    {"target-framework-version", () => { target.TargetFrameworkVersion = ParseFrameworkVersion(ParseString(value)); }},
                    {"target-framework-profile", () => { target.TargetFrameworkProfile= ParseFrameworkProfile(ParseString(value)); }},
                    {"target-framework", () => ApplyFrameworkVersionAndProfile(target, ParseString(value))},
                    {"sdk", () => { target.SDK = ParseString(value); }},
                    {"use-wpf", () => { target.UseWPF = ParseBool(parser, value); }},
                    {"use-winforms", () => { target.UseWinForms = ParseBool(parser, value); }},
                    {"target-os", () => { target.TargetOS = ParseString(value); }},
                    {"self-contained", () => { target.SelfContained = ParseBool(parser, value); }},
                    {"grpc-services", () => { target.GrpcServices = ParsegRPC(parser, value); }},
                    {"links", () => { target.Links = ParseLinks(parser, value); }},
                    };
        }

        private void ApplyFrameworkVersionAndProfile(CsharpProjectParameters target, string value)
        {
            string[] parts = value.Split('-');
            if (parts.Length == 1)
                target.TargetFrameworkVersion = ParseFrameworkVersion(value);
            else
            {
                target.TargetFrameworkVersion = ParseFrameworkVersion(parts[0]);
                target.TargetFrameworkProfile = ParseFrameworkProfile(parts[1]);
            }
        }

        private FrameworkVersion ParseFrameworkVersion(string value)
        {
            switch (value.TrimStart('v'))
            {
                case "2.0": return FrameworkVersion.v20;
                case "3.0": return FrameworkVersion.v30;
                case "3.5": return FrameworkVersion.v35;
                case "4.0": return FrameworkVersion.v4;
                case "4.5": return FrameworkVersion.v45;
                case "4.5.1": return FrameworkVersion.v451;
                case "4.5.2": return FrameworkVersion.v452;
                case "4.6": return FrameworkVersion.v46;
                case "4.6.1": return FrameworkVersion.v461;
                case "4.6.2": return FrameworkVersion.v462;
                case "4.7": return FrameworkVersion.v47;
                case "4.7.1": return FrameworkVersion.v471;
                case "4.7.2": return FrameworkVersion.v472;
                case "4.8": return FrameworkVersion.v48;
                case "6.0": return FrameworkVersion.v6;
                case "7.0": return FrameworkVersion.v7;
                case "8.0": return FrameworkVersion.v8;
                default:
                    throw new InvalidSpecificationException(
                        String.Format("Invalid framework version: {0}. Must be '2.0', '3.0', '3.5', '4.0', '4.5', '4.5.1', '4.5.2', '4.6', '4.6.1', '4.6.2', '4.7', '4.7.1', '4.7.2', '4.8', '6.0', '7.0' or '8.0'", value));
            }
        }

        private FrameworkProfile ParseFrameworkProfile(string value)
        {
            switch (value)
            {
                case "client": return FrameworkProfile.Client;
                default: return FrameworkProfile.Default;
            }
        }

        private void ParseWarningsAsError(YamlParser parser, CsharpProjectParameters target, YamlNode value)
        {
            var seq = value as YamlSequenceNode;
            if (seq != null)
                target.SpecificWarningsAsError = ParseWarnings(parser, value);
            else
                target.AllWarningsAsError = ParseBool(parser, value);
        }

        private string[] ParseWarnings(YamlParser parser, YamlNode value)
        {
            var seq = value as YamlSequenceNode;
            if (seq != null)
                return parser.EnumerateNodesOf(seq).OfType<YamlScalarNode>().Select(childValue => childValue.Value).ToArray();
            else
                return new string[0];
        }

        private string[] ParseDefines(YamlParser parser, YamlNode value)
        {
            var seq = value as YamlSequenceNode;
            if (seq != null)
                return parser.EnumerateNodesOf(seq).OfType<YamlScalarNode>().Select(childValue => childValue.Value).ToArray();
            else
                return new string[0];
        }

        private WarningLevel ParseWarningLevel(YamlNode value)
        {
            var sval = ParseString(value).ToLowerInvariant();
            switch (sval)
            {
                case "0":
                case "off":
                    return WarningLevel.Off;
                case "1":
                    return WarningLevel.Level1;
                case "2":
                    return WarningLevel.Level2;
                case "3":
                    return WarningLevel.Level3;
                case "4":
                case "all":
                    return WarningLevel.All;
                default:
                    throw new InvalidSpecificationException(
                        String.Format("Invalid warning level: {0}. Must be 'off', '1', '2', '3' or 'all'", sval));
            }
        }

        private CLRPlatform ParsePlatform(YamlNode value)
        {
            var sval = ParseString(value).ToLowerInvariant();
            switch (sval)
            {
                case "anycpu":
                    return CLRPlatform.AnyCPU;
                case "anycpu-32bit-preferred":
                    return CLRPlatform.AnyCPU32BitPreferred;
                case "arm":
                    return CLRPlatform.ARM;
                case "x64":
                    return CLRPlatform.x64;
                case "x86":
                    return CLRPlatform.x86;
                case "itanium":
                    return CLRPlatform.Itanium;
                default:
                    throw new InvalidSpecificationException(
                        String.Format("Invalid CLR platform: {0}. Must be 'anycpu', 'anycpu-32bit-preferred', 'arm', 'x64', 'x86' or 'itanium'", sval));
            }
        }

        private DebugLevel ParseDebugLevel(YamlNode value)
        {
            var sval = ParseString(value).ToLowerInvariant();
            switch (sval)
            {
                case "none":
                    return DebugLevel.None;
                case "pdbonly":
                    return DebugLevel.PdbOnly;
                case "full":
                    return DebugLevel.Full;
                default:
                    throw new InvalidSpecificationException(
                        String.Format("Invalid debug level: {0}. Must be 'none', 'pdbonly' or 'full'", sval));
            }
        }

        private Tuple<string, string>[] ParseLinks(YamlParser parser, YamlNode node)
        {
            var seq = node as YamlSequenceNode;
            if (seq != null)
                return parser.EnumerateNodesOf(seq)
                             .OfType<YamlNode>()
                             .Select(childValue =>
                                {
                                    var mapping = childValue as YamlMappingNode;
                                    if (mapping != null)
                                    {
                                        if (mapping.Children.ContainsKey(new YamlScalarNode("include")) && mapping.Children.ContainsKey(new YamlScalarNode("link")))
                                        {
                                            var include = ((YamlScalarNode)mapping.Children[new YamlScalarNode("include")]).Value;
                                            var link = ((YamlScalarNode)mapping.Children[new YamlScalarNode("link")]).Value;
                                            return new Tuple<string, string>(include, link);
                                        }
                                    }
                                    return null;
                                })
                             .Where(x => x != null)
                             .ToArray();
            else
                return new Tuple<string, string>[0];
        }

        private Tuple<string, string>[] ParsegRPC(YamlParser parser, YamlNode node)
        {
            var seq = node as YamlSequenceNode;
            if (seq != null)
                return parser.EnumerateNodesOf(seq)
                             .OfType<YamlNode>()
                             .Select(childValue =>
                             {
                                 var mapping = childValue as YamlMappingNode;
                                 if (mapping != null)
                                 {
                                     if (mapping.Children.ContainsKey(new YamlScalarNode("proto-file")) && mapping.Children.ContainsKey(new YamlScalarNode("services")))
                                     {
                                         var include = ((YamlScalarNode)mapping.Children[new YamlScalarNode("proto-file")]).Value;
                                         var link = ((YamlScalarNode)mapping.Children[new YamlScalarNode("services")]).Value;
                                         return new Tuple<string, string>(include, link);
                                     }
                                 }
                                 return null;
                             })
                             .Where(x => x != null)
                             .ToArray();
            else
                return new Tuple<string, string>[0];
        }
    }
}