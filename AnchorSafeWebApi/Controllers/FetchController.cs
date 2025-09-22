using AnchorSafe.API.Helpers;
using AnchorSafe.API.Models.v2;
using AnchorSafe.API.Services;
using AnchorSafe.Data;
using AnchorSafe.SimPro.DTO.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace AnchorSafe.API.Controllers.v2
{
    /// <summary>
    /// Provides version 2 data export endpoints for external integrations.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiExplorerSettings(IgnoreApi = false)]
    public class FetchController : ApiController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly String connectionString = ConfigurationManager.ConnectionStrings["SiteSqlServer"].ConnectionString;

        private async Task<(Boolean success, IHttpActionResult result, Users user)> TryAuthenticate()
        {
            log.Debug("Entering TryAuthenticate");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved from header.");

            String username = TokenService.ValidateToken(token);
            log.Debug($"Validated token. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("Authentication failed: invalid token and not in dev mode.");
                IHttpActionResult result = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Debug("Exiting TryAuthenticate with failure.");
                return (false, result, null);
            }

            Users user = await Services.UserService.GetUserByUsername(username);
            log.Debug($"User lookup returned: {(user == null ? "null" : user.Id.ToString())}");
            if (user == null || !user.IsAuthorised)
            {
                log.Warn($"Authentication failed: user null or not authorised for username '{username}'.");
                IHttpActionResult result = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Debug("Exiting TryAuthenticate with failure.");
                return (false, result, null);
            }

            log.Debug($"Authentication successful for user ID {user.Id}.");
            log.Debug("Exiting TryAuthenticate with success.");
            return (true, null, user);
        }

        /* 
         * APP END POINTS
        */

        [HttpGet]
        public async Task<IHttpActionResult> CheckDataUpdatesAvailable([FromUri] DateTime? ts)
        {
            if (ts == null)
                ts = new DateTime(2000, 1, 1);

            if (ts < new DateTime(2000, 1, 1) || ts > DateTime.Today.AddDays(7))
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Timestamp is out of range." }));

            ts = CheckForceSync(ts.Value);    // Force sync if active

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (SqlCommand command = new SqlCommand("CheckModifiedSince", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("@UpdatedSince", SqlDbType.DateTime).Value = ts;

                    SqlParameter existsParam = command.Parameters.Add("@Exists", SqlDbType.Bit);
                    existsParam.Direction = ParameterDirection.Output;

                    // Execute the command
                    await command.ExecuteNonQueryAsync();

                    // Retrieve the boolean result from the output parameter
                    bool exists = (bool)existsParam.Value;

                    return ResponseMessage(Request.CreateResponse(exists ? HttpStatusCode.OK : HttpStatusCode.NoContent));
                }
            }
        }

        /// <summary>
        /// Returns an index of counts and pages for all data types changed since the timestamp.
        /// </summary>
        /// <param name="ts">Timestamp to check updates since.</param>
        /// <param name="ps">Page size for paging calculation.</param>
        /// <returns>Compressed JSON result of DataIndex.</returns>
        [HttpGet]
        public async Task<IHttpActionResult> Index([FromUri] DateTime? ts, [FromUri] Int64 ps = 25)
        {
            log.Info("Entering Index");
            log.Debug($"Parameters – ts: {(ts.HasValue ? ts.Value.ToString("O") : "null")}, ps: {ps}");

            (Boolean success, IHttpActionResult result, Users user) authResult = await TryAuthenticate();
            if (!authResult.success)
            {
                log.Warn("Index – authentication failed.");
                return authResult.result;
            }

            if (ts == null)
            {
                log.Debug("Index – timestamp is null. Defaulting to Jan 1, 2000.");
                ts = new DateTime(2000, 1, 1);
            }
            log.Debug($"Index – timestamp parameter: {ts.Value:O}");
            if (ts.Value < new DateTime(2000, 1, 1) || ts.Value > DateTime.Today.AddDays(7))
            {
                log.Warn($"Index – timestamp out of range: {ts.Value:O}");
                IHttpActionResult badTs = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Timestamp is out of range." }));
                log.Info("Exiting Index with BadRequest (timestamp).");
                return badTs;
            }

            ts = CheckForceSync(ts.Value);
            log.Debug($"Index – timestamp after CheckForceSync: {ts:O}");

            if (ps <= 0 || ps > Int32.MaxValue)
            {
                log.Warn($"Index – pageSize out of range: {ps}");
                IHttpActionResult badPs = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "PageSize is out of range." }));
                log.Info("Exiting Index with BadRequest (pageSize).");
                return badPs;
            }

            DataIndex result = new DataIndex()
            {
                Timestamp = ts.Value,
                PageSize = ps
            };

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                log.Debug("Index – querying database for counts.");

                //IQueryable<Inspections> inspectionsQ = db.Inspections.Where(x => x.DateModified >= ts && !x.IsDeleted)
                //                                                      .OrderBy(x => x.Id)
                //                                                      .Include(i => i.InspectionItems);
                IQueryable<Definitions> definitionsQ = db.Definitions.Where(x => x.DateModified >= ts)
                                                                      .OrderBy(x => x.Id);
                IQueryable<Categories> categoriesQ = db.Categories.Where(x => x.DateModified >= ts)
                                                                   .OrderBy(x => x.Id);
                IQueryable<Clients> clientsQ = db.Clients.Where(x => x.DateModified >= ts)
                                                         .OrderBy(x => x.Id);
                IQueryable<Sites> sitesQ = db.Sites.Where(x => x.DateModified >= ts)
                                                   .OrderBy(x => x.Id);
                IQueryable<Locations> locationsQ = db.Locations.Where(x => x.DateModified >= ts)
                                                                .OrderBy(x => x.Id);

                result.Inspections.Count = 0; // await inspectionsQ.LongCountAsync();
                result.Definitions.Count = await definitionsQ.LongCountAsync();
                result.Categories.Count = await categoriesQ.LongCountAsync();
                result.Clients.Count = await clientsQ.LongCountAsync();
                result.Sites.Count = await sitesQ.LongCountAsync();
                result.Locations.Count = await locationsQ.LongCountAsync();

                log.Debug($"Index – counts: Inspections={result.Inspections.Count}, Definitions={result.Definitions.Count}, Categories={result.Categories.Count}, Clients={result.Clients.Count}, Sites={result.Sites.Count}, Locations={result.Locations.Count}");

                result.Inspections.Pages = 0; // (result.Inspections.Count + ps - 1) / ps;
                result.Definitions.Pages = (result.Definitions.Count + ps - 1) / ps;
                result.Categories.Pages = (result.Categories.Count + ps - 1) / ps;
                result.Clients.Pages = (result.Clients.Count + ps - 1) / ps;
                result.Sites.Pages = (result.Sites.Count + ps - 1) / ps;
                result.Locations.Pages = (result.Locations.Count + ps - 1) / ps;

                String dataPullType = (ts.Value.Year == 2000) ? "Full" : "Refresh";
                log.Info($"Index – User {authResult.user.Id} performed {dataPullType} Index Pull at {ts:O}");
                new ASLogs().AddLogEntry($"{dataPullType} (App) - Index Pull", (Int32)Dashboard.LogType.DataPull, authResult.user.Id);
            }

            GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            log.Info("Exiting Index – returning compressed JSON.");
            return await GZJson(result);
        }

        /// <summary>
        /// Returns a paged list of inspections since the specified timestamp.
        /// </summary>
        /// <param name="ts">Timestamp to check updates since.</param>
        /// <param name="p">Page number (zero-based).</param>
        /// <param name="ps">Page size.</param>
        /// <returns>Compressed JSON result of DataPage of Inspection DTOs.</returns>
        [HttpGet]
        public async Task<IHttpActionResult> Inspections([FromUri] DateTime? ts, [FromUri] Int64 p = 0, [FromUri] Int64 ps = Int64.MaxValue)
        {
            log.Info("Entering Inspections");
            log.Debug($"Parameters – ts: {(ts.HasValue ? ts.Value.ToString("O") : "null")}, p: {p}, ps: {ps}");

            (Boolean success, IHttpActionResult result, Users user) authResult = await TryAuthenticate();
            if (!authResult.success)
            {
                log.Warn("Inspections – authentication failed.");
                return authResult.result;
            }

            //if (ts == null)
            //{
            //    log.Debug("Inspections – timestamp is null. Defaulting to Jan 1, 2000.");
            //    ts = new DateTime(2000, 1, 1);
            //}
            //log.Debug($"Inspections – timestamp parameter: {ts.Value:O}");
            //if (ts.Value < new DateTime(2000, 1, 1) || ts.Value > DateTime.Today.AddDays(7))
            //{
            //    log.Warn($"Inspections – timestamp out of range: {ts.Value:O}");
            //    IHttpActionResult badTs = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Timestamp is out of range." }));
            //    log.Info("Exiting Inspections with BadRequest (timestamp).");
            //    return badTs;
            //}

            //ts = CheckForceSync(ts.Value);
            //log.Debug($"Inspections – timestamp after CheckForceSync: {ts:O}");

            //if (p < 0)
            //{
            //    log.Warn($"Inspections – page out of range: {p}");
            //    IHttpActionResult badP = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Page is out of range." }));
            //    log.Info("Exiting Inspections with BadRequest (page).");
            //    return badP;
            //}
            //if (ps <= 0 || ps > Int32.MaxValue)
            //{
            //    log.Warn($"Inspections – pageSize out of range: {ps}");
            //    IHttpActionResult badPs = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "PageSize is out of range." }));
            //    log.Info("Exiting Inspections with BadRequest (pageSize).");
            //    return badPs;
            //}

            DataPage<Models.DTO.Inspection> result = null;
            //using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            //{
            //    db.Configuration.LazyLoadingEnabled = false;
            //    log.Debug("Inspections – querying database.");

            //    IQueryable<Inspections> inspectionsQ = db.Inspections.Where(x => x.DateModified >= ts && !x.IsDeleted)
            //                                                          .OrderBy(x => x.Id);
            //    Int64 total = await inspectionsQ.LongCountAsync();
            //    log.Debug($"Inspections – total count: {total}");

            //    IEnumerable<Models.DTO.Inspection> pageItems = (await inspectionsQ.Skip((Int32)(p * ps)).Take((Int32)ps).ToListAsync()).Select(i => new Models.DTO.Inspection(i));

            //    result = new DataPage<Models.DTO.Inspection>(ts.Value, total, p, ps, pageItems);

            //    String dataPullType = (ts.Value.Year == 2000) ? "Full" : "Refresh";
            //    log.Info($"Inspections – User {authResult.user.Id} performed {dataPullType} Data Pull: {p * ps} to {Math.Min((p * ps) + ps, total)} of {total} items.");
            //    new ASLogs().AddLogEntry($"{dataPullType} (App) - {p * ps} to {Math.Min((p * ps) + ps, total)} of {total} Inspection(s)", (Int32)Dashboard.LogType.DataPull, authResult.user.Id);
            //}

            GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            log.Info("Exiting Inspections – returning compressed JSON.");

            result = new DataPage<Models.DTO.Inspection>(ts.Value, 0, p, ps, Array.Empty<Models.DTO.Inspection>());
            return await GZJson(result);
        }

        [HttpGet]
        public async Task<IHttpActionResult> Definitions([FromUri] DateTime? ts, [FromUri] Int64 p = 0, [FromUri] Int64 ps = Int32.MaxValue)
        {
            (Boolean success, IHttpActionResult result, Users user) authResult = await TryAuthenticate();
            if (!authResult.success)
                return authResult.result;

            if (ts == null)
                ts = new DateTime(2000, 1, 1);

            if (ts < new DateTime(2000, 1, 1) || ts > DateTime.Today.AddDays(7))
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Timestamp is out of range." }));

            ts = CheckForceSync(ts.Value);    // Force sync if active

            if (p < 0)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Page is out of range." }));

            if (ps <= 0 || ps > Int32.MaxValue)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "PageSize is out of range." }));

            DataPage<Models.DTO.Definition> result = null;

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;

                IOrderedQueryable<Definitions> definitions = db.Definitions.Where(x => x.DateModified >= ts).OrderBy(x => x.Id);
                Int64 totalDefinitions = await definitions.LongCountAsync();
                result = new DataPage<Models.DTO.Definition>(ts.Value, totalDefinitions, p, ps, (await definitions.Skip((Int32)(p * ps)).Take((Int32)ps).ToListAsync()).Select(i => new Models.DTO.Definition(i)));

                String dataPullType = (ts.Value.Year == 2000) ? "Full Page" : "Refresh Page";
                log.Info($"GetDefinitions | User {authResult.user.Id} | Details: {dataPullType} Data Pull: (Timestamp {ts.Value}) {p * ps} to {Math.Min((p * ps) + ps, totalDefinitions)} of {totalDefinitions} Definition(s)."); /* LOG */
                String asLog = new ASLogs().AddLogEntry($"{dataPullType} (App) - {p * ps} to {Math.Min((p * ps) + ps, totalDefinitions)} of {totalDefinitions} Definition(s)", (int)Dashboard.LogType.DataPull, authResult.user.Id);
            }

            GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            return await GZJson(result);
        }

        [HttpGet]
        public async Task<IHttpActionResult> Categories([FromUri] DateTime? ts, [FromUri] Int64 p = 0, [FromUri] Int64 ps = Int32.MaxValue)
        {
            (Boolean success, IHttpActionResult result, Users user) authResult = await TryAuthenticate();
            if (!authResult.success)
                return authResult.result;

            if (ts == null)
                ts = new DateTime(2000, 1, 1);

            if (ts < new DateTime(2000, 1, 1) || ts > DateTime.Today.AddDays(7))
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Timestamp is out of range." }));

            ts = CheckForceSync(ts.Value);    // Force sync if active

            if (p < 0)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Page is out of range." }));

            if (ps <= 0 || ps > Int32.MaxValue)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "PageSize is out of range." }));

            DataPage<Models.DTO.Category> result = null;

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;

                IOrderedQueryable<Categories> categories = db.Categories.Where(x => /*x.IsActive && */x.DateModified >= ts).OrderBy(x => x.Id);
                Int64 totalCategories = await categories.LongCountAsync();
                result = new DataPage<Models.DTO.Category>(ts.Value, totalCategories, p, ps, (await categories.Skip((Int32)(p * ps)).Take((Int32)ps).ToListAsync()).Select(i => new Models.DTO.Category(i)));

                String dataPullType = (ts.Value.Year == 2000) ? "Full Page" : "Refresh Page";
                log.Info($"GetCategories | User {authResult.user.Id} | Details: {dataPullType} Data Pull: (Timestamp {ts.Value}) {p * ps} to {Math.Min((p * ps) + ps, totalCategories)} of {totalCategories} Category(s)."); /* LOG */
                String asLog = new ASLogs().AddLogEntry($"{dataPullType} (App) - {p * ps} to {Math.Min((p * ps) + ps, totalCategories)} of {totalCategories} Category(s)", (int)Dashboard.LogType.DataPull, authResult.user.Id);
            }

            GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            return await GZJson(result);
        }

        [HttpGet]
        public async Task<IHttpActionResult> Clients([FromUri] DateTime? ts, [FromUri] Int64 p = 0, [FromUri] Int64 ps = Int32.MaxValue)
        {
            (Boolean success, IHttpActionResult result, Users user) authResult = await TryAuthenticate();
            if (!authResult.success)
                return authResult.result;

            if (ts == null)
                ts = new DateTime(2000, 1, 1);

            if (ts < new DateTime(2000, 1, 1) || ts > DateTime.Today.AddDays(7))
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Timestamp is out of range." }));

            ts = CheckForceSync(ts.Value);    // Force sync if active

            if (p < 0)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Page is out of range." }));

            if (ps <= 0 || ps > Int32.MaxValue)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "PageSize is out of range." }));

            DataPage<Models.DTO.Client> result = null;

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;

                IOrderedQueryable<Clients> clients = db.Clients.Where(x => x.DateModified >= ts).OrderBy(x => x.Id);
                Int64 totalClients = await clients.LongCountAsync();
                result = new DataPage<Models.DTO.Client>(ts.Value, totalClients, p, ps, (await clients.Skip((Int32)(p * ps)).Take((Int32)ps).ToListAsync()).Select(i => new Models.DTO.Client(i)));

                String dataPullType = (ts.Value.Year == 2000) ? "Full Page" : "Refresh Page";
                log.Info($"GetClients | User {authResult.user.Id} | Details: {dataPullType} Data Pull: (Timestamp {ts.Value}) {p * ps} to {Math.Min((p * ps) + ps, totalClients)} of {totalClients} Client(s)."); /* LOG */
                String asLog = new ASLogs().AddLogEntry($"{dataPullType} (App) - {p * ps} to {Math.Min((p * ps) + ps, totalClients)} of {totalClients} Client(s)", (int)Dashboard.LogType.DataPull, authResult.user.Id);
            }

            GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            return await GZJson(result);
        }

        [HttpGet]
        public async Task<IHttpActionResult> Sites([FromUri] DateTime? ts, [FromUri] Int64 p = 0, [FromUri] Int64 ps = Int32.MaxValue)
        {
            (Boolean success, IHttpActionResult result, Users user) authResult = await TryAuthenticate();
            if (!authResult.success)
                return authResult.result;

            if (ts == null)
                ts = new DateTime(2000, 1, 1);

            if (ts < new DateTime(2000, 1, 1) || ts > DateTime.Today.AddDays(7))
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Timestamp is out of range." }));

            ts = CheckForceSync(ts.Value);    // Force sync if active

            if (p < 0)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Page is out of range." }));

            if (ps <= 0 || ps > Int32.MaxValue)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "PageSize is out of range." }));

            DataPage<Models.DTO.Site> result = null;

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;

                IOrderedQueryable<Sites> sites = db.Sites.Where(x => x.DateModified >= ts).OrderBy(x => x.Id);
                Int64 totalSites = await sites.LongCountAsync();
                result = new DataPage<Models.DTO.Site>(ts.Value, totalSites, p, ps, (await sites.Skip((Int32)(p * ps)).Take((Int32)ps).ToListAsync()).Select(i => new Models.DTO.Site(i)));

                String dataPullType = (ts.Value.Year == 2000) ? "Full Page" : "Refresh Page";
                log.Info($"GetSites | User {authResult.user.Id} | Details: {dataPullType} Data Pull: (Timestamp {ts.Value}) {p * ps} to {Math.Min((p * ps) + ps, totalSites)} of {totalSites} Site(s)."); /* LOG */
                String asLog = new ASLogs().AddLogEntry($"{dataPullType} (App) - {p * ps} to {Math.Min((p * ps) + ps, totalSites)} of {totalSites} Site(s)", (int)Dashboard.LogType.DataPull, authResult.user.Id);
            }

            GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            return await GZJson(result);
        }

        [HttpGet]
        public async Task<IHttpActionResult> Locations([FromUri] DateTime? ts, [FromUri] Int64 p = 0, [FromUri] Int64 ps = Int32.MaxValue)
        {
            (Boolean success, IHttpActionResult result, Users user) authResult = await TryAuthenticate();
            if (!authResult.success)
                return authResult.result;

            if (ts == null)
                ts = new DateTime(2000, 1, 1);

            if (ts < new DateTime(2000, 1, 1) || ts > DateTime.Today.AddDays(7))
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Timestamp is out of range." }));

            ts = CheckForceSync(ts.Value);    // Force sync if active

            if (p < 0)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Page is out of range." }));

            if (ps <= 0 || ps > Int32.MaxValue)
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "PageSize is out of range." }));

            DataPage<Models.DTO.Location> result = null;

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;

                IOrderedQueryable<Locations> locations = db.Locations.Where(x => x.DateModified >= ts).OrderBy(x => x.Id);
                Int64 totalLocations = await locations.LongCountAsync();
                result = new DataPage<Models.DTO.Location>(ts.Value, totalLocations, p, ps, (await locations.Skip((Int32)(p * ps)).Take((Int32)ps).ToListAsync()).Select(i => new Models.DTO.Location(i)));

                String dataPullType = (ts.Value.Year == 2000) ? "Full Page" : "Refresh Page";
                log.Info($"GetLocations | User {authResult.user.Id} | Details: {dataPullType} Data Pull: (Timestamp {ts.Value}) {p * ps} to {Math.Min((p * ps) + ps, totalLocations)} of {totalLocations} Location(s)."); /* LOG */
                String asLog = new ASLogs().AddLogEntry($"{dataPullType} (App) - {p * ps} to {Math.Min((p * ps) + ps, totalLocations)} of {totalLocations} Location(s)", (int)Dashboard.LogType.DataPull, authResult.user.Id);
            }

            GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            return await GZJson(result);
        }

        /// <summary>
        /// Compresses the provided data to GZip JSON format.
        /// </summary>
        /// <param name="data">Data object to serialize.</param>
        /// <returns>Compressed HTTP response with JSON content.</returns>
        public async Task<IHttpActionResult> GZJson(Object data)
        {
            HttpResponseMessage response = null;

            log.Debug("Entering GZJson serialization.");
            response = await Task.Run(() =>
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    using (GZipStream gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                    using (TextWriter tw = new StreamWriter(gz, Encoding.UTF8, 4096, true))
                    using (JsonTextWriter jtw = new JsonTextWriter(tw))
                    {
                        JsonSerializer js = new JsonSerializer()
                        {
                            Formatting = Formatting.None
                        };
                        log.Debug("Serializing data to JSON.");
                        js.Serialize(jtw, data);
                    }

                    Int32 length = ms.ToArray().Length;
                    log.Debug($"Serialization and compression complete. Byte length: {length}");

                    HttpResponseMessage ourResponse = new HttpResponseMessage(HttpStatusCode.OK);
                    ourResponse.Content = new ByteArrayContent(ms.ToArray());
                    ourResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    ourResponse.Content.Headers.ContentEncoding.Add("gzip");

                    return ourResponse;
                }
            });

            log.Debug("Returning compressed JSON response.");
            return ResponseMessage(response);
        }

        public DateTime CheckForceSync(DateTime ts)
        {
            if (bool.Parse(ConfigurationManager.AppSettings["AS_API_ForceSync"].ToString()))
                DateTime.TryParse(ConfigurationManager.AppSettings["AS_API_ForceSyncDate"].ToString(), out ts);

            return ts;
        }
    }
}

