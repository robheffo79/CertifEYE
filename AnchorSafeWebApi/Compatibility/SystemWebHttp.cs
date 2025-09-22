using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

namespace System.Web.Http;

public interface IHttpActionResult : Microsoft.AspNetCore.Mvc.IActionResult
{
}

public abstract class ApiController : ControllerBase
{
    private LegacyHttpRequestMessage? _requestMessage;

    protected new LegacyHttpRequestMessage Request => _requestMessage ??= HttpRequestMessageFactory.Create(base.HttpContext);

    protected IHttpActionResult ResponseMessage(HttpResponseMessage response) => new ActionResultWrapper(new HttpResponseMessageResult(response));

    protected new IHttpActionResult Ok() => new ActionResultWrapper(base.Ok());

    protected new IHttpActionResult Ok<T>(T value) => new ActionResultWrapper(base.Ok(value));

    protected new IHttpActionResult BadRequest() => new ActionResultWrapper(base.BadRequest());

    protected new IHttpActionResult BadRequest(string error) => new ActionResultWrapper(base.BadRequest(error));

    protected new IHttpActionResult BadRequest(object error) => new ActionResultWrapper(base.BadRequest(error));

    protected IHttpActionResult StatusCode(HttpStatusCode statusCode) => new ActionResultWrapper(base.StatusCode((int)statusCode));

    protected IHttpActionResult StatusCode(HttpStatusCode statusCode, object value) => new ActionResultWrapper(base.StatusCode((int)statusCode, value));
}

internal sealed class ActionResultWrapper : IHttpActionResult
{
    private readonly IActionResult _inner;

    public ActionResultWrapper(IActionResult inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task ExecuteResultAsync(ActionContext context) => _inner.ExecuteResultAsync(context);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = true)]
public sealed class AllowAnonymousAttribute : Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HttpGetAttribute : Microsoft.AspNetCore.Mvc.HttpGetAttribute
{
    public HttpGetAttribute()
    {
    }

    public HttpGetAttribute(string template) : base(template)
    {
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HttpPostAttribute : Microsoft.AspNetCore.Mvc.HttpPostAttribute
{
    public HttpPostAttribute()
    {
    }

    public HttpPostAttribute(string template) : base(template)
    {
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HttpPutAttribute : Microsoft.AspNetCore.Mvc.HttpPutAttribute
{
    public HttpPutAttribute()
    {
    }

    public HttpPutAttribute(string template) : base(template)
    {
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HttpDeleteAttribute : Microsoft.AspNetCore.Mvc.HttpDeleteAttribute
{
    public HttpDeleteAttribute()
    {
    }

    public HttpDeleteAttribute(string template) : base(template)
    {
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class HttpPatchAttribute : Microsoft.AspNetCore.Mvc.HttpPatchAttribute
{
    public HttpPatchAttribute()
    {
    }

    public HttpPatchAttribute(string template) : base(template)
    {
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromBodyAttribute : Microsoft.AspNetCore.Mvc.FromBodyAttribute
{
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
public sealed class FromUriAttribute : Microsoft.AspNetCore.Mvc.FromQueryAttribute
{
    public FromUriAttribute()
    {
    }

    public FromUriAttribute(string name)
    {
        Name = name;
    }
}


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RouteAttribute : Microsoft.AspNetCore.Mvc.RouteAttribute
{
    public RouteAttribute(string template) : base(template)
    {
    }
}

public static class GlobalConfiguration
{
    public static HttpConfiguration Configuration { get; } = new();
}

public sealed class HttpConfiguration
{
    public MediaTypeFormatterCollection Formatters { get; } = new();
}

public sealed class MediaTypeFormatterCollection
{
    public JsonMediaTypeFormatter JsonFormatter { get; } = new();
}

public sealed class JsonMediaTypeFormatter
{
    public JsonSerializerSettings SerializerSettings { get; } = new();
}

internal static class HttpRequestMessageFactory
{
    public static LegacyHttpRequestMessage Create(Microsoft.AspNetCore.Http.HttpContext context)
    {
        var request = context.Request;
        request.EnableBuffering();

        var requestMessage = new LegacyHttpRequestMessage(new HttpMethod(request.Method), request.GetEncodedUrl());

        NonDisposableStreamContent? content = null;
        if ((request.ContentLength ?? 0) > 0 || !string.IsNullOrEmpty(request.ContentType))
        {
            content = new NonDisposableStreamContent(request.Body);
        }

        foreach (var header in request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value))
            {
                content ??= new NonDisposableStreamContent(request.Body);
                content.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value);
            }
        }

        if (content != null)
        {
            requestMessage.Content = content;
        }


        if (!string.IsNullOrEmpty(request.ContentType) && content != null)
        {
            content.Headers.TryAddWithoutValidation("Content-Type", request.ContentType);
        }

        if (request.Headers.TryGetValue("Authorization", out StringValues authorizationValues))
        {
            var authorization = authorizationValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(authorization) && AuthenticationHeaderValue.TryParse(authorization, out var headerValue))
            {
                requestMessage.Headers.Authorization = headerValue;
            }
        }

        if (request.Body.CanSeek)
        {
            request.Body.Position = 0;
        }

        requestMessage.Properties["MS_HttpConfiguration"] = GlobalConfiguration.Configuration;

        return requestMessage;
    }
}

public sealed class LegacyHttpRequestMessage : HttpRequestMessage
{
    public LegacyHttpRequestMessage(HttpMethod method, string requestUri)
        : base(method, requestUri)
    {
    }

    public HttpResponseMessage CreateResponse(HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode);
    }

    public HttpResponseMessage CreateResponse(HttpStatusCode statusCode, object? value)
    {
        var response = new HttpResponseMessage(statusCode);
        if (value != null)
        {
            var content = JsonConvert.SerializeObject(value);
            response.Content = new StringContent(content, Encoding.UTF8, "application/json");
        }

        return response;
    }
}

internal sealed class NonDisposableStreamContent : StreamContent
{
    public NonDisposableStreamContent(Stream stream) : base(stream, 81920)
    {
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(false);
    }
}

internal sealed class HttpResponseMessageResult : IActionResult
{
    private readonly HttpResponseMessage _response;

    public HttpResponseMessageResult(HttpResponseMessage response)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
    }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var httpResponse = context.HttpContext.Response;
        httpResponse.StatusCode = (int)_response.StatusCode;

        foreach (var header in _response.Headers)
        {
            httpResponse.Headers[header.Key] = header.Value.ToArray();
        }

        if (_response.Content != null)
        {
            foreach (var header in _response.Content.Headers)
            {
                if (string.Equals(header.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    httpResponse.ContentType = header.Value.FirstOrDefault();
                }
                else
                {
                    httpResponse.Headers[header.Key] = header.Value.ToArray();
                }
            }

            var contentStream = await _response.Content.ReadAsStreamAsync();
            if (contentStream.CanSeek)
            {
                contentStream.Position = 0;
            }

            await contentStream.CopyToAsync(httpResponse.Body);
        }

        _response.Dispose();
    }
}





public class HttpResponseException : Exception
{
    public HttpResponseException(HttpStatusCode statusCode)
        : this(new HttpResponseMessage(statusCode))
    {
    }

    public HttpResponseException(HttpResponseMessage response)
        : base(response.ReasonPhrase)
    {
        Response = response;
    }

    public HttpResponseMessage Response { get; }
}








