using System.Reflection;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Application.CrossCutting.Localization;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Localization;

/// <summary>
/// E2 guard: every <c>public const string</c> declared under <see cref="LocalizedKeys"/>
/// (including nested static classes, recursively) must resolve to a translated
/// value — not the key itself — in every shipped culture.
///
/// <para>
/// This catches the common refactor accident of adding a new key constant
/// without registering its translations. Reflection-discovered so the test
/// can never drift out of sync with the catalogue.
/// </para>
/// </summary>
public class LocalizedKeySafetyTests
{
    private static readonly string[] ShippedCultures = { "en", "ar" };

    public static TheoryData<string> AllKeys()
    {
        var data = new TheoryData<string>();
        foreach (var key in EnumerateKeys(typeof(LocalizedKeys)))
        {
            data.Add(key);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(AllKeys))]
    public void EveryDeclaredKey_HasTranslationInEveryShippedCulture(string key)
    {
        foreach (var culture in ShippedCultures)
        {
            var resolved = LocalizedStrings.Resolve(key, culture);
            resolved.Should().NotBe(key,
                $"key '{key}' is declared in LocalizedKeys but has no '{culture}' translation in LocalizedStrings");
            resolved.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void AtLeastOneKeyIsDiscovered_GuardsAgainstReflectionRegression()
    {
        // If the reflection walk silently breaks (e.g. LocalizedKeys gets refactored
        // out of static nested classes), the theory above quietly stops asserting.
        // Pin a floor so that regression is visible.
        EnumerateKeys(typeof(LocalizedKeys)).Should().HaveCountGreaterThan(5);
    }

    private static IEnumerable<string> EnumerateKeys(Type type)
    {
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
        {
            if (field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            {
                var value = (string?)field.GetRawConstantValue();
                if (!string.IsNullOrEmpty(value)) yield return value;
            }
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (var inner in EnumerateKeys(nested)) yield return inner;
        }
    }
}