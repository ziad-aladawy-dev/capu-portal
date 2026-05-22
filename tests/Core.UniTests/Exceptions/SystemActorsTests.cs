using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Exceptions;

/// <summary>
/// Runtime Hardening Plan §3.2 — the SystemActors constants must be stable so
/// historical audit rows do not orphan when the assembly is rebuilt. They must
/// also be distinct so audit queries can filter them apart.
/// </summary>
public class SystemActorsTests
{
    [Fact]
    public void Actors_AreNonEmpty_AndDistinct()
    {
        var ids = new[]
        {
            SystemActors.BackgroundProcessor,
            SystemActors.AcademicTimeline,
            SystemActors.OutboxDispatcher,
        };

        foreach (var id in ids)
        {
            id.Should().NotBeEmpty("SystemActors must carry a deterministic, non-empty Guid");
        }

        ids.Distinct().Should().HaveCount(ids.Length, "actor IDs must be unique");
    }

    [Fact]
    public void Actors_HaveStableValues()
    {
        // Stability check — if these change you'll orphan audit history.
        // Update intentionally only as part of an explicit migration.
        SystemActors.BackgroundProcessor.Should().Be(new Guid("00000000-0000-0000-0000-00000000B6C5"));
        SystemActors.AcademicTimeline.Should().Be(new Guid("00000000-0000-0000-0000-00000000A7E1"));
        SystemActors.OutboxDispatcher.Should().Be(new Guid("00000000-0000-0000-0000-00000000017B"));
        SystemActors.DisplayName.Should().Be("System");
    }
}
