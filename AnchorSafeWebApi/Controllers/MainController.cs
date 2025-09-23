using AnchorSafe.API.Helpers;
using AnchorSafe.Data;
using AnchorSafe.SimPro;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace AnchorSafe.API.Controllers
{
    /// <summary>
    /// Supplies core AnchorSafe operational endpoints.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiExplorerSettings(IgnoreApi = false)]
    public class MainController : ApiController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly IConfiguration configuration;
        private readonly SimProSettings simProSettings;

        public MainController()
        {
            configuration = ConfigurationHelper.Configuration;
            simProSettings = new SimProSettings
            {
                Host = configuration["SimPro_API_BaseUrl"] ?? string.Empty,
                Version = configuration["SimPro_API_Version"] ?? string.Empty,
                Key = configuration["SimPro_API_Key"] ?? string.Empty,
                CompanyId = configuration.GetValue<int>("SimPro_API_CompanyId"),
                CachePath = configuration["SimPro_API_CachePath"] ?? string.Empty
            };
        }

        /// <summary>
        /// Simple greeting endpoint.
        /// </summary>
        [HttpGet]
        public Task<IHttpActionResult> Hello()
        {
            log.Info("Entering Hello()");
            IHttpActionResult result = Ok("Hi There");
            log.Info("Exiting Hello() with \"Hi There\"");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Checks connectivity to SimPro API and database.
        /// </summary>
        [HttpGet]
        public async Task<HttpResponseMessage> HealthCheck()
        {
            log.Info("Entering HealthCheck()");
            Boolean simProOk = false;
            Boolean dbOk = false;

            // Test SimPro connectivity
            try
            {
                log.Debug("HealthCheck | Testing SimProProvider");
                simProOk = new SimProProvider(simProSettings).Test();
                log.Info($"HealthCheck | SimProOnline = {simProOk}");
            }
            catch (Exception ex)
            {
                simProOk = false;
                log.Warn("HealthCheck | SimProProvider test failed", ex);
            }

            // Test database connectivity
            try
            {
                log.Debug("HealthCheck | Testing database");
                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    db.Configuration.LazyLoadingEnabled = false;
                    dbOk = (await db.Inspections.CountAsync()) > 0;
                    log.Info($"HealthCheck | DatabaseOnline = {dbOk}");
                }
            }
            catch (Exception ex)
            {
                dbOk = false;
                log.Warn("HealthCheck | Database test failed", ex);
            }

            var body = new
            {
                SimProOnline = simProOk,
                DatabaseOnline = dbOk
            };

            HttpResponseMessage response;
            if (!dbOk)
            {
                response = Request.CreateResponse(HttpStatusCode.InternalServerError, body);
                System.Web.HttpContext.Current.Response.TrySkipIisCustomErrors = true;
                log.Warn("HealthCheck | Returning 500 InternalServerError");
            }
            else
            {
                response = Request.CreateResponse(HttpStatusCode.OK, body);
                log.Info("HealthCheck | Returning 200 OK");
            }

            log.Info("Exiting HealthCheck()");
            return response;
        }

        /// <summary>
        /// Returns this assembly's version.
        /// </summary>
        [HttpGet]
        public Task<IHttpActionResult> Version()
        {
            log.Info("Entering Version()");
            String version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            log.Debug($"Version found: {version}");
            IHttpActionResult result = Ok(version);
            log.Info($"Exiting Version() with version \"{version}\"");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Returns the latest mobile app version based on User-Agent.
        /// </summary>
        [HttpGet]
        public Task<IHttpActionResult> GetLatestAppVersion()
        {
            log.Info("Entering GetLatestAppVersion()");
            Request.Headers.TryGetValues("User-Agent", out IEnumerable<String> userAgentHeaders);
            String version = String.Empty;

            if (userAgentHeaders != null)
            {
                String ua = userAgentHeaders.FirstOrDefault() ?? String.Empty;
                log.Debug($"User-Agent header: {ua}");
                if (ua.Contains("iOS"))
                {
                    version = configuration["AS_App_Latest_iOS"] ?? string.Empty;
                }
                else if (ua.Contains("Android"))
                {
                    version = configuration["AS_App_Latest_Android"] ?? string.Empty;
                }
                log.Info($"GetLatestAppVersion | Determined version = {version}");
            }
            else
            {
                log.Warn("GetLatestAppVersion | No User-Agent header provided");
            }

            IHttpActionResult result = Ok(version);
            log.Info($"Exiting GetLatestAppVersion() with version \"{version}\"");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Default GET endpoint.
        /// </summary>
        public IEnumerable<String> Get()
        {
            log.Info("Entering Get()");
            IEnumerable<String> values = new String[] { "value1", "value2" };
            log.Info("Exiting Get()");
            return values;
        }

        /// <summary>
        /// Default GET by id endpoint.
        /// </summary>
        public String Get(Int32 id)
        {
            log.Info($"Entering Get(id={id})");
            String value = "value";
            log.Info($"Exiting Get(id={id}) with \"{value}\"");
            return value;
        }

        /// <summary>
        /// Default POST endpoint.
        /// </summary>
        public void Post([FromBody] String value)
        {
            log.Info($"Entering Post(value={value})");
            // No operation
            log.Info("Exiting Post()");
        }

        /// <summary>
        /// Default PUT endpoint.
        /// </summary>
        public void Put(Int32 id, [FromBody] String value)
        {
            log.Info($"Entering Put(id={id}, value={value})");
            // No operation
            log.Info("Exiting Put()");
        }

        /// <summary>
        /// Default DELETE endpoint.
        /// </summary>
        public void Delete(Int32 id)
        {
            log.Info($"Entering Delete(id={id})");
            // No operation
            log.Info("Exiting Delete()");
        }
    }
}
