using System;
using System.Text.Json.Serialization;
using Windows.ApplicationModel;

namespace ViewAppxPackage
{
    /// <summary>
    /// Simplified model for JSON serialization of package data
    /// </summary>
    public class PackageJsonModel
    {
        public PackageJsonModel(Package package)
        {
            if (package == null)
                throw new ArgumentNullException(nameof(package));

            Name = package.Id.Name;
            FullName = package.Id.FullName;
            FamilyName = package.Id.FamilyName;
            Publisher = package.Id.Publisher;
            PublisherDisplayName = package.PublisherDisplayName;
            InstalledDate = package.InstalledDate;
            Architecture = package.Id.Architecture.ToString();
            Version = $"{package.Id.Version.Major}.{package.Id.Version.Minor}.{package.Id.Version.Build}.{package.Id.Version.Revision}";
            IsFramework = package.IsFramework;
            IsResourcePackage = package.IsResourcePackage;
            IsBundle = package.IsBundle;
            IsDevelopmentMode = package.IsDevelopmentMode;
            InstalledPath = package.InstalledPath;
        }

        [JsonPropertyName("name")]
        public string Name { get; }

        [JsonPropertyName("fullName")]
        public string FullName { get; }

        [JsonPropertyName("familyName")]
        public string FamilyName { get; }

        [JsonPropertyName("publisher")]
        public string Publisher { get; }

        [JsonPropertyName("publisherDisplayName")]
        public string PublisherDisplayName { get; }

        [JsonPropertyName("installedDate")]
        public DateTimeOffset InstalledDate { get; }

        [JsonPropertyName("architecture")]
        public string Architecture { get; }

        [JsonPropertyName("version")]
        public string Version { get; }

        [JsonPropertyName("isFramework")]
        public bool IsFramework { get; }

        [JsonPropertyName("isResourcePackage")]
        public bool IsResourcePackage { get; }

        [JsonPropertyName("isBundle")]
        public bool IsBundle { get; }

        [JsonPropertyName("isDevelopmentMode")]
        public bool IsDevelopmentMode { get; }

        [JsonPropertyName("installedPath")]
        public string InstalledPath { get; }
    }
}