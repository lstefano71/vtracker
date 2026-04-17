using VTracker.Core;

namespace VTracker.Tests;

public sealed class PathNormalizerTests
{
    private readonly PathNormalizer _pathNormalizer = new();

    [Theory]
    [InlineData(@".\bin\file.dll", "bin/file.dll")]
    [InlineData("nested/file.txt", "nested/file.txt")]
    public void NormalizeRelativePath_NormalizesExpectedShapes(string input, string expected)
    {
        var result = _pathNormalizer.NormalizeRelativePath(input);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(@"..\bin\file.dll")]
    [InlineData(@"\bin\file.dll")]
    [InlineData("bin//file.dll")]
    public void NormalizeRelativePath_RejectsInvalidRelativePaths(string input)
    {
        Assert.Throws<VTrackerException>(() => _pathNormalizer.NormalizeRelativePath(input));
    }
}
