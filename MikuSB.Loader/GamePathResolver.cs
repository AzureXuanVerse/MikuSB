using Microsoft.Win32;
using MikuSB.Util;
using System.Runtime.Versioning;

namespace MikuSB.Loader;

public static class GamePathResolver
{
    private const string GameExecutableSuffix = @"game\Game\Binaries\Win64\Game.exe";

    public static string? Resolve(string? configuredPath, string baseDirectory)
    {
        return Resolve(configuredPath, baseDirectory, ReadRegistryValue, ReadMuiCacheValueName, File.Exists);
    }

    public static bool TryPersistAutoDetectedGamePath()
    {
        if (!string.IsNullOrWhiteSpace(ConfigManager.Config.Loader.GamePath))
            return false;

        var gamePath = Resolve(ConfigManager.Config.Loader.GamePath, AppContext.BaseDirectory);
        if (string.IsNullOrWhiteSpace(gamePath))
            return false;

        ConfigManager.Config.Loader.GamePath = Path.GetFullPath(gamePath);
        ConfigManager.SaveConfig();
        return true;
    }

    internal static string? Resolve(
        string? configuredPath,
        string baseDirectory,
        TryReadRegistryValue readRegistryValue,
        TryReadRegistryValue readMuiCacheValueName,
        Func<string, bool> fileExists)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return ResolveConfiguredPath(configuredPath, baseDirectory);

        foreach (var candidate in EnumerateRegistryCandidates(readRegistryValue, readMuiCacheValueName))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && fileExists(candidate))
                return candidate;
        }

        return null;
    }

    internal delegate bool TryReadRegistryValue(string keyName, out string? value);

    private static string ResolveConfiguredPath(string configuredPath, string baseDirectory)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(baseDirectory, configuredPath));
    }

    private static IEnumerable<string> EnumerateRegistryCandidates(
        TryReadRegistryValue readRegistryValue,
        TryReadRegistryValue readMuiCacheValueName)
    {
        foreach (var keyName in new[]
        {
            @"HKLM\SOFTWARE\SeasunGameSnowBreakOs\InstalledGamePath|snowbreak",
            @"HKLM\SOFTWARE\Kingsoft\SeasunGameGP\snowbreak|InstallPath"
        })
        {
            if (readRegistryValue(keyName, out var installDirectory))
                yield return BuildGameExecutablePath(installDirectory);
        }

        foreach (var keyName in new[]
        {
            @"HKCR\gplauncherSnowBreakOs\shell\open\command|",
            @"HKLM\SOFTWARE\Classes\gplauncherSnowBreakOs\shell\open\command|",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SeasunGameSnowBreakOs|DisplayIcon"
        })
        {
            if (readRegistryValue(keyName, out var launcherCommand))
                yield return BuildGameExecutablePathFromLauncher(launcherCommand);
        }

        foreach (var keyName in new[]
        {
            @"HKCR\Local Settings\Software\Microsoft\Windows\Shell\MuiCache|",
            @"HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache|"
        })
        {
            if (readMuiCacheValueName(keyName, out var muiPath))
                yield return ExtractMuiCacheExecutablePath(muiPath);
        }
    }

    private static string BuildGameExecutablePath(string? installDirectory)
    {
        return string.IsNullOrWhiteSpace(installDirectory)
            ? string.Empty
            : Path.GetFullPath(Path.Combine(NormalizeSlashes(installDirectory), GameExecutableSuffix));
    }

    private static string BuildGameExecutablePathFromLauncher(string? launcherCommand)
    {
        var launcherPath = ExtractExecutablePath(launcherCommand);
        var launcherDirectory = string.IsNullOrWhiteSpace(launcherPath) ? null : Path.GetDirectoryName(launcherPath);
        return string.IsNullOrWhiteSpace(launcherDirectory)
            ? string.Empty
            : Path.GetFullPath(Path.Combine(launcherDirectory, @"Game\snowbreak", GameExecutableSuffix));
    }

    private static string ExtractMuiCacheExecutablePath(string? valueName)
    {
        if (string.IsNullOrWhiteSpace(valueName))
            return string.Empty;

        var marker = @"SeasunSnowBreakOs\Game\snowbreak\game\Game\Binaries\Win64\Game.exe";
        var normalized = NormalizeSlashes(valueName);
        var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? string.Empty : normalized[..(index + marker.Length)];
    }

    private static string ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        var trimmed = NormalizeSlashes(command.Trim());
        if (trimmed[0] == '"')
        {
            var endQuote = trimmed.IndexOf('"', 1);
            return endQuote > 1 ? trimmed[1..endQuote] : string.Empty;
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex < 0 ? string.Empty : trimmed[..(exeIndex + 4)];
    }

    private static string NormalizeSlashes(string value) => value.Replace('/', Path.DirectorySeparatorChar);

    private static bool ReadRegistryValue(string keyName, out string? value)
    {
        if (!OperatingSystem.IsWindows())
        {
            value = null;
            return false;
        }

        var separator = keyName.LastIndexOf('|');
        var path = keyName[..separator];
        var valueName = keyName[(separator + 1)..];
        value = ReadRegistryValue(RegistryView.Registry64, path, valueName)
            ?? ReadRegistryValue(RegistryView.Registry32, path, valueName);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool ReadMuiCacheValueName(string keyName, out string? value)
    {
        if (!OperatingSystem.IsWindows())
        {
            value = null;
            return false;
        }

        var separator = keyName.LastIndexOf('|');
        var path = keyName[..separator];
        value = ReadMuiCacheValueName(RegistryView.Registry64, path)
            ?? ReadMuiCacheValueName(RegistryView.Registry32, path);
        return !string.IsNullOrWhiteSpace(value);
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistryValue(RegistryView view, string path, string valueName)
    {
        var separator = path.IndexOf('\\');
        var hiveName = separator < 0 ? path : path[..separator];
        var subKeyName = separator < 0 ? string.Empty : path[(separator + 1)..];

        using var hive = RegistryKey.OpenBaseKey(GetRegistryHive(hiveName), view);
        using var subKey = hive.OpenSubKey(subKeyName, false);
        if (subKey is null)
            return null;

        if (valueName.Length > 0)
            return subKey.GetValue(valueName)?.ToString();

        return subKey.GetValue(null)?.ToString();
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadMuiCacheValueName(RegistryView view, string path)
    {
        var separator = path.IndexOf('\\');
        var hiveName = separator < 0 ? path : path[..separator];
        var subKeyName = separator < 0 ? string.Empty : path[(separator + 1)..];

        using var hive = RegistryKey.OpenBaseKey(GetRegistryHive(hiveName), view);
        using var subKey = hive.OpenSubKey(subKeyName, false);
        return subKey?.GetValueNames()
            .FirstOrDefault(x => x.Contains(@"SeasunSnowBreakOs\Game\snowbreak\" + GameExecutableSuffix, StringComparison.OrdinalIgnoreCase));
    }

    [SupportedOSPlatform("windows")]
    private static RegistryHive GetRegistryHive(string hiveName)
    {
        return hiveName switch
        {
            "HKCR" or "HKEY_CLASSES_ROOT" => RegistryHive.ClassesRoot,
            "HKCU" or "HKEY_CURRENT_USER" => RegistryHive.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => RegistryHive.LocalMachine,
            _ => throw new ArgumentOutOfRangeException(nameof(hiveName), hiveName, "Unsupported registry hive.")
        };
    }
}
