using DenoVideoPlayer.Services;
using System.IO;

namespace DenoVideoPlayer.Tests;

public sealed class RuntimeExecutableValidatorTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "DenoRuntimeValidatorTests_" + Guid.NewGuid().ToString("N"));

    public RuntimeExecutableValidatorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void RejectsMissingEmptyAndNonPeFiles()
    {
        var missing = Path.Combine(_dir, "missing.exe");
        var empty = Path.Combine(_dir, "empty.exe");
        var text = Path.Combine(_dir, "text.exe");
        File.WriteAllBytes(empty, Array.Empty<byte>());
        File.WriteAllBytes(text, Enumerable.Repeat((byte)'x', 8192).ToArray());

        Assert.False(RuntimeExecutableValidator.HasPlausiblePortableExecutableHeader(missing));
        Assert.False(RuntimeExecutableValidator.HasPlausiblePortableExecutableHeader(empty));
        Assert.False(RuntimeExecutableValidator.HasPlausiblePortableExecutableHeader(text));
        Assert.False(RuntimeExecutableValidator.IsUsable(text, "--version", 100));
    }
}
