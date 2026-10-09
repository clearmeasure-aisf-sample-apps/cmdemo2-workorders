using ClearMeasure.Bootcamp.UI.Shared;
using Shouldly;

namespace ClearMeasure.Bootcamp.UnitTests.UI.Shared;

[TestFixture]
public class AppVersionFormatterTests
{
    private const string Commit = "abc1234def5678901abc1234def5678901abc123";

    [TestCase("2.4.18+" + Commit, "2.4.18")]
    [TestCase("2.4.18+local", "2.4.18")]
    [TestCase("3.0.0-rc.1+" + Commit, "3.0.0-rc.1")]
    [TestCase("2.4.18", "2.4.18")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void DisplayVersion_ShouldDropBuildMetadata_WhateverFollowsThePlus(
        string? informationalVersion,
        string expected)
    {
        AppVersionFormatter.DisplayVersion(informationalVersion).ShouldBe(expected);
    }
}
