using System;
using System.Linq;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Core.Infrastructure.Services.Notifications;
using CapitalUniversity.Core.IntegrationsTests._Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CapitalUniversity.Core.IntegrationsTests.Notifications;

public class NotificationServiceTests
{
    private readonly CoreDbContext _context;
    private readonly NotificationService _sut;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CoreDbContext(options);
        _sut = new NotificationService(_context, new TestLocalizationService());
    }

    [Fact]
    public async Task CreateNotificationAsync_ShouldSaveToDatabase()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var title = "Test Title";
        var message = "Test Message";
        var type = NotificationType.Info;

        // Act
        await _sut.CreateNotificationAsync(userId, title, message, type);

        // Assert
        var notification = await _context.Notifications.FirstOrDefaultAsync();
        notification.Should().NotBeNull();
        notification!.RecipientUserId.Should().Be(userId);
        
        // Entity carries the raw JSON; seeder/service normalizes plain text to bilingual JSON.
        LocalizedJson.Extract(notification.Title, "en").Should().Be(title);
        LocalizedJson.Extract(notification.Message, "en").Should().Be(message);
        
        notification.Type.Should().Be(type);
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserNotificationsAsync_ShouldReturnOnlyUserNotifications()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await _sut.CreateNotificationAsync(user1, "U1 Title", "Msg", NotificationType.Info);
        await _sut.CreateNotificationAsync(user2, "U2 Title", "Msg", NotificationType.Warning);

        // Act
        var result = await _sut.GetUserNotificationsAsync(user1);

        // Assert
        result.Should().HaveCount(1);
        result.First().Title.Should().Be("U1 Title");
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldUpdateIsReadStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await _sut.CreateNotificationAsync(userId, "Title", "Msg", NotificationType.Info);
        var notification = await _context.Notifications.FirstAsync();

        // Act
        await _sut.MarkAsReadAsync(notification.Id, userId);

        // Assert
        var updated = await _context.Notifications.FindAsync(notification.Id);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsReadAsync_WithWrongUser_ShouldNotUpdate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var wrongUserId = Guid.NewGuid();
        await _sut.CreateNotificationAsync(userId, "Title", "Msg", NotificationType.Info);
        var notification = await _context.Notifications.FirstAsync();

        // Act
        await _sut.MarkAsReadAsync(notification.Id, wrongUserId);

        // Assert
        var updated = await _context.Notifications.FindAsync(notification.Id);
        updated!.IsRead.Should().BeFalse();
    }
}