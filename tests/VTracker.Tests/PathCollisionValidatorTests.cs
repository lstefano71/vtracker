using VTracker.Core;

namespace VTracker.Tests;

public sealed class PathCollisionValidatorTests
{
    [Fact]
    public void EnsureUnique_ThrowsWhenPathsCollapseToTheSameCaseInsensitiveKey()
    {
        var validator = new PathCollisionValidator();

        Assert.Throws<NormalizedPathCollisionException>(
            () => validator.EnsureUnique(
                [
                    ("bin/file.dll", @"bin\file.dll"),
                    ("BIN/FILE.DLL", @"BIN\FILE.DLL"),
                ]));
    }
}
