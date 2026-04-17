using System.ComponentModel;
using System.Diagnostics;

namespace VTracker.Core;

public sealed record FileVersionMetadata(string? FileVersion, string? ProductVersion);

public sealed class PeVersionService
{
    public FileVersionMetadata Read(string path)
    {
        var extension = Path.GetExtension(path);
        if (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return new FileVersionMetadata(null, null);
        }

        try
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(path);
            return new FileVersionMetadata(
                NormalizeValue(fileVersionInfo.FileVersion),
                NormalizeValue(fileVersionInfo.ProductVersion));
        }
        catch (ArgumentException)
        {
            return new FileVersionMetadata(null, null);
        }
        catch (FileNotFoundException)
        {
            return new FileVersionMetadata(null, null);
        }
        catch (IOException)
        {
            return new FileVersionMetadata(null, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new FileVersionMetadata(null, null);
        }
        catch (Win32Exception)
        {
            return new FileVersionMetadata(null, null);
        }
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
