using System.Net;
using System.Text;
using System.Text.Json;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.Exceptions;

/// <summary>
/// Behavioural tests for the Runtime Hardening Plan §1 / §2 / §4 changes to
/// <see cref="GlobalExceptionHandler"/>. Constructs a tiny in-memory service
/// provider with a real <see cref="LocalizationService"/> so we exercise the
/// real translation tables, not a hand-rolled mock.
/// </summary>
public class GlobalExceptionHandlerTests
{
    private sealed class StubCulture : ICurrentCultureService
    {
        public string Language { get; set; } = "en";
    }

    private static (DefaultHttpContext ctx, MemoryStream body, RecordingLogger logger, StubCulture culture)
        BuildContext(string method = "POST", string path = "/api/something")
    {
        var services = new ServiceCollection();
        var culture = new StubCulture();
        services.AddSingleton<ICurrentCultureService>(culture);
        services.AddSingleton<ILocalizationService>(sp => new LocalizationService(
            sp.GetRequiredService<ICurrentCultureService>(),
            NullLogger<LocalizationService>.Instance));
        var provider = services.BuildServiceProvider();

        var body = new MemoryStream();
        var ctx = new DefaultHttpContext
        {
            RequestServices = provider,
            TraceIdentifier = "trace-0001",
        };
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Response.Body = body;
        return (ctx, body, new RecordingLogger(), culture);
    }

    private static async Task<ProblemDetails> Read(MemoryStream body)
    {
        body.Position = 0;
        using var reader = new StreamReader(body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ProblemDetails>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [Fact]
    public async Task ValidationException_LogsAsWarning_Not_Error()
    {
        var (ctx, body, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        var ex = new ValidationException("Name", "required");

        var handled = await handler.TryHandleAsync(ctx, ex, default);

        handled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Warning,
            "§4.1 — client-originated exceptions must not pollute Error telemetry");
    }

    [Theory]
    [InlineData(typeof(NotFoundException), HttpStatusCode.NotFound)]
    [InlineData(typeof(ConflictException), HttpStatusCode.Conflict)]
    public async Task ClientOriginatedException_LogsAsWarning(Type exceptionType, HttpStatusCode expectedStatus)
    {
        var (ctx, _, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        var ex = (Exception)Activator.CreateInstance(exceptionType, new object[] { "msg" })!;

        await handler.TryHandleAsync(ctx, ex, default);

        ctx.Response.StatusCode.Should().Be((int)expectedStatus);
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task UnexpectedException_StillLogsAsError()
    {
        var (ctx, _, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        await handler.TryHandleAsync(ctx, new InvalidOperationException("boom"), default);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
        logger.Entries.Should().ContainSingle();
        logger.Entries[0].Level.Should().Be(LogLevel.Error,
            "§4.1 — unexpected exceptions must still log at Error so on-call sees them");
    }

    [Fact]
    public async Task ValidationException_DetailIsLocalized_WhenMessageIsKey()
    {
        var (ctx, body, logger, culture) = BuildContext();
        culture.Language = "en";
        var handler = new GlobalExceptionHandler(logger);

        // Message is a localization key — should be translated to the English value.
        var ex = new ValidationException("Code", LocalizedKeys.Courses.CodeInUse);

        await handler.TryHandleAsync(ctx, ex, default);

        var pd = await Read(body);
        pd.Status.Should().Be((int)HttpStatusCode.BadRequest);
        pd.Extensions.Should().ContainKey("errors");

        // The Detail itself defaults to the *exception message* — since that is a
        // known localization key, it should be resolved on the way out.
        // ResolveDetail prefers the message; only falls back to fallbackKey on empty.
        // ex.Message here is the ValidationException's default base message,
        // since constructing with (property, message) only populates Errors.
        var errorsJson = pd.Extensions["errors"]!.ToString()!;
        errorsJson.Should().Contain("already in use", "the error-array message must be localized to English");
    }

    [Fact]
    public async Task ValidationException_ErrorArray_LocalizedInArabicCulture()
    {
        var (ctx, body, logger, culture) = BuildContext();
        culture.Language = "ar";
        var handler = new GlobalExceptionHandler(logger);

        var ex = new ValidationException("Code", LocalizedKeys.Courses.CodeInUse);

        await handler.TryHandleAsync(ctx, ex, default);

        var pd = await Read(body);
        var errorsJson = pd.Extensions["errors"]!.ToString()!;
        errorsJson.Should().Contain("كود المقرر",
            "Arabic culture must surface the Arabic translation of the localization key");
    }

    [Fact]
    public async Task ValidationException_LiteralMessage_PassesThroughUntouched()
    {
        var (ctx, body, logger, culture) = BuildContext();
        culture.Language = "en";
        var handler = new GlobalExceptionHandler(logger);

        // A non-key literal — should not be mangled.
        var ex = new ValidationException("Field", "Custom literal message");

        await handler.TryHandleAsync(ctx, ex, default);

        var pd = await Read(body);
        var errorsJson = pd.Extensions["errors"]!.ToString()!;
        errorsJson.Should().Contain("Custom literal message");
    }

    [Fact]
    public async Task NotFoundException_WithLocalizedMessage_DetailIsTranslated()
    {
        var (ctx, body, logger, culture) = BuildContext();
        culture.Language = "en";
        var handler = new GlobalExceptionHandler(logger);

        var ex = new NotFoundException(LocalizedKeys.Courses.NotFound);

        await handler.TryHandleAsync(ctx, ex, default);

        var pd = await Read(body);
        pd.Status.Should().Be((int)HttpStatusCode.NotFound);
        pd.Detail.Should().Be("Course not found.",
            "Detail must resolve the localization key into the current culture's translation");
    }

    [Fact]
    public async Task DbUpdateException_UniqueViolation_MapsToConflict()
    {
        var (ctx, body, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        var ex = new DbUpdateException("db update failed", new FakeSqlException(2627));

        await handler.TryHandleAsync(ctx, ex, default);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        var pd = await Read(body);
        pd.Title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DbUpdateException_UniqueIndex_MapsToConflict()
    {
        var (ctx, body, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        var ex = new DbUpdateException("db", new FakeSqlException(2601));

        await handler.TryHandleAsync(ctx, ex, default);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DbUpdateException_ForeignKeyViolation_MapsToBadRequest()
    {
        var (ctx, _, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        var ex = new DbUpdateException("db", new FakeSqlException(547));

        await handler.TryHandleAsync(ctx, ex, default);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DbUpdateException_UnknownSqlNumber_StillBecomes500()
    {
        var (ctx, _, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        var ex = new DbUpdateException("db", new FakeSqlException(9999));

        await handler.TryHandleAsync(ctx, ex, default);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_StillMapsToConflict()
    {
        var (ctx, _, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        await handler.TryHandleAsync(ctx, new DbUpdateConcurrencyException("conflict"), default);

        ctx.Response.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TraceIdentifierIsAlwaysIncluded()
    {
        var (ctx, body, logger, _) = BuildContext();
        var handler = new GlobalExceptionHandler(logger);

        await handler.TryHandleAsync(ctx, new NotFoundException("x"), default);

        var pd = await Read(body);
        pd.Extensions.Should().ContainKey("traceId");
        pd.Extensions["traceId"]!.ToString().Should().Be("trace-0001");
    }

    private sealed class RecordingLogger : ILogger<GlobalExceptionHandler>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    /// <summary>
    /// Stand-in for Microsoft.Data.SqlClient.SqlException. The handler unwraps the
    /// inner exception via reflection on the <c>Number</c> property; any type with
    /// a <c>Number</c> getter satisfies the duck-typed contract.
    /// </summary>
    private sealed class FakeSqlException : Exception
    {
        public FakeSqlException(int number) : base("fake sql") { Number = number; }
        public int Number { get; }
    }
}