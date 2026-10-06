namespace TokNotch.Infrastructure.Authentication;
public sealed record MimoCookie(string Name,string Value,string Domain,string Path,bool Secure,DateTimeOffset? ExpiresAt);
public sealed record MimoSession(string Header,DateTimeOffset SavedAt,DateTimeOffset? ExpiresAt);
public static class MimoCookiePolicy
{
 public const string Origin="https://platform.xiaomimimo.com";
 public const string UsageUri=Origin+"/api/v1/usage";
 private static readonly HashSet<string> names=new(StringComparer.Ordinal){"api-platform_serviceToken","api-platform_slh","api-platform_ph","userId"};
 public static bool IsLoginNavigation(string uri)=>Uri.TryCreate(uri,UriKind.Absolute,out var parsed)&&parsed.Scheme==Uri.UriSchemeHttps&&new[]{"xiaomimimo.com","xiaomi.com","mi.com"}.Any(domain=>parsed.Host==domain||parsed.Host.EndsWith("."+domain,StringComparison.OrdinalIgnoreCase));
 public static MimoSession? Extract(IEnumerable<MimoCookie> cookies,DateTimeOffset now)
 {
  var applicable=cookies.Where(c=>names.Contains(c.Name)&&!string.IsNullOrWhiteSpace(c.Value)&&c.Value.Length<=16384&&!c.Value.Any(ch=>ch is '\r' or '\n' or ';')&&(!c.ExpiresAt.HasValue||c.ExpiresAt>now)&&DomainMatches(c.Domain)&&PathMatches(c.Path))
   .OrderByDescending(c=>c.Path.Length).ThenByDescending(c=>c.Domain.Length).DistinctBy(c=>c.Name).OrderBy(c=>c.Name,StringComparer.Ordinal).ToArray();
  if(!applicable.Any(c=>c.Name=="api-platform_serviceToken"))return null;
  var header=string.Join("; ",applicable.Select(c=>c.Name+"="+c.Value));var expiry=applicable.Where(c=>c.ExpiresAt.HasValue).Select(c=>c.ExpiresAt!.Value).ToArray();return new(header,now,expiry.Length>0?expiry.Min():null);
 }
 private static bool DomainMatches(string domain){var value=domain.TrimStart('.');return value.Equals("platform.xiaomimimo.com",StringComparison.OrdinalIgnoreCase)||value.Equals("xiaomimimo.com",StringComparison.OrdinalIgnoreCase);}
 private static bool PathMatches(string path)=>path=="/"||path=="/api"||path=="/api/"||path=="/api/v1"||path=="/api/v1/"||path=="/api/v1/usage";
}

