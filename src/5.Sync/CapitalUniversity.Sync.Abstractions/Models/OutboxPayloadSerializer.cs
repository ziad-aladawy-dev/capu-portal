using System.Text.Json;
using System.Text.Json.Serialization;

namespace CapitalUniversity.Sync.Abstractions.Models;

/// <summary>
/// Strict System.Text.Json serializer used by every module's outbox push flow.
/// Two guarantees that every outbox needs identically:
///
/// <list type="number">
///   <item>
///     <see cref="JsonSerializerOptions.UnmappedMemberHandling"/> = <c>Disallow</c> —
///     a JSON field not declared on the payload DTO throws
///     <see cref="JsonException"/>. Catches the "added a column upstream, mapper
///     wasn't updated" scenario as a loud failure (outbox row stays Pending with
///     descriptive LastError) instead of silently pushing truncated data.
///   </item>
///   <item>
///     All <c>required</c> properties on the DTO must appear in the JSON; missing
///     fields throw at deserialization. The <c>required</c> modifier on the DTO's
///     init-only properties triggers this for free.
///   </item>
/// </list>
///
/// <para>
/// Lifted here from per-module copies so every module shares one serializer
/// configuration. Schema versioning is still done by the per-module mapper
/// (compares the outbox row's <c>PayloadSchemaVersion</c> column against the
/// module's <c>CurrentPayloadSchemaVersion</c> constant before deserialising).
/// </para>
/// </summary>
public static class OutboxPayloadSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static string Serialize<TPayload>(TPayload payload) where TPayload : notnull
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.Serialize(payload, Options);
    }

    public static TPayload Deserialize<TPayload>(string json) where TPayload : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<TPayload>(json, Options)
            ?? throw new InvalidOperationException("Outbox payload deserialized to null.");
    }
}