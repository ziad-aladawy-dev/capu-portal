using System.Globalization;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using CapitalUniversity.Core.Abstractions.Localization;

namespace CapitalUniversity.Core.CrossCutting.Localization;

public class CurrentCultureService : ICurrentCultureService
{
    private readonly IHttpContextAccessor _http;

    public CurrentCultureService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string Language =>
        _http.HttpContext?.Request.Headers["Accept-Language"].ToString()
        ?? "ar";
}