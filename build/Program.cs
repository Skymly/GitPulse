using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tooling;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[UnsetVisualStudioEnvironmentVariables]
sealed class Build : NukeBuild
{
    /// <summary>
    ///   Build configuration: Debug locally, Release on CI.
    /// </summary>
    [Parameter("Build configuration (Debug/Release)")]
    readonly string Configuration = IsLocalBuild ? "Debug" : "Release";

    /// <summary>
    ///   Version override. When set, passed to publish as -p:Version.
    ///   When null, MinVer derives the version from the latest git tag.
    /// </summary>
    [Parameter("Version override (defaults to MinVer git-tag-based version)")]
    readonly string? Version = Environment.GetEnvironmentVariable("VERSION");

    /// <summary>
    ///   Target runtime for self-contained publish (default: win-x64).
    /// </summary>
    [Parameter("Target runtime identifier for publish (e.g. win-x64, win-arm64)")]
    readonly string Runtime = "win-x64";

    /// <summary>
    ///   Target framework for MAUI Windows publish.
    /// </summary>
    [Parameter("MAUI Windows target framework moniker for publish")]
    readonly string WindowsFramework = "net10.0-windows10.0.19041.0";

    /// <summary>
    ///   Target framework for MAUI Android compile gate (ADR-011 / M11).
    /// </summary>
    [Parameter("MAUI Android target framework moniker")]
    readonly string AndroidFramework = "net10.0-android";

    /// <summary>
    ///   Base64-encoded Android keystore (secret). Decoded to a temp file by
    ///   PublishAndroid and deleted after use; never committed (ADR-012
    ///   secrets contract, docs/DEVELOPMENT.md).
    /// </summary>
    [Parameter("Base64-encoded Android keystore (secret; env ANDROID_KEYSTORE_BASE64)")]
    [Secret]
    readonly string? AndroidKeystoreBase64 = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_BASE64");

    /// <summary>
    ///   Android keystore password (secret).
    /// </summary>
    [Parameter("Android keystore password (secret; env ANDROID_KEYSTORE_PASSWORD)")]
    [Secret]
    readonly string? AndroidKeystorePassword = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASSWORD");

    /// <summary>
    ///   Android key alias (secret).
    /// </summary>
    [Parameter("Android key alias (secret; env ANDROID_KEY_ALIAS)")]
    [Secret]
    readonly string? AndroidKeyAlias = Environment.GetEnvironmentVariable("ANDROID_KEY_ALIAS");

    /// <summary>
    ///   Android key password (secret).
    /// </summary>
    [Parameter("Android key password (secret; env ANDROID_KEY_PASSWORD)")]
    [Secret]
    readonly string? AndroidKeyPassword = Environment.GetEnvironmentVariable("ANDROID_KEY_PASSWORD");

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "GitPulse.slnx";
    AbsolutePath AppProject => Root / "src" / "GitPulse.App" / "GitPulse.App.csproj";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath ArtifactsDirectory => Root / "artifacts";
    AbsolutePath PublishDirectory => ArtifactsDirectory / "publish" / Runtime;
    AbsolutePath AndroidPublishDirectory => ArtifactsDirectory / "publish" / "android";

    /// <summary>
    ///   Windows Release Artifact: zip of the full self-contained publish folder (ADR-012).
    /// </summary>
    AbsolutePath PublishZipFile => ArtifactsDirectory / $"GitPulse-{Runtime}.zip";

    /// <summary>
    ///   Android Release Artifact: CI-signed APK (ADR-012).
    /// </summary>
    AbsolutePath SignedApkFile => ArtifactsDirectory / "GitPulse-android.apk";

    static readonly string[] TestProjectRelativePaths =
    [
        "tests/GitPulse.Tests/GitPulse.Tests.csproj",
    ];

    public static int Main() => Execute<Build>(x => x.Ci);

    Target Clean => _ => _
        .Executes(() =>
        {
            if (TestResultsDirectory.DirectoryExists())
            {
                TestResultsDirectory.DeleteDirectory();
            }

            TestResultsDirectory.CreateDirectory();

            if (ArtifactsDirectory.DirectoryExists())
            {
                ArtifactsDirectory.DeleteDirectory();
            }
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(SolutionFile));
        });

    /// <summary>
    ///   Compiles the MAUI App for Android only (no Windows TFM, no tests).
    ///   Used as the M11 Android compile gate (ADR-011); also invoked from Compile.
    ///   Skips APK packaging — the signed APK Release Artifact is PublishAndroid (ADR-012 / M12).
    /// </summary>
    Target CompileAndroid => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            // No RID: Mono runtime comes from the MAUI workload. Do not pass a
            // RID here — it triggers NU1102 Mono runtime pack resolution issues.
            // Prefer apk-only packaging for the compile gate (Release defaults to
            // aab;apk). Signed APK distribution is PublishAndroid (ADR-012).
            DotNetBuild(s => s
                .SetProjectFile(AppProject)
                .SetConfiguration(Configuration)
                .SetFramework(AndroidFramework)
                .SetProperty("AndroidPackageFormats", "apk")
                .SetProperty("AndroidBuildApplicationPackage", "false"));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .DependsOn(CompileAndroid)
        .Executes(() =>
        {
            // Build library projects + tests via the test project (which
            // transitively builds Core/GitHubApi/Services). This avoids
            // building the App project which needs special runtime handling.
            foreach (string relativePath in TestProjectRelativePaths)
            {
                DotNetBuild(s => s
                    .SetProjectFile(Root / relativePath)
                    .SetConfiguration(Configuration)
                    .EnableNoRestore());
            }

            // Build the App project's Windows target. Building with an explicit
            // RID triggers Mono runtime pack resolution for the Android TFM
            // (NU1102), so we build without a RID. The .NET runtime pack for
            // win-x64 is provided by the installed SDK on Windows.
            DotNetBuild(s => s
                .SetProjectFile(AppProject)
                .SetConfiguration(Configuration)
                .SetFramework(WindowsFramework));
        });

    Target UnitTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            foreach (string relativePath in TestProjectRelativePaths)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    throw new InvalidOperationException($"Test project not found: {projectFile}");
                }

                DotNetTest(s => s
                    .SetProjectFile(projectFile)
                    .SetConfiguration(Configuration)
                    .SetNoBuild(true)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers("trx;LogFileName=" + projectFile.NameWithoutExtension + ".trx")
                    .SetDataCollector("XPlat Code Coverage"));
            }
        });

    /// <summary>
    ///   Cross-platform library test target. Builds and tests only the test
    ///   project (which transitively builds Core/GitHubApi/Services but NOT
    ///   the platform-specific App project). Safe to run on Linux/macOS.
    /// </summary>
    Target UnitTestLib => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            foreach (string relativePath in TestProjectRelativePaths)
            {
                AbsolutePath projectFile = Root / relativePath;
                if (!projectFile.FileExists())
                {
                    throw new InvalidOperationException($"Test project not found: {projectFile}");
                }

                DotNetTest(s => s
                    .SetProjectFile(projectFile)
                    .SetConfiguration(Configuration)
                    .SetResultsDirectory(TestResultsDirectory)
                    .SetLoggers("trx;LogFileName=" + projectFile.NameWithoutExtension + ".trx")
                    .SetDataCollector("XPlat Code Coverage"));
            }
        });

    /// <summary>
    ///   Convenience alias for UnitTest.
    /// </summary>
    Target Test => _ => _
        .DependsOn(UnitTest);

    Target Format => _ => _
        .Executes(() =>
        {
            DotNet($"format \"{SolutionFile}\" --verify-no-changes --verbosity diagnostic");
        });

    Target FormatFix => _ => _
        .Executes(() =>
        {
            DotNet($"format \"{SolutionFile}\" --verbosity normal");
        });

    /// <summary>
    ///   Publishes the MAUI Windows app (unpackaged, self-contained) and zips
    ///   the full publish folder as the Windows Release Artifact (ADR-012 / #56).
    ///   Output: artifacts/publish/{Runtime}/ and artifacts/GitPulse-{Runtime}.zip
    /// </summary>
    Target Publish => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            PublishDirectory.CreateOrCleanDirectory();

            // Restore without a RID first so library assets keep net10.0 targets.
            // Then publish with Windows-only TFMs + RID and --no-restore: a single
            // publish restore with RID hits NU1102 (Android Mono.win-x64 pack).
            DotNetRestore(s => s.SetProjectFile(SolutionFile));

            DotNetPublish(s => ApplyVersionOverride(s
                .SetProject(AppProject)
                .SetConfiguration(Configuration)
                .SetFramework(WindowsFramework)
                .SetRuntime(Runtime)
                .SetSelfContained(true)
                .SetOutput(PublishDirectory)
                .SetNoRestore(true)
                .SetProperty("TargetFrameworks", WindowsFramework)
                .SetProperty("WindowsPackageType", "None")));

            if (PublishZipFile.FileExists())
            {
                PublishZipFile.DeleteFile();
            }

            PublishDirectory.ZipTo(PublishZipFile);
            Console.WriteLine($"Publish zip created: {PublishZipFile}");
        });

    /// <summary>
    ///   Verifies the published Windows entry point and publish-folder zip
    ///   Release Artifact (ADR-012 / #56).
    /// </summary>
    Target PublishVerify => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            AbsolutePath exeApp = PublishDirectory / "GitPulse.App.exe";
            AbsolutePath dllApp = PublishDirectory / "GitPulse.App.dll";
            AbsolutePath exe = PublishDirectory / "GitPulse.exe";
            AbsolutePath dll = PublishDirectory / "GitPulse.dll";

            AbsolutePath entryPoint =
                exeApp.FileExists() ? exeApp :
                dllApp.FileExists() ? dllApp :
                exe.FileExists() ? exe :
                dll;

            Assert.FileExists(entryPoint,
                $"Published entry point not found. Expected GitPulse.App.exe/dll or GitPulse.exe/dll in {PublishDirectory}");

            var sizeMb = new FileInfo(entryPoint).Length / (1024.0 * 1024.0);
            Console.WriteLine($"Publish verified: {entryPoint.Name} ({sizeMb:F1} MB) at {PublishDirectory}");

            Assert.FileExists(PublishZipFile,
                $"Windows Release Artifact zip not found. Expected {PublishZipFile}");

            string[] publishFiles = Directory.GetFiles(PublishDirectory, "*", SearchOption.AllDirectories);
            Assert.True(publishFiles.Length > 0,
                $"Publish directory is empty; cannot form a publish-folder zip. Path: {PublishDirectory}");

            using ZipArchive archive = ZipFile.OpenRead(PublishZipFile);
            ZipArchiveEntry[] fileEntries = archive.Entries
                .Where(static e => !string.IsNullOrEmpty(e.Name))
                .ToArray();

            bool hasEntryPoint = fileEntries.Any(static e =>
            {
                string name = Path.GetFileName(e.FullName.Replace('\\', '/'));
                return name.Equals("GitPulse.App.exe", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("GitPulse.App.dll", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("GitPulse.exe", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("GitPulse.dll", StringComparison.OrdinalIgnoreCase);
            });

            Assert.True(hasEntryPoint,
                $"Windows Release Artifact zip has wrong shape. Expected GitPulse.App.exe/dll (or GitPulse.exe/dll) inside {PublishZipFile}");

            Assert.True(fileEntries.Length >= publishFiles.Length,
                $"Windows Release Artifact zip has wrong shape. Expected at least {publishFiles.Length} files from {PublishDirectory}, found {fileEntries.Length} in {PublishZipFile}");

            foreach (string publishFile in publishFiles)
            {
                string relative = Path.GetRelativePath(PublishDirectory, publishFile).Replace('\\', '/');
                bool present = fileEntries.Any(e =>
                    e.FullName.Replace('\\', '/').Equals(relative, StringComparison.OrdinalIgnoreCase));
                Assert.True(present,
                    $"Windows Release Artifact zip has wrong shape. Missing publish file '{relative}' in {PublishZipFile}");
            }

            var zipMb = new FileInfo(PublishZipFile).Length / (1024.0 * 1024.0);
            Console.WriteLine($"Publish zip verified: {PublishZipFile.Name} ({zipMb:F1} MB, {fileEntries.Length} files)");
        });

    /// <summary>
    ///   Publishes the MAUI Android app as the CI-signed APK Release Artifact
    ///   (ADR-012 / #57). All four ANDROID_* secrets must be present (contract:
    ///   docs/DEVELOPMENT.md); the target fails fast when any is missing so a
    ///   tag push can never attach an unsigned APK or ship a half-empty Release.
    ///   No AAB. Output: artifacts/GitPulse-android.apk
    /// </summary>
    Target PublishAndroid => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            string keystoreBase64 = RequireAndroidSecret(AndroidKeystoreBase64, "ANDROID_KEYSTORE_BASE64");
            string storePassword = RequireAndroidSecret(AndroidKeystorePassword, "ANDROID_KEYSTORE_PASSWORD");
            string keyAlias = RequireAndroidSecret(AndroidKeyAlias, "ANDROID_KEY_ALIAS");
            string keyPassword = RequireAndroidSecret(AndroidKeyPassword, "ANDROID_KEY_PASSWORD");

            // The keystore lives outside the repo tree and is deleted in the
            // finally block. Passwords reach MSBuild as environment-variable
            // properties (picked up implicitly), so no secret ever appears on
            // the dotnet command line echoed to build logs.
            string keystoreFile = Path.Combine(Path.GetTempPath(), $"gitpulse-{Guid.NewGuid():N}.keystore");
            try
            {
                File.WriteAllBytes(keystoreFile, DecodeKeystore(keystoreBase64));

                Environment.SetEnvironmentVariable("AndroidSigningStorePass", storePassword);
                Environment.SetEnvironmentVariable("AndroidSigningKeyAlias", keyAlias);
                Environment.SetEnvironmentVariable("AndroidSigningKeyPass", keyPassword);

                AndroidPublishDirectory.CreateOrCleanDirectory();

                // No RID (same NU1102 constraint as CompileAndroid); the MAUI
                // workload supplies the Android runtime packs.
                DotNetPublish(s => ApplyVersionOverride(s
                    .SetProject(AppProject)
                    .SetConfiguration(Configuration)
                    .SetFramework(AndroidFramework)
                    .SetProperty("AndroidPackageFormats", "apk")
                    .SetProperty("AndroidSignPackage", "true")
                    .SetProperty("AndroidSigningKeyStore", keystoreFile)
                    .SetOutput(AndroidPublishDirectory)));

                // The Android build emits '<ApplicationId>-Signed.apk' only when
                // signing succeeded; an unsigned package lacks the suffix.
                var signedApks = AndroidPublishDirectory.GlobFiles("*-Signed.apk");
                if (signedApks.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected exactly one '*-Signed.apk' in {AndroidPublishDirectory}, found " +
                        $"{signedApks.Count}. Missing output means the signing configuration failed " +
                        "and the release must not continue (ADR-012).");
                }

                signedApks.First().Copy(SignedApkFile, ExistsPolicy.FileOverwrite);
                Console.WriteLine($"Signed APK created: {SignedApkFile}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("AndroidSigningStorePass", null);
                Environment.SetEnvironmentVariable("AndroidSigningKeyAlias", null);
                Environment.SetEnvironmentVariable("AndroidSigningKeyPass", null);

                if (File.Exists(keystoreFile))
                {
                    File.Delete(keystoreFile);
                }
            }
        });

    /// <summary>
    ///   Verifies the CI-signed Android APK Release Artifact (ADR-012 / #57):
    ///   exists, is a valid APK zip containing classes*.dex, and carries an
    ///   APK Signature Scheme v2+ block. Fails the release when the signed
    ///   APK is missing or not actually signed.
    /// </summary>
    Target PublishAndroidVerify => _ => _
        .DependsOn(PublishAndroid)
        .Executes(() =>
        {
            Assert.FileExists(SignedApkFile,
                $"Android Release Artifact not found. Expected {SignedApkFile}");

            using (ZipArchive archive = ZipFile.OpenRead(SignedApkFile))
            {
                bool hasDex = archive.Entries.Any(static e =>
                    e.FullName.StartsWith("classes", StringComparison.Ordinal)
                    && e.FullName.EndsWith(".dex", StringComparison.Ordinal));
                Assert.True(hasDex,
                    $"Android Release Artifact has wrong shape. Expected classes*.dex inside {SignedApkFile}");
            }

            Assert.True(HasApkSignatureBlock(SignedApkFile),
                $"Android Release Artifact is not signed (no APK Signature Scheme v2+ block): {SignedApkFile}");

            var sizeMb = new FileInfo(SignedApkFile).Length / (1024.0 * 1024.0);
            Console.WriteLine($"Signed APK verified: {SignedApkFile.Name} ({sizeMb:F1} MB)");
        });

    DotNetPublishSettings ApplyVersionOverride(DotNetPublishSettings settings)
        => string.IsNullOrWhiteSpace(Version)
            ? settings
            : settings.SetProperty("Version", Version);

    static string RequireAndroidSecret(string? value, string environmentVariable)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException(
            $"Android signing secret '{environmentVariable}' is missing or empty. Configure the four " +
            "ANDROID_* GitHub Secrets per the contract in docs/DEVELOPMENT.md (ADR-012); locally, set " +
            "the same environment variables from the offline keystore backup. The release fails closed: " +
            "an unsigned APK must never ship.");
    }

    static byte[] DecodeKeystore(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "ANDROID_KEYSTORE_BASE64 is not valid Base64. Re-encode the keystore (e.g. " +
                "[Convert]::ToBase64String([IO.File]::ReadAllBytes('gitpulse.keystore'))) and update " +
                "the GitHub Secret.", exception);
        }
    }

    /// <summary>
    ///   Detects an APK Signature Scheme v2+ block: a 16-byte "APK Sig Block 42"
    ///   magic immediately before the zip Central Directory. JAR (v1) signatures
    ///   alone do not satisfy the CI-signed contract.
    /// </summary>
    static bool HasApkSignatureBlock(AbsolutePath apkFile)
    {
        // End Of Central Directory record layout (fixed part, then comment).
        const int EocdFixedSize = 22;
        const int EocdMaxCommentSize = 65535;
        const int EocdCentralDirectoryOffsetPosition = 16;
        const int EocdCommentLengthPosition = 20;

        // APK Signing Block footer: 8-byte size + 16-byte magic.
        const int SigningBlockFooterSize = 24;
        const int SigningBlockMagicPosition = 8;

        ReadOnlySpan<byte> eocdSignature = [0x50, 0x4B, 0x05, 0x06];

        using FileStream stream = File.OpenRead(apkFile);
        if (stream.Length < EocdFixedSize)
        {
            return false;
        }

        // The EOCD record sits within the last 22 + 65535 bytes (fixed record
        // plus optional comment).
        int tailLength = (int)Math.Min(stream.Length, EocdFixedSize + EocdMaxCommentSize);
        byte[] tail = new byte[tailLength];
        stream.Seek(-tailLength, SeekOrigin.End);
        stream.ReadExactly(tail);

        int eocdIndex = -1;
        for (int i = tail.Length - EocdFixedSize; i >= 0; i--)
        {
            if (tail.AsSpan(i, eocdSignature.Length).SequenceEqual(eocdSignature)
                && i + EocdFixedSize + BitConverter.ToUInt16(tail, i + EocdCommentLengthPosition) == tail.Length)
            {
                eocdIndex = i;
                break;
            }
        }

        if (eocdIndex < 0)
        {
            return false;
        }

        long centralDirectoryOffset = BitConverter.ToUInt32(tail, eocdIndex + EocdCentralDirectoryOffsetPosition);
        if (centralDirectoryOffset < SigningBlockFooterSize)
        {
            return false;
        }

        Span<byte> signingBlockFooter = stackalloc byte[SigningBlockFooterSize];
        stream.Seek(centralDirectoryOffset - SigningBlockFooterSize, SeekOrigin.Begin);
        stream.ReadExactly(signingBlockFooter);

        return signingBlockFooter[SigningBlockMagicPosition..].SequenceEqual("APK Sig Block 42"u8);
    }

    /// <summary>
    ///   CI entry point: Clean → Restore → Compile → UnitTest.
    /// </summary>
    Target Ci => _ => _
        .DependsOn(UnitTest)
        .Executes(() =>
        {
            Console.WriteLine("CI build completed successfully.");
        });

    /// <summary>
    ///   Cross-platform CI entry point: Clean → UnitTestLib (library tests
    ///   only, no App project). Safe for Linux/macOS runners.
    /// </summary>
    Target CiLib => _ => _
        .DependsOn(UnitTestLib)
        .Executes(() =>
        {
            Console.WriteLine("Cross-platform library CI completed successfully.");
        });

    /// <summary>
    ///   Android App compile gate (M11 / ADR-011): Clean → Restore → CompileAndroid.
    ///   Compile-only — no APK/AAB publish (that is M12). Requires MAUI workload.
    /// </summary>
    Target CiAndroid => _ => _
        .DependsOn(CompileAndroid)
        .Executes(() =>
        {
            Console.WriteLine($"Android compile gate ({AndroidFramework}) completed successfully.");
        });

    /// <summary>
    ///   Full local/CI verification: Format + Ci.
    ///   Ci → Compile already includes CompileAndroid (Android compile gate).
    /// </summary>
    Target CiAll => _ => _
        .DependsOn(Format)
        .DependsOn(Ci)
        .Executes(() =>
        {
            Console.WriteLine("Full verification (format + CI) completed successfully.");
        });

    /// <summary>
    ///   Full release pipeline: CiAll → PublishVerify (Windows zip) →
    ///   PublishAndroidVerify (CI-signed APK). Run on tag pushes (v*) or
    ///   manually with --target Release; requires the four ANDROID_* secrets
    ///   (docs/DEVELOPMENT.md / ADR-014), otherwise PublishAndroid fails by
    ///   design so a dual-artifact cut never ships a half-empty Release.
    ///   Android Emulator UI Smoke stays on the cut checklist — not a hard
    ///   dependency of this target.
    /// </summary>
    Target Release => _ => _
        .DependsOn(CiAll)
        .DependsOn(PublishVerify)
        .DependsOn(PublishAndroidVerify)
        .Executes(() =>
        {
            Console.WriteLine("Release pipeline completed successfully (Windows zip + signed Android APK).");
        });
}
