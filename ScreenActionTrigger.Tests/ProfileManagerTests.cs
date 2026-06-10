using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ScreenActionTrigger.Core.Models;
using ScreenActionTrigger.Persistence.Repositories;
using Xunit;

namespace ScreenActionTrigger.Tests;

public sealed class ProfileManagerTests : IDisposable
{
    private readonly ProfileRepository _repo;
    private readonly string _tempDir;

    public ProfileManagerTests()
    {
        _repo    = new ProfileRepository(NullLogger<ProfileRepository>.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), $"SAT_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void CreateNew_ReturnsProfileWithName()
    {
        var profile = _repo.CreateNew("Meu Perfil");

        profile.Name.Should().Be("Meu Perfil");
        profile.Id.Should().NotBe(Guid.Empty);
        profile.Regions.Should().BeEmpty();
        profile.Rules.Should().BeEmpty();
        profile.Templates.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        var profile = new ExecutionProfile
        {
            Name = "RoundTrip",
            Regions = new List<MonitoredRegion>
            {
                new() { Name = "Região 1", X = 10, Y = 20, Width = 300, Height = 200 }
            },
            Rules = new List<VisualRule>
            {
                new() { Name = "Regra A", CooldownMs = 750, Priority = 2 }
            },
            Settings = new AppSettings { CaptureIntervalMs = 200, OverlayEnabled = false }
        };

        var path = Path.Combine(_tempDir, "test.satprofile");
        await _repo.SaveAsync(profile, path);

        var loaded = await _repo.LoadAsync(path);

        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("RoundTrip");
        loaded.Regions.Should().HaveCount(1);
        loaded.Regions[0].Name.Should().Be("Região 1");
        loaded.Regions[0].X.Should().Be(10);
        loaded.Rules.Should().HaveCount(1);
        loaded.Rules[0].CooldownMs.Should().Be(750);
        loaded.Settings.CaptureIntervalMs.Should().Be(200);
        loaded.Settings.OverlayEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Load_NonExistentFile_ReturnsNull()
    {
        var result = await _repo.LoadAsync(Path.Combine(_tempDir, "ghost.satprofile"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Save_UpdatesTimestamp()
    {
        var profile = _repo.CreateNew("TimestampTest");
        var before  = profile.UpdatedAt;

        await Task.Delay(50);
        var path = Path.Combine(_tempDir, "ts.satprofile");
        await _repo.SaveAsync(profile, path);

        profile.UpdatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task Import_SameAsLoad()
    {
        var profile = _repo.CreateNew("ImportTest");
        var path    = Path.Combine(_tempDir, "import.satprofile");
        await _repo.SaveAsync(profile, path);

        var imported = await _repo.ImportAsync(path);

        imported.Should().NotBeNull();
        imported!.Name.Should().Be("ImportTest");
    }

    [Fact]
    public void AddRecentPath_MaxTen()
    {
        for (int i = 0; i < 15; i++)
            _repo.AddRecentPath($"C:/path/profile{i}.satprofile");

        _repo.GetRecentPaths().Should().HaveCount(10);
    }

    [Fact]
    public void AddRecentPath_MostRecentIsFirst()
    {
        _repo.AddRecentPath(@"C:.satprofile");
        _repo.AddRecentPath(@"C:.satprofile");
        _repo.AddRecentPath("C:/c.satprofile");

        _repo.GetRecentPaths().First().Should().Be("C:/c.satprofile");
    }

    [Fact]
    public void AddRecentPath_DuplicateMovesToFront()
    {
        _repo.AddRecentPath(@"C:.satprofile");
        _repo.AddRecentPath(@"C:.satprofile");
        _repo.AddRecentPath(@"C:.satprofile"); // re-add

        var paths = _repo.GetRecentPaths().ToList();
        paths[0].Should().Be(@"C:.satprofile");
        paths.Should().HaveCount(2);
    }

    [Fact]
    public async Task Save_CreatesDirectoryIfNotExists()
    {
        var nested = Path.Combine(_tempDir, "nested", "deep", "profile.satprofile");
        var profile = _repo.CreateNew("Nested");

        await _repo.SaveAsync(profile, nested);

        File.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoad_PreservesExtraTargetColors()
    {
        var profile = new ExecutionProfile
        {
            Name = "Colors",
            Rules = new List<VisualRule>
            {
                new()
                {
                    Name = "Cor multipla",
                    Condition = new RuleCondition
                    {
                        Type = ConditionType.ColorDetection,
                        TargetColor = "#FF0000",
                        TargetColors = new System.Collections.ObjectModel.ObservableCollection<string>
                        {
                            "#00FF00", "#0000FF"
                        }
                    }
                }
            },
            Settings = new AppSettings { HotkeyStartStop = "CTRL+F9", CaptureIntervalMs = 150 }
        };

        var path = Path.Combine(_tempDir, "colors.satprofile");
        await _repo.SaveAsync(profile, path);

        var loaded = await _repo.LoadAsync(path);

        loaded.Should().NotBeNull();
        loaded!.Rules[0].Condition.TargetColor.Should().Be("#FF0000");
        loaded.Rules[0].Condition.TargetColors.Should().BeEquivalentTo("#00FF00", "#0000FF");
        loaded.Settings.HotkeyStartStop.Should().Be("CTRL+F9");
        loaded.Settings.CaptureIntervalMs.Should().Be(150);
    }

    [Fact]
    public async Task CurrentProfile_UpdatedAfterLoad()
    {
        var profile = _repo.CreateNew("CurrentTest");
        var path    = Path.Combine(_tempDir, "current.satprofile");
        await _repo.SaveAsync(profile, path);

        await _repo.LoadAsync(path);

        _repo.CurrentProfile.Should().NotBeNull();
        _repo.CurrentProfile!.Name.Should().Be("CurrentTest");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
