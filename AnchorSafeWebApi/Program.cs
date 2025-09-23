using AnchorSafe.API.Compatibility;
using AnchorSafe.API.Helpers;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net.Config;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddHttpContextAccessor();

        var log4netConfigPath = Path.Combine(AppContext.BaseDirectory, "log4net.config");
        if (File.Exists(log4netConfigPath))
        {
            XmlConfigurator.ConfigureAndWatch(new FileInfo(log4netConfigPath));
        }

        builder.Services.AddControllers(options =>
        {
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            options.Conventions.Add(new LegacyApiExplorerConvention());
        }).AddNewtonsoftJson(settings =>
        {
            settings.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AnchorSafe API",
                Version = "v1",
                Description = "Legacy-compatible AnchorSafe API surface"
            });

            options.ResolveConflictingActions(apiDescriptions =>
                apiDescriptions
                    .OrderByDescending(description => description.ParameterDescriptions.Count)
                    .First());

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });

        ConfigurationHelper.Initialize(builder.Configuration);

        var app = builder.Build();

        System.Web.HttpContext.ConfigureAccessor(app.Services.GetRequiredService<IHttpContextAccessor>());

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "AnchorSafe API v1");
            options.RoutePrefix = "swagger";
        });

        app.UseRouting();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "DirectApi",
            pattern: "{controller}/{action}/{id?}");

        app.MapControllerRoute(
            name: "DefaultApi",
            pattern: "{action}/{id?}",
            defaults: new { controller = "Main" });

        app.Run();
    }
}
