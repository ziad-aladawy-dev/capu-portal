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
    public void NullCultureName_ThrowsArgumentNullException_WithExpectedParamName()
    {
        // Constructor must reject null cleanly — silently accepting and
        // resolving to the invariant culture would mask a configuration bug.
        // ParamName check distinguishes our explicit "?? throw" from
        // CultureInfo.GetCultureInfo's own internal null guard (whose
        // ParamName is "name"). Without the assertion, a mutation that
        // removes our "?? throw" would survive because GetCultureInfo throws
        // the same exception type — only the ParamName tells them apart.
        var act = () => new SystemCultureScope((string)null!);
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("cultureName");
    }

    [Fact]
    public void NullCultureInfo_ThrowsArgumentNullException()
    {
        // The (CultureInfo) ctor's ArgumentNullException.ThrowIfNull guard is
        // a separate code path from the (string) ctor — exercise it directly
        // so a mutation that removes the ThrowIfNull call surfaces.
        var act = () => new SystemCultureScope((CultureInfo)null!);
        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("culture");
    }

    [Fact]
    public void DoubleDispose_DoesNotClobberLaterCultureChanges()
    {
        // Stronger version of DoubleDispose_IsNoOp: by mutating CurrentCulture
        // between the two Dispose calls, we make the "is the early-return
        // guard present?" question observable. With the guard, the second
        // Dispose is a true no-op and our intervening "fr" stays. Without
        // the guard, the second Dispose re-runs "CurrentCulture =
        // _previousCulture" and clobbers our change.
        var before = CultureInfo.CurrentCulture;
        try
        {
            var scope = new SystemCultureScope("en");
            scope.Dispose();

            var interim = CultureInfo.GetCultureInfo("fr");
            CultureInfo.CurrentCulture = interim;
            CultureInfo.CurrentUICulture = interim;

            scope.Dispose(); // must be a true no-op

            CultureInfo.CurrentCulture.Should().BeSameAs(interim,
                "the second Dispose must NOT re-restore — that would clobber unrelated, later culture changes");
            CultureInfo.CurrentUICulture.Should().BeSameAs(interim);
        }
        finally
        {
            // Restore for test isolation — the parallel xUnit runner shares
            // the ExecutionContext's culture surface across collections.
            CultureInfo.CurrentCulture = before;
            CultureInfo.CurrentUICulture = before;
        }
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