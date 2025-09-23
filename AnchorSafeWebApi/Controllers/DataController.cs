using AnchorSafe.API.Helpers;
using AnchorSafe.API.Services;
using AnchorSafe.Data;
using Antlr.Runtime;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Http;

namespace AnchorSafe.API.Controllers
{
    /// <summary>
    /// Exposes data retrieval and synchronisation endpoints used by the mobile application.
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ApiExplorerSettings(IgnoreApi = false)]
    public class DataController : ApiController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly IConfiguration configuration;

        public DataController()
        {
            configuration = ConfigurationHelper.Configuration;
        }

        /// <summary>
        /// Health check endpoint for DataController.
        /// </summary>
        [HttpGet]
        public Task<IHttpActionResult> Hello()
        {
            log.Info("Entering Hello()");
            IHttpActionResult response = Ok("Hi Data");
            log.Info("Exiting Hello()");
            return Task.FromResult(response);
        }

        /// <summary>
        /// Returns the current assembly version.
        /// </summary>
        [HttpGet]
        public Task<IHttpActionResult> Version()
        {
            log.Info("Entering Version()");
            String version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            log.Debug($"Version found: {version}");
            IHttpActionResult response = Ok(version);
            log.Info("Exiting Version()");
            return Task.FromResult(response);
        }

        /* 
         * APP END POINTS
         */

        /// <summary>
        /// Retrieves all application data (full pull).
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> AppDataPullAll()
        {
            log.Info("Entering AppDataPullAll() overload");
            IHttpActionResult response = await AppDataPullAll(String.Empty);
            log.Info("Exiting AppDataPullAll() overload");
            return response;
        }

        /// <summary>
        /// Retrieves all application data modified since the given timestamp.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> AppDataPullAll(String ts)
        {
            log.Info("Entering AppDataPullAll(ts)");
            log.Debug($"Parameter ts=\"{ts}\"");

            // Security
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for AppDataPullAll");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("AppDataPullAll | Access Denied (authentication)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppDataPullAll(ts) with Forbidden");
                return forbidden;
            }

            Users user = await Services.UserService.GetUserByUsername(username);
            log.Debug($"User lookup returned: {(user == null ? "null" : user.Id.ToString())}");
            if (user == null || !user.IsAuthorised)
            {
                log.Warn("AppDataPullAll | Access Denied (authorization)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppDataPullAll(ts) with Forbidden");
                return forbidden;
            }

            Models.DataPackage data = new API.Models.DataPackage();
            String debugDump = String.Empty;

            try
            {
                DateTime timestamp = new DateTime(2000, 1, 1);
                if (!String.IsNullOrWhiteSpace(ts))
                {
                    Boolean parsed = DateTime.TryParse(ts, out timestamp);
                    log.Debug($"Parsed ts to timestamp={timestamp:O}, success={parsed}");
                }

                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    db.Configuration.LazyLoadingEnabled = false;
                    log.Debug("Querying database for AppDataPullAll");

                    // Build your base queries
                    IQueryable<Inspections> inspectionsQ = db.Inspections.Where(x => x.DateModified >= timestamp && !x.IsDeleted).Include(i => i.InspectionItems);
                    IQueryable<Definitions> definitionsQ = db.Definitions.Where(x => x.IsActive && x.DateModified >= timestamp).Include(d => d.DefinitionTypes).Include(d => d.Categories);
                    IQueryable<Categories> categoriesQ = db.Categories.Where(x => x.IsActive && x.DateModified >= timestamp);
                    IQueryable<Clients> clientsQ = db.Clients.Where(x => x.DateModified >= timestamp).Include(c => c.Sites);
                    IQueryable<Sites> sitesQ = db.Sites.Where(x => x.DateModified >= timestamp).Include(s => s.Locations);
                    IQueryable<Locations> locationsQ = db.Locations.Where(x => x.DateModified >= timestamp);

                    // Fire off all six queries in parallel
                    Task<List<Inspections>> inspectionsTask = inspectionsQ.ToListAsync();
                    Task<List<Definitions>> definitionsTask = definitionsQ.ToListAsync();
                    Task<List<Categories>> categoriesTask = categoriesQ.ToListAsync();
                    Task<List<Clients>> clientsTask = clientsQ.ToListAsync();
                    Task<List<Sites>> sitesTask = sitesQ.ToListAsync();
                    Task<List<Locations>> locationsTask = locationsQ.ToListAsync();

                    await Task.WhenAll(inspectionsTask, definitionsTask, categoriesTask, clientsTask, sitesTask, locationsTask);

                    // Map to DTOs in memory
                    data.Inspections.AddRange(inspectionsTask.Result.Select((Inspections insp) => new Models.DTO.Inspection(insp)));
                    data.Definitions.AddRange(definitionsTask.Result.Select((Definitions def) => new Models.DTO.Definition(def)));
                    data.Categories.AddRange(categoriesTask.Result.Select((Categories cat) => new Models.DTO.Category(cat)));
                    data.Clients.AddRange(clientsTask.Result.Select((Clients cli) => new Models.DTO.Client(cli)));
                    data.Sites.AddRange(sitesTask.Result.Select((Sites site) => new Models.DTO.Site(site)));
                    data.Locations.AddRange(locationsTask.Result.Select((Locations loc) => new Models.DTO.Location(loc)));

                    Int32 countClients = data.Clients.Count();
                    Int32 countSites = data.Sites.Count();
                    Int32 countLocations = data.Locations.Count();
                    Int32 countInspections = data.Inspections.Count();
                    Int32 countDefinitions = data.Definitions.Count();
                    Int32 countCategories = data.Categories.Count();

                    String dataPullType = (timestamp.Year == 2000) ? "Full" : "Refresh";
                    log.Info($"AppDataPullAll | User {user.Id} | Details: {dataPullType} Data Pull: (Timestamp {timestamp:O}) "
                           + $"{countClients} Client(s), {countSites} Site(s), {countLocations} Location(s), "
                           + $"{countInspections} Inspection(s), {countDefinitions} Definition(s), {countCategories} Category(s)");

                    new AnchorSafe.Data.ASLogs()
                        .AddLogEntry($"{dataPullType} (App) - {countClients} Client(s), {countSites} Site(s), "
                                   + $"{countLocations} Location(s), {countInspections} Inspection(s), "
                                   + $"{countDefinitions} Definition(s), {countCategories} Category(s)",
                                     (Int32)Dashboard.LogType.DataPull,
                                      user.Id);
                }

                GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                IHttpActionResult ok = Ok(new { Data = data });
                log.Info("Exiting AppDataPullAll(ts) with Ok");
                return ok;
            }
            catch (Exception ex)
            {
                log.Error($"AppDataPullAll | User {user.Id} | Exception: {ex.Message}", ex);
                IHttpActionResult error = ResponseMessage(Request.CreateResponse(HttpStatusCode.InternalServerError, new { result = ex.Message }));
                log.Info("Exiting AppDataPullAll(ts) with InternalServerError");
                return error;
            }
        }

        /// <summary>
        /// Retrieves user-specific application data (full pull).
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> AppDataPull()
        {
            log.Info("Entering AppDataPull() overload");
            IHttpActionResult response = await AppDataPullAll(String.Empty);
            log.Info("Exiting AppDataPull() overload");
            return response;
        }

        /// <summary>
        /// Retrieves user-specific application data modified since the given timestamp.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> AppDataPull(String ts)
        {
            log.Info("Entering AppDataPull(ts)");
            log.Debug($"Parameter ts=\"{ts}\"");

            // Security
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for AppDataPull");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("AppDataPull | Access Denied (authentication)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppDataPull(ts) with Forbidden");
                return forbidden;
            }

            Users user = await Services.UserService.GetUserByUsername(username);
            log.Debug($"User lookup returned: {(user == null ? "null" : user.Id.ToString())}");
            if (user == null || !user.IsAuthorised)
            {
                log.Warn("AppDataPull | Access Denied (authorization)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppDataPull(ts) with Forbidden");
                return forbidden;
            }

            Models.DataPackage data = new API.Models.DataPackage();
            try
            {
                DateTime timestamp = new DateTime(2000, 1, 1);
                if (!String.IsNullOrWhiteSpace(ts))
                {
                    Boolean parsed = DateTime.TryParse(ts, out timestamp);
                    log.Debug($"Parsed ts to timestamp={timestamp:O}, success={parsed}");
                }

                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    db.Configuration.LazyLoadingEnabled = false;
                    log.Debug("Querying database for AppDataPull");

                    IQueryable<Inspections> inspectionsQ = db.Inspections.Where(x => x.UserId == user.Id && x.DateModified >= timestamp && !x.IsDeleted).Include(i => i.InspectionItems);
                    IQueryable<Definitions> definitionsQ = db.Definitions.Where(x => x.IsActive && x.DateModified >= timestamp).Include(d => d.DefinitionTypes).Include(c => c.Categories);
                    IQueryable<Categories> categoriesQ = db.Categories.Where(x => x.IsActive && x.DateModified >= timestamp);
                    IQueryable<Clients> clientsQ = db.Clients.Where(x => x.DateModified >= timestamp).Include(c => c.Sites);
                    IQueryable<Sites> sitesQ = db.Sites.Where(x => x.DateModified >= timestamp).Include(s => s.Locations);
                    IQueryable<Locations> locationsQ = db.Locations.Where(x => x.DateModified >= timestamp);

                    Task<List<Inspections>> inspectionsTask = inspectionsQ.ToListAsync();
                    Task<List<Definitions>> definitionsTask = definitionsQ.ToListAsync();
                    Task<List<Categories>> categoriesTask = categoriesQ.ToListAsync();
                    Task<List<Clients>> clientsTask = clientsQ.ToListAsync();
                    Task<List<Sites>> sitesTask = sitesQ.ToListAsync();
                    Task<List<Locations>> locationsTask = locationsQ.ToListAsync();

                    await Task.WhenAll(inspectionsTask, definitionsTask, categoriesTask, clientsTask, sitesTask, locationsTask);

                    data.Inspections.AddRange(inspectionsTask.Result.Select((Inspections insp) => new Models.DTO.Inspection(insp)));
                    data.Definitions.AddRange(definitionsTask.Result.Select((Definitions def) => new Models.DTO.Definition(def)));
                    data.Categories.AddRange(categoriesTask.Result.Select((Categories cat) => new Models.DTO.Category(cat)));
                    data.Clients.AddRange(clientsTask.Result.Select((Clients cli) => new Models.DTO.Client(cli)));
                    data.Sites.AddRange(sitesTask.Result.Select((Sites site) => new Models.DTO.Site(site)));
                    data.Locations.AddRange(locationsTask.Result.Select((Locations loc) => new Models.DTO.Location(loc)));

                    Int32 countClients = data.Clients.Count();
                    Int32 countSites = data.Sites.Count();
                    Int32 countLocations = data.Locations.Count();
                    Int32 countInspections = data.Inspections.Count();
                    Int32 countDefinitions = data.Definitions.Count();
                    Int32 countCategories = data.Categories.Count();

                    log.Info($"AppDataPull | User {user.Id} | Details: (Timestamp {timestamp:O}) {countClients} Client(s), {countSites} Site(s), {countLocations} Location(s), {countInspections} Inspection(s), {countDefinitions} Definition(s), {countCategories} Category(s)");
                }

                GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                IHttpActionResult ok = Ok(new { Data = data });
                log.Info("Exiting AppDataPull(ts) with Ok");
                return ok;
            }
            catch (Exception ex)
            {
                log.Error($"AppDataPull | User {user.Id} | Exception: {ex.Message}", ex);
                IHttpActionResult error = ResponseMessage(Request.CreateResponse(HttpStatusCode.InternalServerError, new { result = ex.Message }));
                log.Info("Exiting AppDataPull(ts) with InternalServerError");
                return error;
            }
        }

        /// <summary>
        /// Assigns an inspection to a user.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> AppInspectionAssignment([FromBody] Models.DTO.InspectionAssignment inspectionAssignment)
        {
            log.Info("Entering AppInspectionAssignment()");
            log.Debug($"Parameter assignment: {Newtonsoft.Json.JsonConvert.SerializeObject(inspectionAssignment)}");

            // Security
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for AppInspectionAssignment");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("AppInspectionAssignment | Access Denied (authentication)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppInspectionAssignment() with Forbidden");
                return forbidden;
            }

            Users user = await Services.UserService.GetUserByUsername(username);
            log.Debug($"User lookup returned: {(user == null ? "null" : user.Id.ToString())}");
            if (user == null || !user.IsAuthorised)
            {
                log.Warn("AppInspectionAssignment | Access Denied (authorization)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppInspectionAssignment() with Forbidden");
                return forbidden;
            }

            if (inspectionAssignment == null)
            {
                log.Warn("AppInspectionAssignment | No assignment data");
                IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "No assignment data." }));
                log.Info("Exiting AppInspectionAssignment() with BadRequest");
                return bad;
            }

            try
            {
                if (inspectionAssignment.UserId == user.Id || user.IsAdmin)
                {
                    using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                    {
                        db.Configuration.LazyLoadingEnabled = false;
                        log.Debug($"Looking up inspection ID {inspectionAssignment.InspectionId}");
                        Inspections inspection = await db.Inspections.FirstOrDefaultAsync(x => x.Id == inspectionAssignment.InspectionId);
                        if (inspection != null)
                        {
                            inspection.UserId = inspectionAssignment.UserId;
                            inspection.InspectionStatusId = (Int32)Models.DTO.InspectionStatus.InProgress;
                            inspection.InspectionDate = inspectionAssignment.DateStarted;
                            db.Entry(inspection).State = EntityState.Modified;
                            await db.SaveChangesAsync();

                            log.Info($"AppInspectionAssignment | User {user.Id} | Assigned User {inspectionAssignment.UserId} to Inspection {inspectionAssignment.InspectionId}");
                            log.Info("Exiting AppInspectionAssignment() with Ok");
                            return Ok();
                        }
                        else
                        {
                            log.Warn($"AppInspectionAssignment | No inspection found for ID {inspectionAssignment.InspectionId}");
                            IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "No inspection data." }));
                            log.Info("Exiting AppInspectionAssignment() with BadRequest");
                            return bad;
                        }
                    }
                }
                else
                {
                    log.Warn($"AppInspectionAssignment | Incorrect user. Assignment UserId {inspectionAssignment.UserId}, Current User {user.Id}");
                    IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Incorrect user." }));
                    log.Info("Exiting AppInspectionAssignment() with BadRequest");
                    return bad;
                }
            }
            catch (Exception ex)
            {
                log.Error($"AppInspectionAssignment | User {user.Id} | Exception: {ex.Message}", ex);
                IHttpActionResult error = ResponseMessage(Request.CreateResponse(HttpStatusCode.InternalServerError, new { result = ex.Message }));
                log.Info("Exiting AppInspectionAssignment() with InternalServerError");
                return error;
            }
        }

        /// <summary>
        /// Test saving of header metadata.
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IHttpActionResult> TestHeaders(String nonce)
        {
            log.Info("Entering TestHeaders()");
            log.Debug($"Parameter nonce=\"{nonce}\"");
            Boolean result = false;
            String debug = String.Empty;
            if (Request.Headers.TryGetValues("X-CPro", out IEnumerable<String> connHeaders) && Request.Headers.TryGetValues("User-Agent", out IEnumerable<String> userAgentHeaders))
            {
                (Boolean result, String debug) metaResult = await Device.SaveDeviceMeta(connHeaders.Union(userAgentHeaders).ToList(), DeviceReferenceType.None, nonce, debug, "TEST");
                log.Debug($"Device.SaveDeviceMeta returned result={metaResult.result}, debug=\"{metaResult.debug}\"");
                result = metaResult.result;
                debug = metaResult.debug;
            }
            IHttpActionResult response = Ok("OK: " + result + " | " + debug);
            log.Info("Exiting TestHeaders()");
            return response;
        }

        /// <summary>
        /// Syncs a batch of inspections, optionally including media.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> AppSyncInspections([FromBody] List<Data.AppInspection> appInspections, Boolean includeMedia = true)
        {
            log.Info("Entering AppSyncInspections()");
            log.Debug($"Parameter appInspections count={(appInspections == null ? 0 : appInspections.Count)}, includeMedia={includeMedia}");
            String debugJsonDump = String.Empty;

            // Security
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for AppSyncInspections");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("AppSyncInspections | Access Denied (authentication)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppSyncInspections() with Forbidden");
                return forbidden;
            }

            Users user = await Services.UserService.GetUserByUsername(username);
            log.Debug($"User lookup returned: {(user == null ? "null" : user.Id.ToString())}");
            if (user == null || !user.IsAuthorised)
            {
                log.Warn("AppSyncInspections | Access Denied (authorization)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppSyncInspections() with Forbidden");
                return forbidden;
            }

            try
            {
                debugJsonDump = AppInspectionDataDump(appInspections);
                log.Info($"AppSyncInspections | User {user.Id} | Details: Attempting Inspection Sync | DATA: {debugJsonDump}");

                if (appInspections == null || !appInspections.Any())
                {
                    log.Warn("AppSyncInspections | No inspection data");
                    IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "No inspection data." }));
                    log.Info("Exiting AppSyncInspections() with BadRequest");
                    return bad;
                }

                if (Request.Headers.TryGetValues("X-CPro", out IEnumerable<string> connHeaders) && Request.Headers.TryGetValues("User-Agent", out IEnumerable<string> userAgentHeaders))
                {
                    await Device.SaveDeviceMeta(connHeaders.Union(userAgentHeaders).ToList(), DeviceReferenceType.Inspection, appInspections.FirstOrDefault().Nonce.ToString());
                }

                List<Models.DTO.AppInspectionSync> syncResults = new List<Models.DTO.AppInspectionSync>();
                Int32 inspectionCount = 0;
                String inspectionLog = String.Empty;

                foreach (Data.AppInspection appInspection in appInspections)
                {
                    Int32 existsCount = await Helpers.Inspection.InspectionExists(appInspection.Nonce.ToString());
                    log.Debug($"Nonce={appInspection.Nonce}, existsCount={existsCount}");
                    if (existsCount >= 0)
                    {
                        Int32 wmUserId = configuration.GetValue<int>("AS_API_WebMatrixUserId");
                        if (user.Id != wmUserId && user.Id != appInspection.UserId && !user.IsAdmin)
                        {
                            log.Warn($"AppSyncInspections | Permission denied for user {user.Id} on inspection UserId {appInspection.UserId}");
                            IHttpActionResult badPerm = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "User does not have permission to sync." }));
                            log.Info("Exiting AppSyncInspections() with BadRequest (permission)");
                            return badPerm;
                        }

                        using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                        {
                            db.Configuration.LazyLoadingEnabled = true;
                            log.Debug("Processing inspection record in DB context");

                            Inspections inspectionEntity;
                            Inspections existingInspection = await db.Inspections.FirstOrDefaultAsync(x => x.Nonce == appInspection.Nonce);
                            if (existingInspection != null)
                            {
                                inspectionEntity = existingInspection;
                                db.Entry(inspectionEntity).State = EntityState.Modified;
                                log.Debug($"Updating existing inspection ID={inspectionEntity.Id}");
                            }
                            else
                            {
                                inspectionEntity = new Inspections
                                {
                                    Id = appInspection.Id,
                                    ClientId = appInspection.ClientId,
                                    CreatedUserId = appInspection.CreatedUserId,
                                    InspectionDate = DateTime.Now,
                                    InspectionStatusId = appInspection.InspectionStatusId,
                                    InspectionTypeId = (appInspection.InspectionTypeId > 0) ? appInspection.InspectionTypeId : (Int32?)null,
                                    IsLocked = appInspection.IsLocked,
                                    Latitude = appInspection.Latitude,
                                    Longitude = appInspection.Longitude,
                                    LocationId = appInspection.LocationId,
                                    DateModified = DateTime.Now,
                                    ModifiedUserId = appInspection.UserId,
                                    SimProId = appInspection.SimProId,
                                    SiteId = appInspection.SiteId,
                                    UserId = appInspection.UserId,
                                    SyncDate = appInspection.SyncDate,
                                    Nonce = appInspection.Nonce
                                };
                                db.Entry(inspectionEntity).State = EntityState.Added;
                                log.Debug("Adding new inspection entity");
                            }

                            // ... (site and location checks unchanged, but add debug logs)
                            if (!String.IsNullOrEmpty(appInspection.SiteName))
                            {
                                Sites siteEntity = db.Sites.Where(x => x.ClientId == appInspection.ClientId && x.SiteName.Trim() == appInspection.SiteName.Trim()).FirstOrDefault();
                                if (siteEntity == null)
                                {
                                    siteEntity = new Sites
                                    {
                                        SiteName = appInspection.SiteName.Trim(),
                                        ClientId = inspectionEntity.ClientId,
                                        IsActive = true,
                                        DateModified = DateTime.Now
                                    };
                                    db.Entry(siteEntity).State = EntityState.Added;
                                    log.Debug($"Created new site '{appInspection.SiteName}'");
                                }
                                inspectionEntity.SiteId = siteEntity.Id;
                            }
                            if (!String.IsNullOrEmpty(appInspection.LocationName))
                            {
                                Locations locEntity = db.Locations.Where(x => x.SiteId == inspectionEntity.SiteId && x.LocationName.Trim() == appInspection.LocationName.Trim()).FirstOrDefault();
                                if (locEntity == null)
                                {
                                    locEntity = new Locations
                                    {
                                        LocationName = appInspection.LocationName.Trim(),
                                        SiteId = inspectionEntity.SiteId,
                                        DateModified = DateTime.Now
                                    };
                                    db.Entry(locEntity).State = EntityState.Added;
                                    log.Debug($"Created new location '{appInspection.LocationName}'");
                                }
                                inspectionEntity.LocationId = locEntity.Id;
                            }

                            await db.SaveChangesAsync();
                            log.Debug($"Saved inspection ID={inspectionEntity.Id}");

                            Models.DTO.AppInspectionSync syncDto = new Models.DTO.AppInspectionSync
                            {
                                InspectionId = appInspection.Id,
                                ServerInspectionId = inspectionEntity.Id,
                                ItemCount = (inspectionEntity.InspectionItems != null) ? inspectionEntity.InspectionItems.Count : 0
                            };
                            syncResults.Add(syncDto);

                            List<InspectionItems> itemsToSave = (appInspection.InspectionItems != null)
                                ? appInspection.InspectionItems.ToList()
                                : new List<InspectionItems>();

                            if (itemsToSave.Any())
                            {
                                await SaveInspectionItems(inspectionEntity, itemsToSave, inspectionEntity.Id, includeMedia);
                            }

                            if (await MarkAsSyncedInspection(inspectionEntity.Id) == false)
                            {
                                log.Warn($"MarkAsSyncedInspection failed for inspection ID={inspectionEntity.Id}");
                                IHttpActionResult badSync = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Sync failed." }));
                                log.Info("Exiting AppSyncInspections() with BadRequest (markAsSynced)");
                                return badSync;
                            }
                        }

                        inspectionCount++;
                        inspectionLog += appInspection.Id + ",";
                    }
                    else
                    {
                        log.Warn($"AppSyncInspections | Nonce {appInspection.Nonce} already exists or invalid");
                        IHttpActionResult badNonce = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Sync failed, nonce exists." }));
                        log.Info("Exiting AppSyncInspections() with BadRequest (nonce exists)");
                        return badNonce;
                    }
                }

                inspectionLog = inspectionLog.TrimEnd(',');
                log.Info($"AppSyncInspections | User {user.Id} | Synced {inspectionCount}/{appInspections.Count} inspections. IDs: {inspectionLog}");
                new AnchorSafe.Data.ASLogs().AddLogEntry($"Sync Job (App) - Synced {appInspections.Count} inspections (IDs {inspectionLog})", (Int32)Dashboard.LogType.SyncJob, user.Id);

                GlobalConfiguration.Configuration.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
                IHttpActionResult ok = Ok(syncResults);
                log.Info("Exiting AppSyncInspections() with Ok");
                return ok;
            }
            catch (Exception ex)
            {
                log.Error($"AppSyncInspections | User {user.Id} | Exception: {ex.Message}", ex);
                IHttpActionResult error = ResponseMessage(Request.CreateResponse(HttpStatusCode.InternalServerError, new { result = ex.Message }));
                log.Info("Exiting AppSyncInspections() with InternalServerError");
                return error;
            }
        }

        /// <summary>
        /// Overload: sync inspections without media.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> AppSyncInspectionsData([FromBody] List<Data.AppInspection> appInspections)
        {
            log.Info("Entering AppSyncInspectionsData()");
            IHttpActionResult response = await AppSyncInspections(appInspections, false);
            log.Info("Exiting AppSyncInspectionsData()");
            return response;
        }

        /// <summary>
        /// Overload: sync a single inspection without media.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> AppSyncInspectionData([FromBody] Data.AppInspection appInspection)
        {
            log.Info("Entering AppSyncInspectionData()");
            List<Data.AppInspection> list = new List<Data.AppInspection> { appInspection };
            IHttpActionResult response = await AppSyncInspections(list, false);
            log.Info("Exiting AppSyncInspectionData()");
            return response;
        }

        private static Task<SKEncodedImageFormat> GetImageFormat(Byte[] imageData)
        {
            if (imageData == null)
            {
                throw new ArgumentNullException(nameof(imageData));
            }

            if (imageData.Length == 0)
            {
                throw new ArgumentException("Image data cannot be empty.", nameof(imageData));
            }

            return Task.Run(() =>
            {
                // Wrap the byte array in a memory stream for SkiaSharp to consume
                using (SKMemoryStream memoryStream = new SKMemoryStream(imageData))
                {
                    SKCodec codec = SKCodec.Create(memoryStream);
                    if (codec == null)
                    {
                        throw new ArgumentException(
                            "Unsupported image format or corrupted image data.",
                            nameof(imageData)
                        );
                    }

                    // Sniff out the dimensions and pixel layout
                    SKImageInfo imageInfo = codec.Info;

                    // Prepare a bitmap and attempt to decode every pixel
                    using (SKBitmap bitmap = new SKBitmap(imageInfo))
                    {
                        SKCodecResult decodeResult = codec.GetPixels(
                            bitmap.Info,
                            bitmap.GetPixels()
                        );

                        if (decodeResult != SKCodecResult.Success)
                        {
                            // IncompleteInput or Error means we couldnt load fully
                            throw new ArgumentException(
                                "Image data is incomplete or corrupted and cannot be fully decoded.",
                                nameof(imageData)
                            );
                        }
                    }

                    // All goodreturn the format we found
                    return codec.EncodedFormat;
                }
            });
        }

        private static Task<String> HashData(Byte[] data)
        {
            return Task.Run(() =>
            {
                using (SHA256 sha = SHA256.Create())
                {
                    Byte[] hash = sha.ComputeHash(data);
                    return String.Concat(Array.ConvertAll(hash, x => x.ToString("X2")));
                }
            });
        }

        // Pre-compiled regex: only 4-char Base64 blocks, with optional padding (== or =)
        private static readonly Regex _base64Regex = new Regex("^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$", RegexOptions.Compiled);

        private Boolean IsBase64(String text, out Byte[] data)
        {
            // null/empty is obviously not Base64
            if (String.IsNullOrEmpty(text))
            {
                data = null;
                return false;
            }

            // trim off stray whitespace at the ends
            text = text.Trim();

            // quick regex check: rejects anything not in the right 4-char blocks
            if (!_base64Regex.IsMatch(text))
            {
                data = null;
                return false;
            }

            // safe to decoderegex guarantees no FormatException
            data = Convert.FromBase64String(text);
            return true;
        }

        [HttpGet]
        public async Task<IHttpActionResult> RepairImages([FromUri]Boolean test = true)
        {
            String basePath = configuration["AS_API_ImageSaveFilePath"] ?? string.Empty;
            List<String> results = new List<String>();

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                foreach (InspectionItemMedia media in await db.InspectionItemMedia.Where(i => i.Media.Length > 200).ToListAsync())
                {
                    if (IsBase64(media.Media, out Byte[] data))
                    {
                        InspectionItems item = await db.InspectionItems.FindAsync(media.InspectionItemId);
                        Inspections inspection = await db.Inspections.FindAsync(item.InspectionId);

                        SKEncodedImageFormat format = await GetImageFormat(data);

                        String fileName = null;
                        switch (format)
                        {
                            case SKEncodedImageFormat.Jpeg:
                                fileName = (await HashData(data)) + ".jpg";
                                break;

                            case SKEncodedImageFormat.Png:
                                fileName = (await HashData(data)) + ".png";
                                break;

                            case SKEncodedImageFormat.Webp:
                                fileName = (await HashData(data)) + ".webp";
                                break;

                            default:
                                fileName = null;
                                break;
                        }

                        if (fileName == null)
                            continue;

                        // Compute file paths
                        String year = inspection.InspectionDate.Value.Year.ToString();
                        String month = inspection.InspectionDate.Value.ToString("MM");
                        String dir = Path.Combine(basePath, year, month, inspection.Id.ToString());
                        if (!Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        String fullPath = Path.Combine(dir, fileName);
                        String relativePath = $"{year}/{month}/{inspection.Id}/{fileName}";

                        // Save Changes
                        if (!test)
                        {
                            results.Add($"Saving media file to {fullPath}");
                            using (FileStream fs = System.IO.File.OpenWrite(fullPath))
                            {
                                await fs.WriteAsync(data, 0, data.Length);
                            }

                            media.Media = relativePath;
                            db.Entry(media).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            results.Add($"Would have saved media file to {fullPath}");
                        }
                    }
                }
            }

            return Ok(results);
        }

        /// <summary>
        /// Syncs a media item for an inspection.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> AppSyncInspectionsMediaItem([FromBody] Data.InspectionItemMedia media)
        {
            log.Info("Entering AppSyncInspectionsMediaItem()");
            log.Debug($"Parameter media: {Newtonsoft.Json.JsonConvert.SerializeObject(media)}");
            String debug = "0.";

            // Security
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for AppSyncInspectionsMediaItem");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("AppSyncInspectionsMediaItem | Access Denied (authentication)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppSyncInspectionsMediaItem() with Forbidden");
                return forbidden;
            }

            Users user = await Services.UserService.GetUserByUsername(username);
            log.Debug($"User lookup returned: {(user == null ? "null" : user.Id.ToString())}");
            if (user == null || !user.IsAuthorised)
            {
                log.Warn("AppSyncInspectionsMediaItem | Access Denied (authorization)");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting AppSyncInspectionsMediaItem() with Forbidden");
                return forbidden;
            }

            if (media == null)
            {
                log.Warn("AppSyncInspectionsMediaItem | Media was null");
                IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Could not save media item." }));
                log.Info("Exiting AppSyncInspectionsMediaItem() with BadRequest");
                return bad;
            }

            try
            {
                using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
                {
                    db.Configuration.LazyLoadingEnabled = false;
                    log.Debug($"Looking up inspection item by nonce {media.Nonce}");
                    InspectionItems item = await db.InspectionItems.FirstOrDefaultAsync(x => x.Nonce == media.Nonce);
                    if (item == null)
                    {
                        log.Warn($"No inspection item found for nonce {media.Nonce}");
                        IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "No matching inspection item found." }));
                        log.Info("Exiting AppSyncInspectionsMediaItem() with BadRequest");
                        return bad;
                    }

                    log.Debug($"Found inspection item ID {item.Id}");
                    Inspections inspection = await db.Inspections.FirstOrDefaultAsync(x => x.Id == item.InspectionId);
                    if (inspection == null)
                    {
                        log.Warn($"No inspection found for ID {item.InspectionId}");
                        IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "No matching inspection found." }));
                        log.Info("Exiting AppSyncInspectionsMediaItem() with BadRequest");
                        return bad;
                    }

                    // Compute file paths
                    String basePath = configuration["AS_API_ImageSaveFilePath"] ?? string.Empty;
                    String year = inspection.InspectionDate.Value.Year.ToString();
                    String month = inspection.InspectionDate.Value.ToString("MM");
                    String dir = Path.Combine(basePath, year, month, inspection.Id.ToString());
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                        log.Debug($"Created directory {dir}");
                    }

                    // Verify and write image file.
                    String fileName = null;
                    try
                    {
                        Byte[] data = Convert.FromBase64String(media.Media);
                        SKEncodedImageFormat format = await GetImageFormat(data);

                        switch (format)
                        {
                            case SKEncodedImageFormat.Jpeg:
                                log.Debug("Image verified as valid JPEG");
                                fileName = (await HashData(data)) + ".jpg";
                                break;

                            case SKEncodedImageFormat.Png:
                                log.Debug("Image verified as valid PNG");
                                fileName = (await HashData(data)) + ".png";
                                break;

                            case SKEncodedImageFormat.Webp:
                                log.Debug("Image verified as valid WEBP");
                                fileName = (await HashData(data)) + ".webp";
                                break;

                            default:
                                log.Error("Unable to verify image as a valid format");
                                throw new NotSupportedException("Image format is not supported");
                        }

                        String fullPath = Path.Combine(dir, fileName);
                        String relativePath = $"{year}/{month}/{inspection.Id}/{fileName}";
                        log.Debug($"Saving media file to {fullPath}");

                        using (FileStream fs = System.IO.File.OpenWrite(fullPath))
                        {
                            await fs.WriteAsync(data, 0, data.Length);
                        }

                        media.Media = relativePath;
                        log.Debug($"Media saved, new path: {media.Media}");
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex.Message);
                    }

                    // Upsert media record
                    String name = $"/{inspection.Id}/{fileName}";
                    InspectionItemMedia existing = await db.InspectionItemMedia.FirstOrDefaultAsync(x => x.InspectionItemId == item.Id && x.Media.EndsWith(name));
                    if (existing == null)
                    {
                        media.InspectionItemId = item.Id;
                        db.Entry(media).State = EntityState.Added;
                        await db.SaveChangesAsync();
                        log.Debug("Inserted new media record");
                    }
                    else
                    {
                        if (existing.Media != media.Media)
                        {
                            existing.Media = media.Media;
                            db.Entry(existing).State = EntityState.Modified;
                            await db.SaveChangesAsync();
                            log.Debug("Updated existing media record");
                        }
                    }
                }

                log.Info($"AppSyncInspectionsMediaItem | User {user.Id} | Media sync successful. Debug: {debug}");
                log.Info("Exiting AppSyncInspectionsMediaItem() with Ok");
                return Ok();
            }
            catch (Exception ex)
            {
                String mediaDump = Newtonsoft.Json.JsonConvert.SerializeObject(media);
                log.Error($"AppSyncInspectionsMediaItem | User {user.Id} | Exception: {ex.Message} | DATA: {mediaDump}", ex);
                IHttpActionResult error = ResponseMessage(Request.CreateResponse(HttpStatusCode.InternalServerError, new { result = ex.Message }));
                log.Info("Exiting AppSyncInspectionsMediaItem() with InternalServerError");
                return error;
            }
        }

        private async Task<Int32> SaveInspectionItems(Inspections inspection, List<InspectionItems> items, Int32 newId, Boolean includeMedia = true)
        {
            log.Debug($"Entering SaveInspectionItems() for Inspection {newId}");
            String debug = String.Empty;
            Int32 savedCount = 0;

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                IQueryable<InspectionItems> existing = db.InspectionItems.Where(x => x.InspectionId == newId);
                Int32 existingCount = existing.Count();
                log.Debug($"Existing items count: {existingCount}");

                foreach (InspectionItems item in items.Where(x => x.InspectionItemStatusId > 0))
                {
                    Boolean skip = existingCount > 0 && existing.Where(x => x.Nonce == item.Nonce).Any();
                    if (!skip)
                    {
                        if (item.IsSynced)
                            db.Entry(item).State = EntityState.Modified;
                        else
                            db.Entry(item).State = EntityState.Added;

                        item.InspectionId = newId;
                        item.Findings = String.IsNullOrEmpty(item.Findings) ? String.Empty : item.Findings.Trim();
                        item.Recommendations = String.IsNullOrEmpty(item.Recommendations) ? String.Empty : item.Recommendations.Trim();

                        savedCount++;
                        debug += "OK;";
                    }
                }
                await db.SaveChangesAsync();
                log.Info($"SaveInspectionItems | Inspection {newId} | Saved {savedCount} item(s). Debug: {debug}");

                if (includeMedia)
                {
                    // Update media file paths if needed
                    List<InspectionItems> allItems = await db.InspectionItems.Where(x => x.InspectionId == newId).ToListAsync();
                    foreach (InspectionItems itm in allItems)
                    {
                        List<InspectionItemMedia> medias = await db.InspectionItemMedia.Where(x => x.InspectionItemId == itm.Id).ToListAsync();
                        foreach (InspectionItemMedia m in medias)
                        {
                            if (!m.Media.Contains(".jpg") && m.Media != "SNIPPED")
                            {
                                String pathDir = Path.Combine(configuration["AS_API_ImageSaveFilePath"] ?? string.Empty,
                                                             itm.Inspections.InspectionDate.Value.Year.ToString(),
                                                             itm.Inspections.InspectionDate.Value.ToString("MM"),
                                                             itm.InspectionId.ToString());
                                if (!Directory.Exists(pathDir)) Directory.CreateDirectory(pathDir);

                                String newFile = Guid.NewGuid().ToString() + ".jpg";
                                System.IO.File.WriteAllBytes(Path.Combine(pathDir, newFile), Convert.FromBase64String(m.Media));
                                m.Media = $"/{itm.Inspections.InspectionDate.Value.Year}/{itm.Inspections.InspectionDate.Value:MM}/{itm.InspectionId}/{newFile}";
                                db.Entry(m).State = EntityState.Modified;
                            }
                        }
                        await db.SaveChangesAsync();
                    }
                    log.Debug("Completed media file updates in SaveInspectionItems");
                }
            }

            log.Debug("Exiting SaveInspectionItems()");
            return savedCount;
        }

        private async Task<Boolean> MarkAsSyncedInspection(Int32 id)
        {
            log.Debug($"Entering MarkAsSyncedInspection({id})");
            Boolean result = false;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                Inspections insp = db.Inspections.Find(id);
                if (insp != null)
                {
                    insp.SyncDate = DateTime.Now;
                    db.Entry(insp).State = EntityState.Modified;

                    if (insp.InspectionItems != null && insp.InspectionItems.Any())
                    {
                        foreach (InspectionItems itm in insp.InspectionItems)
                        {
                            itm.IsSynced = true;
                            db.Entry(itm).State = EntityState.Modified;
                            if (itm.InspectionItemMedia != null && itm.InspectionItemMedia.Any())
                            {
                                foreach (InspectionItemMedia m in itm.InspectionItemMedia)
                                {
                                    m.IsSynced = true;
                                    db.Entry(m).State = EntityState.Modified;
                                }
                            }
                        }
                    }

                    await db.SaveChangesAsync();
                    result = true;
                }
            }
            log.Debug($"Exiting MarkAsSyncedInspection({id}) with result={result}");
            return result;
        }

        private String AppInspectionDataDump(List<Data.AppInspection> appInspections)
        {
            log.Debug("Entering AppInspectionDataDump()");
            if (appInspections == null)
            {
                log.Info("AppInspectionDataDump | Data was empty");
                return String.Empty;
            }

            String serialized = Newtonsoft.Json.JsonConvert.SerializeObject(appInspections);
            List<Data.AppInspection> copy = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Data.AppInspection>>(serialized);
            foreach (Data.AppInspection ai in copy)
            {
                if (ai.InspectionItems != null)
                {
                    foreach (InspectionItems ii in ai.InspectionItems)
                    {
                        if (ii.InspectionItemMedia != null)
                        {
                            foreach (InspectionItemMedia iim in ii.InspectionItemMedia)
                            {
                                iim.Media = "SNIPPED";
                            }
                        }
                    }
                }
            }
            String dump = Newtonsoft.Json.JsonConvert.SerializeObject(copy);
            log.Debug("Exiting AppInspectionDataDump()");
            return dump;
        }

        /// <summary>
        /// Generates a test filename for image storage.
        /// </summary>
        [HttpGet]
        public Task<IHttpActionResult> TestFilename()
        {
            log.Info("Entering TestFilename()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for TestFilename");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("TestFilename | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting TestFilename() with Forbidden");
                return Task.FromResult(forbidden);
            }

            String basePath = configuration["AS_API_ImageSaveFilePath"] ?? string.Empty;
            String path = Path.Combine(basePath, Guid.NewGuid().ToString());
            log.Debug($"Generated test filename: {path}");
            IHttpActionResult response = Ok(path);
            log.Info("Exiting TestFilename()");
            return Task.FromResult(response);
        }

        /// <summary>
        /// Saves or updates an inspection record.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> SaveInspection(Inspections inspection)
        {
            log.Info("Entering SaveInspection()");
            if (inspection == null)
            {
                log.Warn("SaveInspection | Inspection data empty");
                IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Inspection data empty." }));
                log.Info("Exiting SaveInspection() with BadRequest");
                return bad;
            }
            if (inspection.ClientId <= 0 || inspection.UserId <= 0)
            {
                log.Warn("SaveInspection | Foreign key violation");
                IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Inspection foreign key violation." }));
                log.Info("Exiting SaveInspection() with BadRequest");
                return bad;
            }

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                Inspections existing = await db.Inspections.FirstOrDefaultAsync(x => x.Id == inspection.Id);
                if (existing != null)
                {
                    db.Entry(inspection).State = EntityState.Modified;
                    log.Debug($"Updating existing inspection ID={inspection.Id}");
                }
                else
                {
                    db.Entry(inspection).State = EntityState.Added;
                    log.Debug("Adding new inspection entity");
                }
                await db.SaveChangesAsync();
            }

            IHttpActionResult ok = Ok(new { InspectionId = inspection.Id });
            log.Info($"Exiting SaveInspection() with Ok (InspectionId={inspection.Id})");
            return ok;
        }

        /// <summary>
        /// Returns all unassigned inspections.
        /// </summary>
        [HttpGet]
        public Task<IHttpActionResult> GetUnassigned()
        {
            log.Info("Entering GetUnassigned()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetUnassigned");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetUnassigned | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetUnassigned() with Forbidden");
                return Task.FromResult(forbidden);
            }

            List<Inspections> result;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                Int32 status = configuration.GetValue<int>("AS_API_UnassignedStatus");
                log.Debug($"Filtering inspections with status={status}");
                result = db.Inspections.Where(x => x.InspectionStatusId == status)
                                       .OrderBy(x => x.Sites.SiteName)
                                       .ThenBy(x => x.Locations.LocationName)
                                       .ToList();
            }

            log.Info($"GetUnassigned | Retrieved {result.Count} inspections");
            log.Info("Exiting GetUnassigned() with Ok");
            IHttpActionResult ok = Ok(result);
            return Task.FromResult(ok);
        }

        /// <summary>
        /// Updates an assigned inspection.
        /// </summary>
        [HttpPost]
        public async Task<IHttpActionResult> UpdateInspectionAssigned(Inspections inspection)
        {
            log.Info("Entering UpdateInspectionAssigned()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for UpdateInspectionAssigned");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("UpdateInspectionAssigned | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting UpdateInspectionAssigned() with Forbidden");
                return forbidden;
            }

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Entry(inspection).State = EntityState.Modified;
                await db.SaveChangesAsync();
                log.Info($"UpdateInspectionAssigned | Updated inspection ID={inspection.Id}");
            }

            log.Info("Exiting UpdateInspectionAssigned() with Ok");
            return Ok(inspection);
        }

        /// <summary>
        /// Returns all clients.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> GetClients()
        {
            log.Info("Entering GetClients()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetClients");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetClients | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetClients() with Forbidden");
                return forbidden;
            }

            List<Clients> clients;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                clients = await db.Clients.OrderBy(x => x.ClientName).ToListAsync();
            }

            log.Info($"GetClients | Retrieved {clients.Count} clients");
            log.Info("Exiting GetClients() with Ok");
            return Ok(clients);
        }

        /// <summary>
        /// Returns all active categories.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> GetCategories()
        {
            log.Info("Entering GetCategories()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetCategories");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetCategories | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetCategories() with Forbidden");
                return forbidden;
            }

            List<Categories> cats;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                cats = await db.Categories.Where(x => x.IsActive).OrderBy(x => x.CategoryOrder).ThenBy(x => x.CategoryName).ToListAsync();
            }

            log.Info($"GetCategories | Retrieved {cats.Count} categories");
            log.Info("Exiting GetCategories() with Ok");
            return Ok(cats);
        }

        /// <summary>
        /// Returns definitions filtered by type.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> GetDefinitions(Int32 typeId = 0)
        {
            log.Info("Entering GetDefinitions()");
            log.Debug($"Parameter typeId={typeId}");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetDefinitions");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetDefinitions | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetDefinitions() with Forbidden");
                return forbidden;
            }

            List<Definitions> defs = new List<Definitions>();
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                IQueryable<Definitions> query = db.Definitions;
                if (typeId > 0)
                {
                    query = query.Where(x => x.DefinitionTypeId == typeId);
                }
                defs = await query.OrderBy(x => x.Description).ToListAsync();
            }

            log.Info($"GetDefinitions | Retrieved {defs.Count} definitions");
            log.Info("Exiting GetDefinitions() with Ok");
            return Ok(defs);
        }

        /// <summary>
        /// Returns locations filtered by site.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> GetLocations(Int32 siteId = 0)
        {
            log.Info("Entering GetLocations()");
            log.Debug($"Parameter siteId={siteId}");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetLocations");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetLocations | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetLocations() with Forbidden");
                return forbidden;
            }

            IQueryable<Locations> query;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                query = db.Locations.Where(x => x.LocationName != String.Empty);
                if (siteId > 0) query = query.Where(x => x.SiteId == siteId);
            }

            List<Locations> locs = await query.OrderBy(x => x.LocationName).ToListAsync();
            log.Info($"GetLocations | Retrieved {locs.Count} locations");
            log.Info("Exiting GetLocations() with Ok");
            return Ok(locs);
        }

        /// <summary>
        /// Returns sites filtered by client.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> GetSites(Int32 clientId = 0)
        {
            log.Info("Entering GetSites()");
            log.Debug($"Parameter clientId={clientId}");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetSites");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetSites | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetSites() with Forbidden");
                return forbidden;
            }

            IQueryable<Sites> query;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                query = db.Sites.Where(x => x.SiteName != String.Empty);
                if (clientId > 0) query = query.Where(x => x.ClientId == clientId);
            }

            List<Sites> sites = await query.OrderBy(x => x.SiteName).ToListAsync();
            log.Info($"GetSites | Retrieved {sites.Count} sites");
            log.Info("Exiting GetSites() with Ok");
            return Ok(sites);
        }

        /// <summary>
        /// Returns all definition types.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> GetDefinitionTypes()
        {
            log.Info("Entering GetDefinitionTypes()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetDefinitionTypes");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetDefinitionTypes | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetDefinitionTypes() with Forbidden");
                return forbidden;
            }

            List<DefinitionTypes> dts;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                dts = await db.DefinitionTypes.OrderBy(x => x.Description).ToListAsync();
            }

            log.Info($"GetDefinitionTypes | Retrieved {dts.Count} definition types");
            log.Info("Exiting GetDefinitionTypes() with Ok");
            return Ok(dts);
        }

        /// <summary>
        /// Returns all inspection item statuses.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> GetInspectionItemStatus()
        {
            log.Info("Entering GetInspectionItemStatus()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetInspectionItemStatus");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetInspectionItemStatus | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetInspectionItemStatus() with Forbidden");
                return forbidden;
            }

            List<InspectionItemStatus> statuses;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                statuses = await db.InspectionItemStatus.OrderBy(x => x.Description).ToListAsync();
            }

            log.Info($"GetInspectionItemStatus | Retrieved {statuses.Count} statuses");
            log.Info("Exiting GetInspectionItemStatus() with Ok");
            return Ok(statuses);
        }

        /// <summary>
        /// Returns all inspection types.
        /// </summary>
        [HttpGet]
        public async Task<IHttpActionResult> GetInspectionTypes()
        {
            log.Info("Entering GetInspectionTypes()");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetInspectionTypes");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetInspectionTypes | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetInspectionTypes() with Forbidden");
                return forbidden;
            }

            List<InspectionType> types;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                types = await db.InspectionType.OrderBy(x => x.Description).ToListAsync();
            }

            log.Info($"GetInspectionTypes | Retrieved {types.Count} inspection types");
            log.Info("Exiting GetInspectionTypes() with Ok");
            return Ok(types);
        }

        /// <summary>
        /// Returns a specific inspection by ID.
        /// </summary>
        [HttpGet]
        [Route("Data/GetInspection/{id}")]
        public async Task<IHttpActionResult> GetInspection(Int32 id = 0)
        {
            log.Info("Entering GetInspection()");
            log.Debug($"Parameter id={id}");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetInspection");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetInspection | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetInspection() with Forbidden");
                return forbidden;
            }

            List<Inspections> inspections;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                db.Configuration.ProxyCreationEnabled = false;
                inspections = await db.Inspections.Where(x => x.Id == id).ToListAsync();
                inspections.Where(x => x.InspectionTypeId == null)
                           .Select(p => { p.InspectionTypeId = (Int32)AnchorSafe.API.Models.DTO.InspectionType.Unassigned; return p; })
                           .ToList();
            }

            log.Info($"GetInspection | Retrieved {inspections.Count} record(s) for ID={id}");
            log.Info("Exiting GetInspection() with Ok");
            return Ok(inspections);
        }

        /// <summary>
        /// Returns inspection items for a specific inspection.
        /// </summary>
        [HttpGet]
        [Route("Data/GetInspectionItems/{id}")]
        public async Task<IHttpActionResult> GetInspectionItems(Int32 id = 0)
        {
            log.Info("Entering GetInspectionItems()");
            log.Debug($"Parameter id={id}");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetInspectionItems");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetInspectionItems | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetInspectionItems() with Forbidden");
                return forbidden;
            }

            List<InspectionItems> items;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                items = await db.InspectionItems.Where(x => x.InspectionId == id).ToListAsync();
            }

            log.Info($"GetInspectionItems | Retrieved {items.Count} items for InspectionId={id}");
            log.Info("Exiting GetInspectionItems() with Ok");
            return Ok(items);
        }

        /// <summary>
        /// Returns inspection media for a specific inspection.
        /// </summary>
        [HttpGet]
        [Route("Data/GetInspectionMedia/{id}")]
        public async Task<IHttpActionResult> GetInspectionMedia(Int32 id = 0)
        {
            log.Info("Entering GetInspectionMedia()");
            log.Debug($"Parameter id={id}");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetInspectionMedia");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetInspectionMedia | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetInspectionMedia() with Forbidden");
                return forbidden;
            }

            List<InspectionItemMedia> mediaList;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                db.Configuration.LazyLoadingEnabled = false;
                mediaList = await db.InspectionItemMedia.Where(x => x.InspectionItems.InspectionId == id).ToListAsync();
            }

            log.Info($"GetInspectionMedia | Retrieved {mediaList.Count} media items for InspectionId={id}");
            log.Info("Exiting GetInspectionMedia() with Ok");
            return Ok(mediaList);
        }

        /// <summary>
        /// Returns the URL of the latest inspection report for a given inspection.
        /// </summary>
        [HttpGet]
        [Route("Data/GetInspectionReportUrl/{inspectionId}")]
        public async Task<IHttpActionResult> GetInspectionReportUrl(Int32 inspectionId)
        {
            log.Info("Entering GetInspectionReportUrl()");
            log.Debug($"Parameter inspectionId={inspectionId}");
            String token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            log.Debug("Token retrieved for GetInspectionReportUrl");
            String username = TokenService.ValidateToken(token);
            log.Debug($"Token validated. Username: {username}");
            if (username == null && !Api.IsApplicationDevMode())
            {
                log.Warn("GetInspectionReportUrl | Access Denied");
                IHttpActionResult forbidden = ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." }));
                log.Info("Exiting GetInspectionReportUrl() with Forbidden");
                return forbidden;
            }

            String url = String.Empty;
            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                InspectionReports ir = await db.InspectionReports.Where(x => x.InspectionId == inspectionId).OrderByDescending(x => x.DateCreated).FirstOrDefaultAsync();
                if (ir != null) url = ir.Url;
            }

            log.Debug($"Found report URL: {url}");
            log.Info("Exiting GetInspectionReportUrl() with Ok");
            return Ok(url);
        }

        /// <summary>
        /// Handles multipart data uploads of arbitrary files (e.g. data dumps).
        /// </summary>
        [HttpPost]
        public async System.Threading.Tasks.Task<IHttpActionResult> Dump()
        {
            log.Info("Entering Dump()");
            if (!Request.Content.IsMimeMultipartContent())
            {
                log.Warn("Dump | Unsupported media type");
                throw new HttpResponseException(HttpStatusCode.UnsupportedMediaType);
            }

            String userId = String.Empty;
            String debug = String.Empty;

            try
            {
                if (Request.Headers.TryGetValues("X-CPro", out IEnumerable<String> connHeaders) &&
                    Request.Headers.TryGetValues("User-Agent", out IEnumerable<String> userAgentHeaders))
                {
                    await Device.SaveDeviceMeta(connHeaders.Union(userAgentHeaders).ToList(), DeviceReferenceType.None, "Dump");
                    log.Debug("Saved device metadata for Dump");
                }

                String uploadPath = configuration["AS_API_DataSaveFilePath"] ?? string.Empty;
                MultipartFormDataStreamProvider provider = new MultipartFormDataStreamProvider(uploadPath);
                await Request.Content.ReadAsMultipartAsync(provider);
                log.Debug("Multipart content read");

                foreach (MultipartFileData fileData in provider.FileData)
                {
                    String fileName = fileData.Headers.ContentDisposition.FileName.Trim('"');
                    log.Debug($"Processing file: {fileName}");
                    if (!fileName.Contains('_'))
                    {
                        log.Warn("Dump | No user id detected in filename");
                        IHttpActionResult bad = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "No user id detected." }));
                        log.Info("Exiting Dump() with BadRequest");
                        return bad;
                    }

                    userId = fileName.Split('_')[0];
                    String userFolder = Path.Combine(uploadPath, userId);
                    if (!Directory.Exists(userFolder)) Directory.CreateDirectory(userFolder);

                    String dest = Path.Combine(userFolder, fileName);
                    System.IO.File.Move(fileData.LocalFileName, dest);
                    log.Info($"DataDump | File saved to {dest}");
                }

                log.Info("Exiting Dump() with Ok");
                return Ok("Saved");
            }
            catch (Exception ex)
            {
                log.Error($"Dump | User {userId} | Exception: {ex.Message}", ex);
                IHttpActionResult error = ResponseMessage(Request.CreateResponse(HttpStatusCode.InternalServerError, new { result = ex.Message }));
                log.Info("Exiting Dump() with InternalServerError");
                return error;
            }
        }

        /// <summary>
        /// Lists uploaded data dump files.
        /// </summary>
        [HttpGet]
        [Route("Data/Dumps/{userId?}")]
        public List<DataDump> Dumps(Int32 userId = -1)
        {
            log.Info("Entering Dumps()");
            log.Debug($"Parameter userId={userId}");

            String dumpUploadPath = configuration["AS_API_DataSaveFilePath"] ?? string.Empty;
            List<DataDump> dumpList = new List<DataDump>();

            DirectoryInfo di = new DirectoryInfo(dumpUploadPath);
            FileInfo[] files = di.GetFiles("*.zip", SearchOption.AllDirectories);
            log.Debug($"Found {files.Length} dump files");

            using (AnchorSafe_DbContext db = new AnchorSafe_DbContext())
            {
                String baseUrl = configuration["AS_API_BaseUrl"] ?? string.Empty;
                foreach (FileInfo file in files)
                {
                    Int32 fileUserId = -1;
                    Int32.TryParse(file.Name.Split('_')[0], out fileUserId);
                    if ((userId > 0 && fileUserId == userId) || (userId < 0 && fileUserId > 0))
                    {
                        String displayName = String.Empty;
                        Users u = db.Users.Where(x => x.Id == fileUserId).FirstOrDefault();
                        if (u != null) displayName = $"{u.FirstName} {u.Surname}";

                        dumpList.Add(new DataDump
                        {
                            DateCreated = file.CreationTime,
                            FileSize = file.Length,
                            UserId = fileUserId,
                            UserDisplayName = displayName,
                            Link = $"{baseUrl}/Data/DumpDownload?fileName={file.Name}"
                        });
                    }
                }
            }

            List<DataDump> ordered = dumpList.OrderBy(x => x.UserId).ToList();
            log.Info($"Dumps | Returning {ordered.Count} dumps");
            log.Info("Exiting Dumps()");
            return ordered;
        }

        /// <summary>
        /// Downloads a specific dump file.
        /// </summary>
        [HttpGet]
        public HttpResponseMessage DumpDownload(String fileName)
        {
            log.Info("Entering DumpDownload()");
            log.Debug($"Parameter fileName={fileName}");

            String dumpUploadPath = configuration["AS_API_DataSaveFilePath"] ?? string.Empty;
            DirectoryInfo di = new DirectoryInfo(dumpUploadPath);
            FileInfo fi = di.GetFiles("*.zip", SearchOption.AllDirectories).Where(x => x.Name == fileName).FirstOrDefault();

            if (fi == null)
            {
                log.Warn($"DumpDownload | File not found: {fileName}");
                return Request.CreateResponse(HttpStatusCode.NotFound, new { result = "No file found." });
            }

            FileStream stream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read);
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = fileName
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            log.Info($"DumpDownload | Serving file {fileName}");
            log.Info("Exiting DumpDownload()");
            return response;
        }
    }
}
