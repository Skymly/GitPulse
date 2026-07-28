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

    AbsolutePath Root => RootDirectory;
    AbsolutePath SolutionFile => Root / "GitPulse.slnx";
    AbsolutePath AppProject => Root / "src" / "GitPulse.App" / "GitPulse.App.csproj";
    AbsolutePath TestResultsDirectory => Root / "TestResults";
    AbsolutePath ArtifactsDirectory => Root / "artifacts";
    AbsolutePath PublishDirectory => ArtifactsDirectory / "publish" / Runtime;

    /// <summary>
    ///   Windows Release Artifact: zip of the full self-contained publish folder (ADR-012).
    /// </summary>
    AbsolutePath PublishZipFile => ArtifactsDirectory / $"GitPulse-{Runtime}.zip";

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
    ///   Skips APK/AAB packaging — distribution is M12.
    /// </summary>
    Target CompileAndroid => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            // No RID: Mono runtime comes from the MAUI workload. Do not pass a
            // RID here — it triggers NU1102 Mono runtime pack resolution issues.
            // Prefer apk-only packaging for the compile gate (Release defaults to
            // aab;apk). Full signed AAB distribution remains M12.
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

            DotNetPublish(s =>
            {
                s = s
                    .SetProject(AppProject)
                    .SetConfiguration(Configuration)
                    .SetFramework(WindowsFramework)
                    .SetRuntime(Runtime)
                    .SetSelfContained(true)
                    .SetOutput(PublishDirectory);

                if (!string.IsNullOrWhiteSpace(Version))
                {
                    s = s.SetProperty("Version", Version);
                }

                return s;
            });

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
            AbsolutePath exe = PublishDirectory / "GitPulse.exe";
            AbsolutePath dll = PublishDirectory / "GitPulse.dll";

            AbsolutePath entryPoint = exe.FileExists() ? exe : dll;
            Assert.FileExists(entryPoint,
                $"Published entry point not found. Expected {exe} or {dll} in {PublishDirectory}");

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
                return name.Equals("GitPulse.exe", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("GitPulse.dll", StringComparison.OrdinalIgnoreCase);
            });

            Assert.True(hasEntryPoint,
                $"Windows Release Artifact zip has wrong shape. Expected GitPulse.exe or GitPulse.dll inside {PublishZipFile}");

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
    ///   Full release pipeline: CiAll → Publish → PublishVerify.
    ///   Run on tag pushes (v*) or manually with --target Release.
    /// </summary>
    Target Release => _ => _
        .DependsOn(CiAll)
        .DependsOn(PublishVerify)
        .Executes(() =>
        {
            Console.WriteLine("Release pipeline completed successfully.");
        });
}
