using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
internal class Build : NukeBuild
{
    public static int Main()
    {
        return Execute<Build>(x => x.CompileUniversalWindowsApp);
    }

    [Parameter] readonly string AppDisplayName = "FS Клиент";

    [Parameter] readonly string BundlePlatforms = "x86|x64|ARM";

    [Parameter] readonly string Configuration = "Release";

    [Parameter] readonly string AppInstallerUri = "https://fsclient.github.io/fs/";

    [Parameter] readonly bool GenerateAppInstallerFile = false;

    [Parameter] readonly bool IsDevBuild = true;

    AbsolutePath RepositoryDirectory => RootDirectory.ToString().EndsWith("build", StringComparison.Ordinal)
        ? RootDirectory.Parent
        : RootDirectory;
    AbsolutePath SourceDirectory => RepositoryDirectory / "src";
    AbsolutePath ArtifactsDirectory => RepositoryDirectory / "artifacts";

    AbsolutePath StartupProject => SourceDirectory.GlobFiles($"**/FSClient.UWP.Startup.*proj").First();

    Target PrintParameters => _ => _
        .Executes(() =>
        {
            Logger.Info($"{nameof(AppDisplayName)}: {AppDisplayName}");
            Logger.Info($"{nameof(BundlePlatforms)}: {BundlePlatforms}");
            Logger.Info($"{nameof(Configuration)}: {Configuration}");
            Logger.Info($"{nameof(AppInstallerUri)}: {AppInstallerUri}");
            Logger.Info($"{nameof(GenerateAppInstallerFile)}: {GenerateAppInstallerFile}");
            Logger.Info($"{nameof(IsDevBuild)}: {IsDevBuild}");
        });

    Target Clean => _ => _
        .Executes(() => SourceDirectory
            .GlobDirectories("**/bin", "**/obj", "**/AppPackages", "**/BundleArtifacts")
            .ForEach(DeleteDirectory));

    Target CreateSecrets => _ => _
        .Executes(() =>
        {
            Base64ToFile(
                Environment.GetEnvironmentVariable("SECRETS_CS_BASE64"),
                SourceDirectory / "FSClient.Shared" / "Secrets.Ignore.cs");

            static void Base64ToFile(string? base64String, AbsolutePath filePath)
            {
                if (File.Exists(filePath.ToString()))
                {
                    Logger.Info($"File {filePath} already existed.");
                    return;
                }
                if (base64String == null)
                {
                    Logger.Warn($"Missing environment variable for {filePath} file content.");
                    return;
                }
                Span<byte> span = stackalloc byte[base64String.Length];
                if (!Convert.TryFromBase64String(base64String, span, out var bytesWritten))
                {
                    Logger.Warn($"Invalid Base64 content for {filePath} file.");
                    return;
                }
                using var file = File.Create(filePath.ToString());
                file.Write(span.Slice(0, bytesWritten));
                Logger.Info($"File {filePath} was created.");
            }
        });

    Target InstallPfxCertificate => _ => _
        .Executes(() =>
        {
            if (!(Environment.GetEnvironmentVariable("UWP_STORE_KEY_PFX_PASSWORD") is string certPassword))
            {
                Logger.Info($"Certificate password is not specified.");
                return;
            }

            var filePath = RepositoryDirectory / "build" / "FSClient_StoreKey.pfx";
            if (!File.Exists(filePath.ToString()))
            {
                Logger.Info($"File {filePath} is missed.");
                return;
            }

            using var cert = new X509Certificate2(filePath.ToString(), certPassword, X509KeyStorageFlags.PersistKeySet);
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
            store.Close();

            Logger.Info("Certificate was installed for current user.");
        });


    Target UpdateAppxManifest => _ => _
        .Executes(() =>
        {
            var manifestFile = SourceDirectory.GlobFiles($"**/*.*appxmanifest").First();
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(manifestFile);
            var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("desktop", "http://schemas.microsoft.com/appx/manifest/desktop/windows10");
            nsmgr.AddNamespace("rescap", "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");

            var displayName = AppDisplayName;
            if (!IsDevBuild)
            {
                var identity = xmlDoc.SelectSingleNode("/ns:Package/ns:Identity", nsmgr)!;
                if (Environment.GetEnvironmentVariable("APPXMANIFEST_PACKAGE_NAME") is string packageName
                    && Environment.GetEnvironmentVariable("APPXMANIFEST_PUBLISHER") is string publisher)
                {
                    identity.Attributes!["Name"]!.Value = packageName;
                    identity.Attributes["Publisher"]!.Value = publisher;
                }
                var assemblyVersion = new Version(ThisAssembly.AssemblyFileVersion);
                identity.Attributes!["Version"]!.Value = assemblyVersion.ToString(3) + ".0";

                var extensionProtocol = xmlDoc.SelectSingleNode("/ns:Package/ns:Applications/ns:Application/ns:Extensions/uap:Extension[@Category='windows.protocol']/uap:Protocol", nsmgr);
                if (extensionProtocol != null)
                {
                    extensionProtocol.Attributes!["Name"]!.Value = "fsclient";
                }
            }
            else
            {
                displayName += " DEV";
            }

            var displayNameNode = xmlDoc.SelectSingleNode("/ns:Package/ns:Properties/ns:DisplayName", nsmgr)!;
            displayNameNode.InnerText = displayName;

            var visualElements = xmlDoc.SelectSingleNode("/ns:Package/ns:Applications/ns:Application/uap:VisualElements", nsmgr)!;
            visualElements.Attributes!["DisplayName"]!.Value = displayName;

            var defaultTile = xmlDoc.SelectSingleNode("/ns:Package/ns:Applications/ns:Application/uap:VisualElements/uap:DefaultTile", nsmgr)!;
            defaultTile.Attributes!["ShortName"]!.Value = displayName;

            xmlDoc.Save(manifestFile);
        });

    Target CompileUniversalWindowsApp => __ => __
        .DependsOn(PrintParameters, Clean, CreateSecrets, InstallPfxCertificate, UpdateAppxManifest)
        .Executes(() =>
        {
            if (!EnvironmentInfo.IsWin)
            {
                Logger.Info($"Should compile for Universal Windows.");
                return;
            }

            Logger.Normal("Restoring packages required by UAP...");

            _ = MSBuild(settings => settings
                .SetMSBuildVersion(MSBuildVersion.VS2019)
                .SetMSBuildPlatform(MSBuildPlatform.x86)
                .SetProjectFile(StartupProject)
                .SetConfiguration(Configuration)
                .SetTargets("Restore"));

            Logger.Success($"Successfully restored UAP packages for {Configuration}.");

            var uwpStoreKeyThumbprint = Environment.GetEnvironmentVariable("UWP_STORE_KEY_THUMBPRINT") ?? string.Empty;

            BundlePlatforms.Split("|")
                .Select(platform => platform switch
                {
                    "ARM" => MSBuildTargetPlatform.arm,
                    "x86" => MSBuildTargetPlatform.x86,
                    "x64" => MSBuildTargetPlatform.x64,
                    _ => throw new NotSupportedException(platform)
                })
                .ForEach(BuildApp);

            void BuildApp(MSBuildTargetPlatform platform)
            {
                Logger.Normal($"Building UAP project for {platform}...");

                _ = MSBuild(settings => settings
                    .SetMSBuildVersion(MSBuildVersion.VS2019)
                    .SetMSBuildPlatform(MSBuildPlatform.x86)
                    .SetProjectFile(StartupProject)
                    .SetTargets("Build")
                    .SetConfiguration(Configuration)
                    .SetTargetPlatform(platform)
                    .SetProperty("IsDevBuild", IsDevBuild)
                    .SetProperty("AppxSymbolPackageEnabled", true)
                    .SetProperty("AppxPackageSigningEnabled", true)
                    .SetProperty("PackageCertificateThumbprint", uwpStoreKeyThumbprint)
                    .SetProperty("GenerateAppInstallerFile", GenerateAppInstallerFile)
                    .SetProperty("AppxAutoIncrementPackageRevision", false)
                    .SetProperty("AppxPackageDir", ArtifactsDirectory)
                    .SetProperty("AppxBundlePlatforms", BundlePlatforms)
                    .SetProperty("AppxBundle", "Always")
                    .SetProperty("UapAppxPackageBuildMode", "Sideload")
                    .SetProperty("AppInstallerCheckForUpdateFrequency", "Hourly")
                    .SetProperty("AppInstallerUpdateFrequency", "1")
                    .SetProperty("AppInstallerUri", AppInstallerUri)
                    .SetProperty("HoursBetweenUpdateChecks", "1"));

                Logger.Success($"Successfully built UAP project for {platform}.");
            }
        });
}
