using ActivityGearSync.Shared;

namespace ActivityGearSync.Tests.Shared;

public class VersionLogicTests
{
    [Test]
    [Arguments("1.0.0", false)]
    [Arguments("1.2.3", false)]
    [Arguments("0.0.1", false)]
    [Arguments("10.20.30", false)]
    [Arguments("main.abc1234", true)]
    [Arguments("feature.login.def5678", true)]
    [Arguments("dev", true)]
    [Arguments("", true)]
    public async Task IsDevBuild_WithVersion_ReturnsExpected(string version, bool expected)
    {
        bool result = VersionLogic.IsDevBuild(version);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("1.1.0", "1.0.0", true)]
    [Arguments("2.0.0", "1.9.9", true)]
    [Arguments("1.0.1", "1.0.0", true)]
    [Arguments("1.0.0", "1.0.0", false)]
    [Arguments("1.0.0", "1.1.0", false)]
    [Arguments("1.0.0", "2.0.0", false)]
    public async Task IsNewerVersion_WithSemanticVersions_ReturnsExpected(string latest, string current, bool expected)
    {
        bool result = VersionLogic.IsNewerVersion(latest, current);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0L, "0 B")]
    [Arguments(512L, "512 B")]
    [Arguments(1024L, "1 KB")]
    [Arguments(1536L, "1.5 KB")]
    [Arguments(1048576L, "1 MB")]
    [Arguments(1572864L, "1.5 MB")]
    [Arguments(1073741824L, "1 GB")]
    [Arguments(1610612736L, "1.5 GB")]
    public async Task FormatFileSize_WithBytes_ReturnsFormattedString(long bytes, string expected)
    {
        string result = VersionLogic.FormatFileSize(bytes);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task GetRuntimeIdentifier_ReturnsValidRid()
    {
        string result = VersionLogic.GetRuntimeIdentifier();
        string[] validRids = ["win-x64", "osx-arm64", "linux-x64"];

        await Assert.That(validRids).Contains(result);
    }
}
