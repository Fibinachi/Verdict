using Xunit;
using Verdict.Services;

namespace Verdict.Tests;

public class ModelDownloadStatusTests
{
    [Fact]
    public void ReadyDownloadComplete_string_matches_expected_ui_text()
    {
        Assert.Equal("Ready - download complete", ModelDownloadStatus.ReadyDownloadComplete);
    }
}
