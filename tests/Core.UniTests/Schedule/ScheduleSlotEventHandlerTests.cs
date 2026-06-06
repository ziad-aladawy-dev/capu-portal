using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Application.Outbox;
using FluentAssertions;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Schedule;

/// <summary>
/// Pins the outbox-handler side of the Schedule event flow. The dispatcher
/// invokes <c>HandleAsync</c> with the raw JSON payload — the handler must
/// deserialise correctly and surface the fact through <c>IAppLogger</c> with
/// every relevant field present in the metadata bag.
/// </summary>
public class ScheduleSlotEventHandlerTests
{
    private static string SerializePayload() =>
        JsonSerializer.Serialize(new ScheduleSlotEventHandler.ScheduleSlotFact(
            ScheduleSlotId: new Guid("11111111-1111-1111-1111-111111111111"),
            CourseOfferingId: new Guid("22222222-2222-2222-2222-222222222222"),
            DayOfWeek: DayOfWeek.Wednesday,
            StartTime: new TimeOnly(14, 0),
            EndTime: new TimeOnly(15, 30),
            Kind: ScheduleSlotKind.Lab));

    [Fact]
    public void TypeKey_Discriminators_AreStableAndNamespaced()
    {
        // Discriminator strings are part of the outbox contract — once an
        // operator has tooling keyed on these, renaming them silently breaks
        // downstream consumers. Pin them explicitly.
        ScheduleSlotCreatedHandler.TypeKey.Should().Be("schedule.slot.created");
        ScheduleSlotUpdatedHandler.TypeKey.Should().Be("schedule.slot.updated");
        ScheduleSlotDeletedHandler.TypeKey.Should().Be("schedule.slot.deleted");
    }

    [Fact]
    public async Task CreatedHandler_LogsFactWithFullMetadata()
    {
        var logger = new Mock<IAppLogger>();
        var sut = new ScheduleSlotCreatedHandler(logger.Object);

        await sut.HandleAsync(Guid.NewGuid(), SerializePayload(), CancellationToken.None);

        logger.Verify(l => l.LogInfoAsync(
            It.IsAny<string>(),
            nameof(ScheduleSlotEventHandler),
            null,
            It.Is<Dictionary<string, object>>(m =>
                (string)m["MessageType"] == ScheduleSlotCreatedHandler.TypeKey
                && (Guid)m["ScheduleSlotId"] == new Guid("11111111-1111-1111-1111-111111111111")
                && (Guid)m["CourseOfferingId"] == new Guid("22222222-2222-2222-2222-222222222222")
                && (string)m["DayOfWeek"] == "Wednesday"
                && (string)m["StartTime"] == "14:00"
                && (string)m["EndTime"] == "15:30"
                && (string)m["Kind"] == "Lab")),
            Times.Once);
    }

    [Fact]
    public async Task UpdatedHandler_UsesUpdatedDiscriminator()
    {
        var logger = new Mock<IAppLogger>();
        var sut = new ScheduleSlotUpdatedHandler(logger.Object);

        await sut.HandleAsync(Guid.NewGuid(), SerializePayload(), CancellationToken.None);

        logger.Verify(l => l.LogInfoAsync(
            It.IsAny<string>(),
            nameof(ScheduleSlotEventHandler),
            null,
            It.Is<Dictionary<string, object>>(m =>
                (string)m["MessageType"] == ScheduleSlotUpdatedHandler.TypeKey)),
            Times.Once);
    }

    [Fact]
    public async Task DeletedHandler_UsesDeletedDiscriminator()
    {
        var logger = new Mock<IAppLogger>();
        var sut = new ScheduleSlotDeletedHandler(logger.Object);

        await sut.HandleAsync(Guid.NewGuid(), SerializePayload(), CancellationToken.None);

        logger.Verify(l => l.LogInfoAsync(
            It.IsAny<string>(),
            nameof(ScheduleSlotEventHandler),
            null,
            It.Is<Dictionary<string, object>>(m =>
                (string)m["MessageType"] == ScheduleSlotDeletedHandler.TypeKey)),
            Times.Once);
    }

    [Fact]
    public async Task Handler_NullPayloadThrows()
    {
        // The deserialiser returning null on a "null" body is a real
        // edge case — surfacing it as a thrown handler means the
        // dispatcher will bump AttemptCount and eventually poison the
        // row, which is the desired "loud failure" behavior.
        var sut = new ScheduleSlotCreatedHandler(Mock.Of<IAppLogger>());

        var act = () => sut.HandleAsync(Guid.NewGuid(), "null", CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}