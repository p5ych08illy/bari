﻿using System;
using Bari.Core.Model;
using Bari.Core.Model.Parameters;

namespace Bari.Plugins.Nuget.Packager
{
    public class NugetPackagerParameters: IPackagerParameters
    {
        private string id;
        private string title;
        private string[] authors;
        private string[] tags;
        private string description;
        private Uri projectUrl;
        private Uri licenseUrl;
        private Uri iconUrl;
        private bool packageAsTool;
        private string apiKey;
        private bool packageReference;

        public string Id
        {
            get { return id; }
            set { id = value; }
        }

        public string Title
        {
            get { return title; }
            set { title = value; }
        }

        public string[] Authors
        {
            get { return authors; }
            set { authors = value; }
        }

        public string[] Tags
        {
            get { return tags; }
            set { tags = value; }
        }

        public string Description
        {
            get { return description; }
            set { description = value; }
        }

        public Uri ProjectUrl
        {
            get { return projectUrl; }
            set { projectUrl = value; }
        }

        public Uri LicenseUrl
        {
            get { return licenseUrl; }
            set { licenseUrl = value; }
        }

        public Uri IconUrl
        {
            get { return iconUrl; }
            set { iconUrl = value; }
        }

        public bool PackageAsTool
        {
            get { return packageAsTool; }
            set { packageAsTool = value; }
        }

        public string ApiKey
        {
            get { return apiKey; }
            set { apiKey = value; }
        }

        public bool PackageReference
        {
            get { return packageReference; }
            set { packageReference = value; }
        }
    }
}