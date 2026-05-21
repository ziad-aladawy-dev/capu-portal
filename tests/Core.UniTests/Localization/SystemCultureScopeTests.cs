using System.Globalization;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Localization;

/// <summary>
/// Pins the contract of <see cref="SystemCultureScope"/>: the scope swaps
/// <see cref="CultureInfo.CurrentCulture"/> + <see cref="CultureInfo.CurrentUICulture"/>
/// for the duration of a <c>using</c> block and restores the previous values
/// on dispose, including across <c>await</c> boundaries (since culture
/// flows with the ExecutionContext in .NET).
/// </summary>
public class SystemCultureScopeTests
{
    [Fact]
    public void Scope_SetsCurrentAndUiCulture_RestoresOnDispose()
    {
        var before = CultureInfo.CurrentCulture;
        var beforeUi = CultureInfo.CurrentUICulture;

        using (new SystemCultureScope("en"))
        {
            CultureInfo.CurrentCulture.Name.Should().Be("en");
            CultureInfo.CurrentUICulture.Name.Should().Be("en");
        }

        CultureInfo.CurrentCulture.Should().BeSameAs(before, "scope must restore the exact previous instance");
        CultureInfo.CurrentUICulture.Should().BeSameAs(beforeUi);
    }

    [Fact]
    public void EnglishAndArabicFactories_ResolveExpectedCultures()
    {
        using (SystemCultureScope.English())
        {
            CultureInfo.CurrentCulture.Name.Should().Be("en");
            CultureInfo.CurrentUICulture.Name.Should().Be("en");
        }

        using (SystemCultureScope.Arabic())
        {
            CultureInfo.CurrentCulture.Name.Should().Be("ar");
            CultureInfo.CurrentUICulture.Name.Should().Be("ar");
        }
    }

    [Fact]
    public async Task Scope_SurvivesAcrossAwaits()
    {
        // CultureInfo.CurrentCulture flows with the AsyncLocal-backed
        // ExecutionContext in modern .NET — this test catches a regression
        // where someone changes the scope to set a thread-static instead.
        using (new SystemCultureScope("en"))
        {
            await Task.Yield();
            CultureInfo.CurrentCulture.Name.Should().Be("en");
            await Task.Delay(1);
            CultureInfo.CurrentUICulture.Name.Should().Be("en");
        }
    }

    [Fact]
    public async Task NestedScopes_RestoreInLifoOrder()
    {
        using (new SystemCultureScope("en"))
        {
            CultureInfo.CurrentCulture.Name.Should().Be("en");
            using (new SystemCultureScope("ar"))
            {
                await Task.Yield();
                CultureInfo.CurrentCulture.Name.Should().Be("ar");
            }
            // inner scope disposed — outer "en" must be back
            CultureInfo.CurrentCulture.Name.Should().Be("en");
        }
    }

    [Fact]
    public void DoubleDispose_IsNoOp()
    {
        var before = CultureInfo.CurrentCulture;
        var scope = new SystemCultureScope("en");
        scope.Dispose();
        var afterFirstDispose = CultureInfo.CurrentCulture;
        scope.Dispose(); // second call must not re-clobber state

        CultureInfo.CurrentCulture.Should().BeSameAs(afterFirstDispose);
        CultureInfo.CurrentCulture.Should().BeSameAs(before);
    }

    [Fact]
    public void NullCultureName_Throws()
    {
        // Constructor must reject null cleanly — silently accepting and
        // resolving to the invariant culture would mask a configuration bug.
        var act = () => new SystemCultureScope((string)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Scope_DoesNotMutateDefaultThreadCulture()
    {
        // Process-wide defaults must stay untouched — only the executing
        // logical flow's culture changes. A future hosted service starting
        // a fresh thread should still see whatever the host configured.
        var defaultBefore = CultureInfo.DefaultThreadCurrentCulture;
        var defaultUiBefore = CultureInfo.DefaultThreadCurrentUICulture;

        using (new SystemCultureScope("en"))
        {
            CultureInfo.DefaultThreadCurrentCulture.Should().BeSameAs(defaultBefore);
            CultureInfo.DefaultThreadCurrentUICulture.Should().BeSameAs(defaultUiBefore);
        }
    }
}
