using CapitalUniversity.Core.Abstractions.Courses.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Courses;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Application.Courses;
using CapitalUniversity.Core.Application.Semesters;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using CapitalUniversity.Modules.Payments.Application;
using CapitalUniversity.Modules.Payments.Application.Validators;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Payments.Repositories;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.Localization;

/// <summary>
/// Runtime Hardening Plan §2 — verify that exceptions thrown from owned modules
/// (Courses, Semesters, Payments, StudentInformation) carry a known
/// <see cref="LocalizedKeys"/> entry so the cross-cutting handler can localize
/// them on the way out.
///
/// <para>
/// These tests do NOT call <see cref="LocalizationService"/> — they assert that
/// the exception message itself equals a registered key. That key-vs-literal
/// distinction is what gates downstream localization in
/// <c>GlobalExceptionHandler</c>.
/// </para>
/// </summary>
public class OwnedModulesLocalizationTests
{
    // ─── Courses ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CourseService_NotFoundOnUpdate_MessageIsLocalizationKey()
    {
        var (sut, courses) = BuildCourseService();
        courses.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync((Course?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateAsync(Guid.NewGuid(), new UpdateCourseRequest { Title = "X" }));

        ex.Message.Should().Be(LocalizedKeys.Courses.NotFound);
        LocalizedStrings.ContainsKey(ex.Message).Should().BeTrue();
    }

    [Fact]
    public async Task CourseService_DuplicateCode_MessageIsLocalizationKey()
    {
        var (sut, courses) = BuildCourseService();
        courses.Setup(c => c.CodeExistsAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            sut.CreateAsync(new CreateCourseRequest { Code = "CS101", Title = "T", CreditHours = 3 }));

        ex.Message.Should().Be(LocalizedKeys.Courses.CodeInUse);
        LocalizedStrings.ContainsKey(ex.Message).Should().BeTrue();
    }

    // ─── Semesters ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SemesterService_MissingYear_ValidationMessageIsLocalizationKey()
    {
        var (sut, years, _) = BuildSemesterService();
        years.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            sut.CreateAsync(new CreateSemesterRequest { AcademicYearId = Guid.NewGuid() }));

        ex.Errors["AcademicYearId"][0].Should().Be(LocalizedKeys.Semesters.AcademicYearMissing);
        LocalizedStrings.ContainsKey(ex.Errors["AcademicYearId"][0]).Should().BeTrue();
    }

    [Fact]
    public async Task SemesterService_NotFoundOnUpdate_MessageIsLocalizationKey()
    {
        var (sut, _, semesters) = BuildSemesterService();
        semesters.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Semester?)null);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.UpdateAsync(Guid.NewGuid(), new UpdateSemesterRequest()));

        ex.Message.Should().Be(LocalizedKeys.Semesters.NotFound);
        LocalizedStrings.ContainsKey(ex.Message).Should().BeTrue();
    }

    [Fact]
    public async Task AcademicYearService_NotFoundOnDelete_MessageIsLocalizationKey()
    {
        var uow = new Mock<IUnitOfWork>();
        var years = new Mock<IAcademicYearRepository>();
        uow.Setup(u => u.AcademicYears).Returns(years.Object);
        var createV = new Mock<IValidator<CreateAcademicYearRequest>>();
        createV.Setup(v => v.ValidateAsync(It.IsAny<CreateAcademicYearRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ValidationResult());
        var updateV = new Mock<IValidator<(Guid, UpdateAcademicYearRequest)>>();
        updateV.Setup(v => v.ValidateAsync(It.IsAny<(Guid, UpdateAcademicYearRequest)>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ValidationResult());
        years.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((AcademicYear?)null);

        var sut = new AcademicYearService(uow.Object, createV.Object, updateV.Object);

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => sut.DeleteAsync(Guid.NewGuid()));

        ex.Message.Should().Be(LocalizedKeys.Semesters.AcademicYearNotFound);
        LocalizedStrings.ContainsKey(ex.Message).Should().BeTrue();
    }

    // ─── Payments ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task FeeCreationService_EmptyItems_MessageIsLocalizationKey()
    {
        var uow = new Mock<IUnitOfWork>();
        var invoices = new Mock<IInvoiceRepository>();
        var cache = new Mock<ICacheService>();
        var sut = new FeeCreationService(uow.Object, invoices.Object, cache.Object);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.CreateFeesAsync(Guid.NewGuid(), "USD", Array.Empty<CreateInvoiceItemRequest>()));

        // ArgumentException prepends "(Parameter ...)"; the leading text must be
        // the localization key so the wire-level handler can resolve it.
        ex.Message.Should().StartWith(LocalizedKeys.Payments.AtLeastOneItem);
        LocalizedStrings.ContainsKey(LocalizedKeys.Payments.AtLeastOneItem).Should().BeTrue();
    }

    [Fact]
    public void InvoiceValidator_EmptyItems_MessageIsLocalizationKey()
    {
        var sut = new CreateInvoiceValidator();
        var result = sut.Validate(new CreateInvoiceRequest
        {
            StudentId = Guid.NewGuid(),
            Currency = "USD",
            Items = new List<CreateInvoiceItemRequest>(),
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == LocalizedKeys.Payments.AtLeastOneItem);
    }

    [Fact]
    public void CourseValidator_OutOfRangeCreditHours_MessageIsLocalizationKey()
    {
        var sut = new Application.Courses.Validators.CreateCourseValidator();
        var result = sut.Validate(new CreateCourseRequest { Code = "X", Title = "T", CreditHours = 999 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == LocalizedKeys.Courses.CreditHoursOutOfRange);
    }

    [Fact]
    public void AcademicPlanValidator_EffectiveToBeforeFrom_MessageIsLocalizationKey()
    {
        var sut = new Application.Courses.Validators.CreateAcademicPlanValidator();
        var result = sut.Validate(new CreateAcademicPlanRequest
        {
            StructureNodeId = Guid.NewGuid(),
            Name = "Plan",
            EffectiveFrom = new DateTime(2030, 1, 1),
            EffectiveTo = new DateTime(2029, 1, 1),
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == LocalizedKeys.Courses.EffectiveToAfterFrom);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static (CourseService sut, Mock<ICourseRepository> repo) BuildCourseService()
    {
        var uow = new Mock<IUnitOfWork>();
        var courses = new Mock<ICourseRepository>();
        var createV = new Mock<IValidator<CreateCourseRequest>>();
        createV.Setup(v => v.ValidateAsync(It.IsAny<CreateCourseRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ValidationResult());
        var updateV = new Mock<IValidator<(Guid, UpdateCourseRequest)>>();
        updateV.Setup(v => v.ValidateAsync(It.IsAny<(Guid, UpdateCourseRequest)>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ValidationResult());
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<CourseResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((CourseResponse?)null);

        var sut = new CourseService(uow.Object, courses.Object, createV.Object, updateV.Object, cache.Object);
        return (sut, courses);
    }

    private static (SemesterService sut, Mock<IAcademicYearRepository> years, Mock<ISemesterRepository> semesters) BuildSemesterService()
    {
        var uow = new Mock<IUnitOfWork>();
        var years = new Mock<IAcademicYearRepository>();
        var semesters = new Mock<ISemesterRepository>();
        uow.Setup(u => u.AcademicYears).Returns(years.Object);
        uow.Setup(u => u.Semesters).Returns(semesters.Object);
        var createV = new Mock<IValidator<CreateSemesterRequest>>();
        createV.Setup(v => v.ValidateAsync(It.IsAny<CreateSemesterRequest>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ValidationResult());
        var updateV = new Mock<IValidator<(Guid, UpdateSemesterRequest)>>();
        updateV.Setup(v => v.ValidateAsync(It.IsAny<(Guid, UpdateSemesterRequest)>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync(new ValidationResult());

        var sut = new SemesterService(uow.Object, createV.Object, updateV.Object);
        return (sut, years, semesters);
    }
}
