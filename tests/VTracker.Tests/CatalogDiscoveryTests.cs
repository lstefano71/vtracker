using VTracker.Core;

namespace VTracker.Tests;

public sealed class CatalogDiscoveryTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"vtracker-discovery-test-{Guid.NewGuid():N}");

    public CatalogDiscoveryTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Resolve_ExplicitPath_OverridesAutoDiscovery()
    {
        var autoPath = Path.Combine(_tempDir, CatalogDiscovery.DefaultCatalogFileName);
        File.WriteAllText(autoPath, "type,pattern,category\n");

        var explicitPath = Path.Combine(_tempDir, "custom-catalog.csv");
        File.WriteAllText(explicitPath, "type,pattern,category\n");

        var discovery = new CatalogDiscovery();
        var result = discovery.Resolve(explicitPath, _tempDir);

        Assert.Equal(Path.GetFullPath(explicitPath), result);
    }

    [Fact]
    public void Resolve_ExplicitPath_NonExistent_Throws()
    {
        var discovery = new CatalogDiscovery();

        var ex = Assert.Throws<VTrackerException>(
            () => discovery.Resolve(Path.Combine(_tempDir, "missing.csv"), _tempDir));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void Resolve_NoExplicit_AutoDiscoversFromCwd()
    {
        var autoPath = Path.Combine(_tempDir, CatalogDiscovery.DefaultCatalogFileName);
        File.WriteAllText(autoPath, "type,pattern,category\n");

        var discovery = new CatalogDiscovery();
        var result = discovery.Resolve(null, _tempDir);

        Assert.Equal(Path.GetFullPath(autoPath), result);
    }

    [Fact]
    public void Resolve_NoExplicit_NoCatalogInCwd_ReturnsNull()
    {
        var discovery = new CatalogDiscovery();
        var result = discovery.Resolve(null, _tempDir);

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyString_TreatedAsNoExplicit()
    {
        var discovery = new CatalogDiscovery();
        var result = discovery.Resolve("  ", _tempDir);

        // Whitespace-only explicit path should be treated as non-existent since the file won't exist
        // Actually per the implementation, whitespace is not null/whitespace check, let me check...
        // The implementation checks !string.IsNullOrWhiteSpace, so "  " is treated as no explicit path
        Assert.Null(result);
    }
}
