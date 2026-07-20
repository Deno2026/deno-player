using DenoVideoPlayer.Services;

namespace DenoVideoPlayer.Tests;

public class FileAssociationServiceTests
{
    [Theory]
    [InlineData(".mp4", FileAssociationService.VideoProgId)]
    [InlineData("mkv", FileAssociationService.VideoProgId)]
    [InlineData(".mp3", FileAssociationService.AudioProgId)]
    [InlineData("flac", FileAssociationService.AudioProgId)]
    [InlineData(".mka", FileAssociationService.AudioProgId)]
    [InlineData(".png", FileAssociationService.ImageProgId)]
    [InlineData("webp", FileAssociationService.ImageProgId)]
    public void ProgIdForExtensionUsesMediaKindSpecificProgIds(string extension, string expected)
    {
        Assert.Equal(expected, FileAssociationService.ProgIdForExtension(extension));
    }
}
