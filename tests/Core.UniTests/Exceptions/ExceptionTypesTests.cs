using CapitalUniversity.Core.Domain.Common.Exceptions;
using Xunit;
using AppValidationException = CapitalUniversity.Core.Application.Exceptions.ValidationException;
using DomainValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.Exceptions;

public class ExceptionTypesTests
{
    private sealed class TestAppException : AppException
    {
        public TestAppException(string message) : base(message) { }
        public TestAppException(string message, Exception inner) : base(message, inner) { }
    }

    [Fact]
    public void AppException_PreservesMessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new TestAppException("outer", inner);

        Assert.Equal("outer", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void AppException_MessageOnly_HasNoInner()
    {
        var ex = new TestAppException("hello");

        Assert.Equal("hello", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void NotFoundException_FormatsEntityNameAndKey()
    {
        var ex = new NotFoundException("Role", 42);
        Assert.Contains("Role", ex.Message);
        Assert.Contains("42", ex.Message);
    }

    [Fact]
    public void NotFoundException_AcceptsCustomMessage()
    {
        var ex = new NotFoundException("missing");
        Assert.Equal("missing", ex.Message);
    }

    [Fact]
    public void UnauthorizedException_DefaultMessage()
    {
        var ex = new UnauthorizedException();
        Assert.Equal("Unauthorized access.", ex.Message);
    }

    [Fact]
    public void UnauthorizedException_CustomMessage()
    {
        var ex = new UnauthorizedException("nope");
        Assert.Equal("nope", ex.Message);
    }

    [Fact]
    public void ForbiddenException_DefaultMessage()
    {
        var ex = new ForbiddenException();
        Assert.Equal("Forbidden access.", ex.Message);
    }

    [Fact]
    public void ForbiddenException_CustomMessage()
    {
        var ex = new ForbiddenException("denied");
        Assert.Equal("denied", ex.Message);
    }

    [Fact]
    public void AppValidationException_StoresErrorsAndDefaultMessage()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = new[] { "required" },
            ["Email"] = new[] { "bad format", "too long" }
        };

        var ex = new AppValidationException(errors);

        Assert.Equal("One or more validation errors occurred.", ex.Message);
        Assert.Same(errors, ex.Errors);
        Assert.Equal(2, ex.Errors["Email"].Length);
    }

    [Fact]
    public void DomainValidationException_NoArg_EmptyErrors()
    {
        var ex = new DomainValidationException();
        Assert.Empty(ex.Errors);
    }

    [Fact]
    public void DomainValidationException_PropertyAndMessage_BuildsSingletonErrorMap()
    {
        var ex = new DomainValidationException("Name", "required");
        Assert.Single(ex.Errors);
        Assert.Equal("required", ex.Errors["Name"][0]);
    }

    [Fact]
    public void DomainValidationException_CustomMessage_PassesThroughToBase()
    {
        var ex = new DomainValidationException("custom");
        Assert.Equal("custom", ex.Message);
        Assert.Empty(ex.Errors);
    }

    [Fact]
    public void DomainValidationException_ErrorsDictionary_RoundTrips()
    {
        var errors = new Dictionary<string, string[]> { ["A"] = new[] { "x" } };
        var ex = new DomainValidationException(errors);
        Assert.Same(errors, ex.Errors);
    }
}