using Microsoft.AspNetCore.Http;

namespace System.Web;

public sealed class HttpContext
{
    private readonly Microsoft.AspNetCore.Http.HttpContext _context;

    private HttpContext(Microsoft.AspNetCore.Http.HttpContext context)
    {
        _context = context;
        Response = new HttpResponse(context.Response);
    }

    public HttpResponse Response { get; }

    internal static IHttpContextAccessor? Accessor { get; private set; }

    public static HttpContext? Current => Accessor?.HttpContext is { } ctx ? new HttpContext(ctx) : null;

    public static void ConfigureAccessor(IHttpContextAccessor accessor)
    {
        Accessor = accessor;
    }
}

public sealed class HttpResponse
{
    private readonly Microsoft.AspNetCore.Http.HttpResponse _response;

    internal HttpResponse(Microsoft.AspNetCore.Http.HttpResponse response)
    {
        _response = response;
    }

    public bool TrySkipIisCustomErrors { get; set; }
}
