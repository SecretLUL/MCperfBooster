using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MCperfBooster.Core.Services;

/// <summary>
/// Where Windows shows whether the official Minecraft Launcher is installed. Replaceable so tests never look at the
/// real system.
/// </summary>
internal interface ILauncherTraces
{
    /// <summary>Whether an app package of this family is installed for the current user.</summary>
    bool HasAppPackage(string packageFamilyName);

    /// <summary>The folder Mojang's installer noted for the launcher, or null when it noted none.</summary>
    string? NotedInstallFolder();

    /// <summary>Whether there is a file at this full path.</summary>
    bool FileExists(string path);

    /// <summary>Whether a Minecraft launcher is running.</summary>
    bool IsLauncherRunning();
}

/// <summary>
/// The real traces: the app packages Windows has installed for the user, the note Mojang's installer leaves under
/// HKEY_CURRENT_USER, the disk and the running processes. Outside Windows there is no such launcher: nothing is found.
/// </summary>
internal sealed class WindowsLauncherTraces : ILauncherTraces
{
    // Mojang's installer notes the folder it put the launcher in here; Fabric's and Quilt's installers find it by
    // this as well.
    private const string InstalledProductKey = @"Software\Mojang\InstalledProducts\Minecraft Launcher";

    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;

    public bool HasAppPackage(string packageFamilyName)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        // Asked with no room for the names, Windows only counts the packages.
        uint count = 0, bufferLength = 0;
        int result = GetPackagesByPackageFamily(packageFamilyName, ref count, 0, ref bufferLength, 0);
        return FoundPackages(result, count);
    }

    /// <summary>
    /// Reads Windows' answer to that count: ERROR_INSUFFICIENT_BUFFER and the count when there are packages, as there
    /// is no room for their names, ERROR_SUCCESS and none when there are none. Any other answer is a failed question.
    /// </summary>
    internal static bool FoundPackages(int result, uint count) =>
        (result is ErrorSuccess or ErrorInsufficientBuffer) && count > 0;

    public string? NotedInstallFolder()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(InstalledProductKey);
        return key?.GetValue("InstallLocation") as string;
    }

    public bool FileExists(string path) => File.Exists(path);

    public bool IsLauncherRunning() => MinecraftService.IsLauncherRunning();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackagesByPackageFamily(
        string packageFamilyName, ref uint count, nint packageFullNames, ref uint bufferLength, nint buffer);
}

/// <summary>
/// Whether the official Minecraft Launcher is installed. MCperfBooster sets Fabric up for it, and Minecraft is started
/// from it, so without it the mods are of no use.
/// </summary>
public static class MinecraftLauncher
{
    /// <summary>Mojang's download of the launcher for Windows (MinecraftInstaller.msi).</summary>
    public const string DownloadUrl = "https://aka.ms/minecraftClientWindows";

    // The Microsoft Store and the Xbox app install the launcher as this app package. Bedrock Edition is a package of
    // its own and does not count.
    internal const string AppPackageFamily = "Microsoft.4297127D64EC6_8wekyb3d8bbwe";

    // Mojang's installer puts the launcher in this folder under Program Files (x86), unless another one is chosen.
    internal const string InstallFolderName = "Minecraft Launcher";
    internal const string LauncherExeName = "MinecraftLauncher.exe";

    /// <summary>
    /// True if the official Minecraft Launcher is installed for the current user: from the Microsoft Store or the Xbox
    /// app, or with Mojang's installer, in its own folder or in one chosen during setup. A launcher that is running
    /// counts as well, wherever it came from. Throws when Windows cannot be asked, for example when the registry
    /// refuses access.
    /// </summary>
    public static bool IsInstalled() =>
        IsInstalled(new WindowsLauncherTraces(), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

    internal static bool IsInstalled(ILauncherTraces traces, string programFilesX86)
    {
        if (traces.HasAppPackage(AppPackageFamily))
            return true;

        // A noted folder can outlive a launcher deleted by hand, so the launcher itself has to be in it. Only full
        // paths count: a relative one would be looked up in whatever folder MCperfBooster happens to run in.
        string?[] folders = { traces.NotedInstallFolder(), Path.Combine(programFilesX86, InstallFolderName) };
        if (folders.Any(folder => folder != null && Path.IsPathFullyQualified(folder) &&
                                  traces.FileExists(Path.Combine(folder, LauncherExeName))))
        {
            return true;
        }

        return traces.IsLauncherRunning();
    }
}
