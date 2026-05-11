using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.Core.Application.CrossCutting.Localization;

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