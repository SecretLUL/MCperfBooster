using MCperfBooster.Core.Services;

namespace MCperfBooster.Tests;

/// <summary>
/// What Windows would show about the launcher, as plain lists, so no test reads the registry, the disk or the
/// process list. File paths compare without regard to case, as Windows' do.
/// </summary>
internal sealed class FakeLauncherTraces : ILauncherTraces
{
    public HashSet<string> AppPackages { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? NotedFolder { get; set; }

    public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Running { get; set; }

    public bool HasAppPackage(string packageFamilyName) => AppPackages.Contains(packageFamilyName);

    public string? NotedInstallFolder() => NotedFolder;

    public bool FileExists(string path) => Files.Contains(path);

    public bool IsLauncherRunning() => Running;
}

public class MinecraftLauncherTests
{
    private const string StorePackage = "Microsoft.4297127D64EC6_8wekyb3d8bbwe";
    private const string BedrockPackage = "Microsoft.MinecraftUWP_8wekyb3d8bbwe";

    // Only the names matter: the fake answers for the disk.
    private static readonly string ProgramFilesX86 = Path.Combine(TestEnvironment.RunDirectory, "Program Files (x86)");
    private static readonly string ChosenFolder = Path.Combine(TestEnvironment.RunDirectory, "Games", "Minecraft Launcher");

    private readonly FakeLauncherTraces _traces = new();

    private bool IsInstalled() => MinecraftLauncher.IsInstalled(_traces, ProgramFilesX86);

    [Fact]
    public void IsInstalled_FindsNothingOnAPcWithoutALauncher()
    {
        Assert.False(IsInstalled());
    }

    [Fact]
    public void IsInstalled_FindsTheLauncherFromTheMicrosoftStoreOrTheXboxApp()
    {
        _traces.AppPackages.Add(StorePackage);

        Assert.True(IsInstalled());
    }

    [Fact]
    public void IsInstalled_DoesNotCountBedrockEdition()
    {
        _traces.AppPackages.Add(BedrockPackage);

        Assert.False(IsInstalled());
    }

    [Fact]
    public void IsInstalled_FindsMojangsInstallerInItsOwnFolder()
    {
        _traces.Files.Add(Path.Combine(ProgramFilesX86, "Minecraft Launcher", "MinecraftLauncher.exe"));

        Assert.True(IsInstalled());
    }

    [Fact]
    public void IsInstalled_FindsMojangsInstallerInAFolderChosenDuringSetup()
    {
        _traces.NotedFolder = ChosenFolder;
        _traces.Files.Add(Path.Combine(ChosenFolder, "MinecraftLauncher.exe"));

        Assert.True(IsInstalled());
    }

    [Fact]
    public void IsInstalled_IgnoresANotedFolderTheLauncherIsGoneFrom()
    {
        // Left behind by a launcher deleted by hand.
        _traces.NotedFolder = ChosenFolder;

        Assert.False(IsInstalled());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Minecraft Launcher")]
    public void IsInstalled_IgnoresANotedFolderThatIsNoFullPath(string folder)
    {
        // A relative path would be looked up in whatever folder MCperfBooster runs in.
        _traces.NotedFolder = folder;
        _traces.Files.Add(Path.Combine(folder, "MinecraftLauncher.exe"));

        Assert.False(IsInstalled());
    }

    [Fact]
    public void IsInstalled_LooksForNoDefaultFolderWithoutProgramFiles()
    {
        // Outside Windows there is no Program Files (x86); the folder name alone is no path.
        _traces.Files.Add(Path.Combine("Minecraft Launcher", "MinecraftLauncher.exe"));

        Assert.False(MinecraftLauncher.IsInstalled(_traces, ""));
    }

    [Fact]
    public void IsInstalled_CountsARunningLauncherWhereverItCameFrom()
    {
        _traces.Running = true;

        Assert.True(IsInstalled());
    }

    [Fact]
    public void DownloadUrl_IsMojangsDownloadForWindows()
    {
        Assert.Equal("https://aka.ms/minecraftClientWindows", MinecraftLauncher.DownloadUrl);
    }

    [Theory]
    [InlineData(122, 1u, true)]   // ERROR_INSUFFICIENT_BUFFER: packages, and no room for their names
    [InlineData(0, 1u, true)]
    [InlineData(0, 0u, false)]    // ERROR_SUCCESS: none
    [InlineData(122, 0u, false)]
    [InlineData(87, 0u, false)]   // ERROR_INVALID_PARAMETER: the question failed
    [InlineData(87, 1u, false)]
    public void WindowsTraces_ReadWindowsAnswerToTheCountOfPackages(int result, uint count, bool found)
    {
        Assert.Equal(found, WindowsLauncherTraces.FoundPackages(result, count));
    }

    [Fact]
    public void WindowsTraces_FindNoAppPackageOfAFamilyThatDoesNotExist()
    {
        // Asks Windows for real: the call must not throw, and a family nobody has is not found.
        Assert.False(new WindowsLauncherTraces().HasAppPackage("MCperfBooster.Tests.NoSuchPackage_0000000000000"));
    }

    [Fact]
    public void IsInstalled_DoesNotThrowOnThisMachine()
    {
        // The result depends on the machine; asking Windows must not fail.
        _ = MinecraftLauncher.IsInstalled();
    }
}
