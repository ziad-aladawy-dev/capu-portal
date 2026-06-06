namespace CapitalUniversity.Sync.Infrastructure.Configuration;

/// <summary>
/// Auth policy options for the sync host's admin endpoints and the Hangfire
/// dashboard. The sync host shares JWT bearer trust with the API (same
/// <c>Jwt</c> config section: issuer / audience / signing key), so an
/// operator's existing API token gates the sync admin surface too — there is
/// no second login flow.
///
/// <para>
/// <see cref="RequiredRole"/> is the role-claim value that callers must carry
/// to pass the <c>SyncAdmin</c> authorization policy. Defaults to
/// <c>"SyncAdmin"</c>; the API's auth controller issues role claims based on
/// the staff record's <c>Role</c> field, so granting a Staff user the
/// <c>SyncAdmin</c> role is the supported onboarding path.
/// </para>
///
/// <para>
/// There is no environment-coupled bypass. Prior versions of this options
/// class carried a <c>DevAllowAnonymous</c> flag gated by
/// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.IsDevelopment"/>;
/// audit finding P0-2 ruled the pattern unacceptable because a stray
/// <c>ASPNETCORE_ENVIRONMENT=Development</c> on a misconfigured image was
/// the single signal between "fully gated" and "open to the world". The
/// policy now requires the role <b>regardless of environment</b>. Local devs
/// authenticate the same way operators do in prod: log in against the API,
/// reuse the JWT here.
/// </para>
/// </summary>
public sealed class SyncAuthOptions
{
    public const string SectionName = "Sync:Auth";

    /// <summary>
    /// Role-claim value enforced by the <c>SyncAdmin</c> policy. Callers
    /// missing this role on their token get 403 from gated endpoints and
    /// are denied entry to the Hangfire dashboard.
    /// </summary>
    public string RequiredRole { get; set; } = "SyncAdmin";
}
