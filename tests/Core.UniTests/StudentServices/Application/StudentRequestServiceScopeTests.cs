using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Module.StudentServices.Abstractions.Dto;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Application;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Abstractions.Hubs;
using CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace CapitalUniversity.Core.UniTests.StudentServices.Application;

public class StudentRequestServiceScopeTests
{
    private readonly Mock<IStudentRequestRepository> _requestRepositoryMock;
    private readonly Mock<IServiceRepository> _serviceRepositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<IHubContext<StudentServicesHub>> _hubContextMock;
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IEffectiveScope> _effectiveScopeMock;
    private readonly StudentRequestService _sut;

    public StudentRequestServiceScopeTests()
    {
        _requestRepositoryMock = new Mock<IStudentRequestRepository>();
        _serviceRepositoryMock = new Mock<IServiceRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _hubContextMock = new Mock<IHubContext<StudentServicesHub>>();
        _currentUserMock = new Mock<ICurrentUser>();
        _effectiveScopeMock = new Mock<IEffectiveScope>();

        _sut = new StudentRequestService(
            _requestRepositoryMock.Object,
            _serviceRepositoryMock.Object,
            _notificationServiceMock.Object,
            _hubContextMock.Object,
            _currentUserMock.Object,
            _effectiveScopeMock.Object);
    }

    [Fact]
    public async Task GetStudentRequestAsync_WhenOutOfScope_ThrowsNotFoundException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var request = new StudentRequest { Id = requestId, StudentId = studentId };
        
        _requestRepositoryMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        
        _effectiveScopeMock.Setup(s => s.CanAccessStudentAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetStudentRequestAsync(requestId));
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenOutOfScope_ThrowsNotFoundException()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var request = new StudentRequest { Id = requestId, StudentId = studentId, Status = RequestStatus.Pending };
        
        _requestRepositoryMock.Setup(r => r.GetByIdWithDetailsAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);
        
        _effectiveScopeMock.Setup(s => s.CanAccessStudentAsync(studentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.UpdateStatusAsync(requestId, RequestStatus.Approved));
    }

    [Fact]
    public async Task GetAllRequestsForStaffAsync_FiltersOutOfScopeRequests()
    {
        // Arrange
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        var requests = new List<StaffRequestListItemDto>
        {
            new StaffRequestListItemDto { Id = Guid.NewGuid(), StudentId = student1Id, StudentCode = "S1" },
            new StaffRequestListItemDto { Id = Guid.NewGuid(), StudentId = student2Id, StudentCode = "S2" }
        };

        _requestRepositoryMock.Setup(r => r.GetAllForStaffAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);

        _effectiveScopeMock.Setup(s => s.CanAccessStudentAsync(student1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _effectiveScopeMock.Setup(s => s.CanAccessStudentAsync(student2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.GetAllRequestsForStaffAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].StudentCode.Should().Be(requests[0].StudentCode);
    }
}