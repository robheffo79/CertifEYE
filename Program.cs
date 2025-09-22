using System.IO;
using log4net.Config;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var log4netConfigPath = Path.Combine(AppContext.BaseDirectory, "log4net.config");
        if (File.Exists(log4netConfigPath))
        {
            XmlConfigurator.ConfigureAndWatch(new FileInfo(log4netConfigPath));
        }

        builder.Services.AddControllers(options =>
        {
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        }).AddNewtonsoftJson(settings =>
        {
            settings.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

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
