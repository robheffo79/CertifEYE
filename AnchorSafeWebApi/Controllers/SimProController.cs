using AnchorSafe.API.Helpers;
using AnchorSafe.API.Services;
using AnchorSafe.SimPro.DTO.Models.Asset;
using AnchorSafe.SimPro.DTO.Models.People;
using AnchorSafe.SimPro.DTO.Models.Projects;
using AnchorSafe.SimPro.Helpers;
using System.Data.Entity;
using System.Diagnostics;
using System.Net;
using System.Web.Http;

namespace AnchorSafe.API.Controllers
{
    public class SimProController : ApiController
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        SimPro.SimProSettings simProSettings = new AnchorSafe.SimPro.SimProSettings()
        {
            Host = ConfigurationManager.AppSettings["SimPro_API_BaseUrl"].ToString(),
            Version = ConfigurationManager.AppSettings["SimPro_API_Version"].ToString(),
            Key = ConfigurationManager.AppSettings["SimPro_API_Key"].ToString(),
            CompanyId = int.Parse(ConfigurationManager.AppSettings["SimPro_API_CompanyId"].ToString()),
            CachePath = ConfigurationManager.AppSettings["SimPro_API_CachePath"].ToString()
        };

        [HttpGet]
        public Task<IHttpActionResult> Hello()
        {
            // Testing AnchorSafe API connection
            IHttpActionResult result =  Ok("Hi SimPro");
            return Task.FromResult(result);
        }

        [HttpGet]
        public Task<IHttpActionResult> Test()
        {
            // Testing SimPro API connection
            bool res = new SimPro.SimProProvider(simProSettings).Test();
            IHttpActionResult result = Ok(new { Status = "SimPro is " + (res ? "OK" : "unavailable") });
            return Task.FromResult(result);
        }

        [HttpGet]
        public Task<IHttpActionResult> LastDataRefresh(String param = "")
        {
            IHttpActionResult result = null;
            if (String.IsNullOrEmpty(param))
            {
                result = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Bad request - missing token." }));
                return Task.FromResult(result);
            }

            if (param != ConfigurationManager.AppSettings["AS_API_CronToken"].ToString())
            {
                result = ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Still a bad request - incorrect token." }));
                return Task.FromResult(result);
            }

            Dictionary<String, String> lastUpdated = new Dictionary<String, String>();
            SimPro.SimProProvider spp = new SimPro.SimProProvider(simProSettings);

            if (System.IO.File.Exists($"{simProSettings.CachePath}customers_data.json"))
            {
                DateTime updated = spp.GetLastCacheUpdate("customers");
                lastUpdated.Add("Clients", updated.ToString());
            }
            if (System.IO.File.Exists($"{simProSettings.CachePath}sites_data.json"))
            {
                DateTime updated = spp.GetLastCacheUpdate("sites");
                lastUpdated.Add("Sites", updated.ToString());
            }
            if (System.IO.File.Exists($"{simProSettings.CachePath}assets_data.json"))
            {
                DateTime updated = spp.GetLastCacheUpdate("assets");
                lastUpdated.Add("Locations", updated.ToString());
            }
            if (System.IO.File.Exists($"{simProSettings.CachePath}jobs_data.json"))
            {
                DateTime updated = spp.GetLastCacheUpdate("jobs");
                lastUpdated.Add("Jobs", updated.ToString());
            }
            /*if (System.IO.File.Exists($"{simProSettings.CachePath}employees_data.json"))
            {
                var updated = spp.GetLastCacheUpdate("employees");
                lastUpdated.Add("Employees", updated.ToString());
            }*/

            string strOutput = "";
            foreach (KeyValuePair<string, string> u in lastUpdated)
            {
                strOutput += $"{u.Key} : {u.Value}, ";
            }
            strOutput = strOutput.Trim().TrimEnd(',');

            log.Info($"LastDataRefresh | Token: {param} | Details: Data was last refreshed {strOutput}."); /* LOG */
            result = Ok(lastUpdated);

            return Task.FromResult(result);
        }


        [HttpGet]
        public async Task<IHttpActionResult> DataRefresh(string param = "", int uid = -1, string get = "all")
        {
            if (string.IsNullOrEmpty(param))
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Bad request - missing token." }));

            if (param != ConfigurationManager.AppSettings["AS_API_CronToken"].ToString())
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Still a bad request - incorrect token." }));

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            //string get = "all";
            string resultMsg = "";
            SimPro.SimProProvider spp = new SimPro.SimProProvider(simProSettings);
            bool result = await spp.GetData(250, 1, true, get);

            if (result)
            {
                resultMsg = "GetData() step SUCCESS. ";

                // DO STUFF
                string data = "";
                int clientCount = 0;
                int sitesCount = 0;
                int locationCount = 0;
                int inspectionsCount = 0;

                // CLIENTS
                if (get == "all" || get == "clients")
                {
                    using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}customers_data.json"))
                    {
                        data = file.ReadToEnd();
                        if (data != null && !string.IsNullOrEmpty(data.ToString()))
                        {
                            CustomerContainer customerContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<CustomerContainer>(data.ToString());
                            if (customerContainer != null && customerContainer.Items.Any())
                            {
                                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
                                {
                                    List<int> customerIDs = await db.Clients.Select(x => x.SimProId).ToListAsync();
                                    IEnumerable<CustomerListItem> newCustomers = customerContainer.Items.Where(x => !customerIDs.Contains(x.ID));
                                    if (newCustomers.Any())
                                    {
                                        /* ADD */
                                        foreach (CustomerListItem customer in newCustomers)
                                        {
                                            Data.Clients client = new Data.Clients()
                                            {
                                                ClientName = customer.CompanyName.Trim(),
                                                SimProId = customer.ID,
                                                DateCreated = DateTime.Now,
                                                DateModified = DateTime.Now
                                            };
                                            Data.Clients c = db.Clients.Add(client);
                                            if (c != null) { clientCount++; }

                                            await db.SaveChangesAsync();
                                        }
                                        resultMsg += $"{clientCount} new client(s) added. ";
                                    }
                                }
                            }
                        }
                    }
                }
                // SITES
                if (get == "all" || get == "sites")
                {
                    data = "";  // Reset
                    using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}sites_data.json"))
                    {
                        data = file.ReadToEnd();
                        if (data != null && !string.IsNullOrEmpty(data.ToString()))
                        {
                            SiteContainer siteContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<SiteContainer>(data.ToString());
                            if (siteContainer != null && siteContainer.Items.Any())
                            {
                                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
                                {
                                    // Sites                                    
                                    //var siteIDs = db.Sites.Select(x => x.SimProId);
                                    //var newSites = siteContainer.Items.Where(x => !siteIDs.Contains(x.ID));
                                    System.Data.Entity.DbSet<Data.Sites> existingSites = db.Sites;
                                    List<SiteListItem> newSites = siteContainer.Items;
                                    if (newSites.Any())
                                    {
                                        /* ADD */
                                        foreach (SiteListItem site in newSites)
                                        {
                                            List<SimpleCustomer> customers = site.Customers;
                                            if (customers != null && customers.Any())
                                            {
                                                foreach (SimpleCustomer customer in customers)
                                                {
                                                    Data.Clients client = await db.Clients.FirstOrDefaultAsync(x => x.SimProId == customer.ID);
                                                    if (client != null)
                                                    {
                                                        if (existingSites.Where(x => x.SimProId == site.ID && x.ClientId == client.Id).Count() <= 0)
                                                        {
                                                            Data.Sites tempSite = new Data.Sites()
                                                            {
                                                                SiteName = site.Name.Trim(),
                                                                SimProId = site.ID,
                                                                Street = site.Address.StreetAddress.Trim(),
                                                                City = site.Address.City.Trim(),
                                                                State = site.Address.State.Trim(),
                                                                PostCode = site.Address.PostalCode.Trim(),
                                                                ClientId = client.Id,
                                                                IsActive = !site.Archived,
                                                                DateCreated = DateTime.Now,
                                                                DateModified = DateTime.Now
                                                            };
                                                            Data.Sites s = db.Sites.Add(tempSite);
                                                            if (s != null) { sitesCount++; }

                                                            await db.SaveChangesAsync();
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        resultMsg += $"{sitesCount} new site(s) added. ";
                                    }
                                }
                            }
                        }
                    }
                }
                // LOCATIONS
                if (get == "all" || get == "locations")
                {
                    data = "";  // Reset
                    using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}assets_data.json"))
                    {
                        data = file.ReadToEnd();
                        if (data != null && !string.IsNullOrEmpty(data.ToString()))
                        {
                            AssetContainer assetContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<AssetContainer>(data.ToString());
                            if (assetContainer != null && assetContainer.Items.Any())
                            {
                                // Use data to update local store
                                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
                                {
                                    IQueryable<int?> assetIDs = db.Locations.Select(x => x.SimProId);
                                    IEnumerable<AssetListItem> newAssets = assetContainer.Items.Where(x => !assetIDs.Contains(x.ID));
                                    if (newAssets.Any())
                                    {
                                        foreach (AssetListItem asset in newAssets)
                                        {
                                            Data.Sites site = await db.Sites.FirstOrDefaultAsync(x => x.SimProId == asset.Site.ID);
                                            if (site != null)
                                            {
                                                foreach (SimPro.DTO.Models.CustomFieldItem cfi in asset.CustomFields)
                                                {
                                                    if (cfi.CustomField.Name == "Building Name / Number" && !string.IsNullOrEmpty(cfi.Value))
                                                    {
                                                        Data.Locations location = await db.Locations.FirstOrDefaultAsync(x => x.SimProId == asset.ID);
                                                        if (location == null)   // Add location
                                                        {
                                                            location = new Data.Locations()
                                                            {
                                                                LocationName = cfi.Value.Trim(),
                                                                SimProId = asset.ID,
                                                                SiteId = site.Id,
                                                            };
                                                            Data.Locations l = db.Locations.Add(location);
                                                            if (l != null) { locationCount++; }

                                                            await db.SaveChangesAsync();
                                                        }
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                resultMsg += $"{locationCount} new location(s) added. ";
                            }
                        }
                    }
                }
                // INSPECTIONS
                if (get == "all" || get == "inspections")
                {
                    data = "";  // Reset
                    using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}jobs_data.json"))
                    {
                        data = file.ReadToEnd();
                        if (data != null && !string.IsNullOrEmpty(data.ToString()))
                        {
                            JobContainer jobContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<JobContainer>(data.ToString());
                            if (jobContainer != null && jobContainer.Items.Any())
                            {
                                int adminUserId = int.Parse(ConfigurationManager.AppSettings["AS_API_AdminUserId"].ToString());
                                int unassignedUserId = int.Parse(ConfigurationManager.AppSettings["AS_API_UnassignedUserId"].ToString());

                                // Use data to update local store
                                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
                                {
                                    IQueryable<int?> jobIDs = db.Inspections.Select(x => x.SimProId);
                                    IEnumerable<JobListItem> newJobs = jobContainer.Items.Where(x => !jobIDs.Contains(x.ID));
                                    if (newJobs.Any())
                                    {
                                        foreach (JobListItem job in newJobs)
                                        {
                                            Data.Clients client = await db.Clients.FirstOrDefaultAsync(x => x.SimProId == job.Customer.ID);
                                            Data.Sites site = await db.Sites.FirstOrDefaultAsync(x => x.SimProId == job.Site.ID);
                                            if (site != null && client != null)
                                            {
                                                // Inspections
                                                Data.Inspections inspection = await db.Inspections.FirstOrDefaultAsync(x => x.SimProId == job.ID);
                                                if (inspection == null) // Add new
                                                {
                                                    inspection = new Data.Inspections()
                                                    {
                                                        ClientId = client.Id,
                                                        SiteId = site.Id,
                                                        /*InspectionTypeId = null,*/
                                                        InspectionStatusId = await GetInspectionStageId(job.Stage),
                                                        UserId = unassignedUserId,
                                                        DateCreated = DateTime.Now,
                                                        DateModified = DateTime.Now,
                                                        ModifiedUserId = adminUserId,
                                                        CreatedUserId = adminUserId,
                                                        SimProId = job.ID
                                                    };
                                                    Data.Inspections i = db.Inspections.Add(inspection);
                                                    if (i != null) { inspectionsCount++; }

                                                    await db.SaveChangesAsync();
                                                }
                                            }
                                        }
                                    }
                                }
                                resultMsg += $"{inspectionsCount} new inspections added. ";
                            }
                        }
                    }
                }


                resultMsg += "Update Local step SUCCESS";


                stopwatch.Stop();
                log.Info($"RefreshData | Token: {param} | Details: {resultMsg}. Time elapsed (milliseconds): {stopwatch.Elapsed.TotalMilliseconds}"); /* LOG */
                string asLog = new AnchorSafe.Data.ASLogs().AddLogEntry($"Refresh Data (SimPro) - New data added: {clientCount} Client(s), {sitesCount} Site(s), {locationCount} Location(s), {inspectionsCount} Inspection(s). Time elapsed (milliseconds): {stopwatch.Elapsed.TotalMilliseconds}", (int)Dashboard.LogType.DataPull, uid);

                return Ok(new
                {
                    Success = result,
                    ProcessTime = string.Format("{0}:{1}", stopwatch.Elapsed.TotalMinutes, stopwatch.Elapsed.ToString("ss\\.ff"))
                });
            }
            else
            {
                resultMsg = "GetData() step FAILED";
                stopwatch.Stop();
                log.Info($"RefreshData | Token: {param} | Details: {resultMsg}. Time elapsed (milliseconds): {stopwatch.Elapsed.TotalMilliseconds}"); /* LOG */
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Couldn't get data." }));
            }
        }

        [HttpGet]
        public async Task<IHttpActionResult> GetRemote(string get = "all")
        {
            SimPro.SimProProvider spp = new SimPro.SimProProvider(simProSettings);
            bool success = await spp.GetData(250, 1, true, get);

            return Ok(new { Success = success });
        }

        [HttpGet]
        public async Task<IHttpActionResult> GetFullRemote(string get = "all")
        {
            SimPro.SimProProvider spp = new SimPro.SimProProvider(simProSettings);
            bool result = await spp.GetData(250, 1, false, get);

            return Ok(new { Success = result });
        }

        [HttpGet]
        public async Task<IHttpActionResult> GetCached(string get)
        {
            string data = "";
            using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}{get}_data.json"))
            {
                data = await file.ReadToEndAsync();
            }
            return Ok(data);
        }

        [HttpGet]
        public async Task<IHttpActionResult> SyncLocal(string get = "all")
        {
            string token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            string username = TokenService.ValidateToken(token);
            if (username == null && !Api.IsApplicationDevMode()) { return ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." })); }

            Data.Users user = await Services.UserService.GetUserByUsername(username);
            if (user == null || !user.IsAuthorised)
            {
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "User not found." }));
            }

            return Ok("Endpoint no longer operational.");   // INTERUPT


            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Start();

            //// Load from cache
            //string result = "";
            //string data = "";// GetCached();
            //int clientCount = 0;
            //int sitesCount = 0;
            //int locationCount = 0;
            //int inspectionsCount = 0;


            //// CLIENTS
            //if (get == "all" || get == "clients")
            //{
            //    using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}customers_data.json"))
            //    {
            //        data = file.ReadToEnd();
            //        if (data != null && !string.IsNullOrEmpty(data.ToString()))
            //        {
            //            CustomerContainer customerContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<CustomerContainer>(data.ToString());
            //            if (customerContainer != null && customerContainer.Items.Any())
            //            {
            //                int count = 0;

            //                // Use data to update local store
            //                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
            //                {
            //                    foreach (CustomerListItem customer in customerContainer.Items)
            //                    {
            //                        count++;
            //                        // Clients
            //                        Data.Clients client = await db.Clients.FirstOrDefaultAsync(x => x.SimProId == customer.ID);
            //                        if (client == null) // Add new
            //                        {
            //                            client = new Data.Clients()
            //                            {
            //                                ClientName = customer.CompanyName.Trim(),
            //                                SimProId = customer.ID
            //                            };
            //                            Data.Clients c = db.Clients.Add(client);
            //                            if (c != null) { clientCount++; }
            //                        }

            //                        await db.SaveChangesAsync();
            //                    }

            //                }
            //                result += $"{count} clients, {clientCount} new clients added. ";
            //            }
            //        }
            //    }
            //}


            //// SITES
            //if (get == "all" || get == "sites")
            //{
            //    data = "";  // Reset
            //    using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}sites_data.json"))
            //    {
            //        data = file.ReadToEnd();
            //        if (data != null && !string.IsNullOrEmpty(data.ToString()))
            //        {
            //            SiteContainer siteContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<SiteContainer>(data.ToString());
            //            if (siteContainer != null && siteContainer.Items.Any())
            //            {
            //                int count = 0;

            //                // Use data to update local store
            //                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
            //                {
            //                    foreach (SiteListItem site in siteContainer.Items.Where(x => !x.Archived))
            //                    {
            //                        count++;
            //                        // Sites
            //                        Data.Sites tempSite = await db.Sites.FirstOrDefaultAsync(x => x.SimProId == site.ID);
            //                        if (tempSite == null) // Add new
            //                        {
            //                            if (site.Customers.Any())
            //                            {
            //                                foreach (SimpleCustomer customer in site.Customers)
            //                                {
            //                                    Data.Clients client = await db.Clients.FirstOrDefaultAsync(x => x.SimProId == customer.ID);
            //                                    if (client != null)
            //                                    {
            //                                        tempSite = new Data.Sites()
            //                                        {
            //                                            SiteName = site.Name.Trim(),
            //                                            SimProId = site.ID,
            //                                            Street = site.Address.StreetAddress.Trim(),
            //                                            City = site.Address.City.Trim(),
            //                                            State = site.Address.State.Trim(),
            //                                            PostCode = site.Address.PostalCode.Trim(),
            //                                            ClientId = client.Id,
            //                                            IsActive = !site.Archived
            //                                        };
            //                                        Data.Sites s = db.Sites.Add(tempSite);
            //                                        if (s != null) { sitesCount++; }
            //                                    }
            //                                }
            //                            }
            //                        }
            //                        await db.SaveChangesAsync();
            //                    }

            //                }
            //                result += $"{count} sites, {sitesCount} new sites added. ";
            //            }
            //        }
            //    }
            //}


            //// LOCATIONS
            //if (get == "all" || get == "locations")
            //{
            //    data = "";  // Reset
            //    using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}assets_data.json"))
            //    {
            //        data = file.ReadToEnd();
            //        if (data != null && !string.IsNullOrEmpty(data.ToString()))
            //        {
            //            AssetContainer assetContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<AssetContainer>(data.ToString());
            //            if (assetContainer != null && assetContainer.Items.Any())
            //            {
            //                int count = 0;
            //                sitesCount = 0;

            //                // Use data to update local store
            //                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
            //                {
            //                    foreach (AssetListItem asset in assetContainer.Items)
            //                    {
            //                        count++;
            //                        Data.Sites site = await db.Sites.FirstOrDefaultAsync(x => x.SimProId == asset.Site.ID);
            //                        if (site != null)
            //                        {
            //                            sitesCount++;
            //                            foreach (SimPro.DTO.Models.CustomFieldItem cfi in asset.CustomFields)
            //                            {
            //                                if (cfi.CustomField.Name == "Building Name / Number" && !string.IsNullOrEmpty(cfi.Value))
            //                                {
            //                                    Data.Locations location = await db.Locations.FirstOrDefaultAsync(x => x.SimProId == asset.ID);
            //                                    if (location == null)   // Add location
            //                                    {
            //                                        location = new Data.Locations()
            //                                        {
            //                                            LocationName = cfi.Value.Trim(),
            //                                            SimProId = asset.ID,
            //                                            SiteId = site.Id,
            //                                        };
            //                                        Data.Locations l = db.Locations.Add(location);
            //                                        if (l != null) { locationCount++; }
            //                                    }
            //                                    break;
            //                                }
            //                            }
            //                        }
            //                        await db.SaveChangesAsync();
            //                    }
            //                }
            //                result += $"{count} locations, {locationCount} new locations added from {sitesCount} sites. ";
            //            }
            //        }
            //    }
            //}


            //// INSPECTIONS
            //if (get == "all" || get == "inspections")
            //{
            //    data = "";  // Reset
            //    using (System.IO.StreamReader file = new System.IO.StreamReader($"{simProSettings.CachePath}jobs_data.json"))
            //    {
            //        data = file.ReadToEnd();
            //        if (data != null && !string.IsNullOrEmpty(data.ToString()))
            //        {
            //            JobContainer jobContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<JobContainer>(data.ToString());
            //            if (jobContainer != null && jobContainer.Items.Any())
            //            {

            //                int adminUserId = int.Parse(ConfigurationManager.AppSettings["AS_API_AdminUserId"].ToString());
            //                int unassignedUserId = int.Parse(ConfigurationManager.AppSettings["AS_API_UnassignedUserId"].ToString());
            //                int count = 0;

            //                // Use data to update local store
            //                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
            //                {
            //                    foreach (JobListItem job in jobContainer.Items)
            //                    {
            //                        count++;
            //                        Data.Clients client = await db.Clients.FirstOrDefaultAsync(x => x.SimProId == job.Customer.ID);
            //                        Data.Sites site = await db.Sites.FirstOrDefaultAsync(x => x.SimProId == job.Site.ID);
            //                        if (site != null && client != null)
            //                        {
            //                            // Inspections
            //                            Data.Inspections inspection = await db.Inspections.FirstOrDefaultAsync(x => x.SimProId == job.ID);
            //                            if (inspection == null) // Add new
            //                            {
            //                                inspection = new Data.Inspections()
            //                                {
            //                                    ClientId = client.Id,
            //                                    SiteId = site.Id,
            //                                    /*InspectionTypeId = null,*/
            //                                    InspectionStatusId = await GetInspectionStageId(job.Stage),
            //                                    UserId = unassignedUserId,
            //                                    DateCreated = DateTime.Now,
            //                                    DateModified = DateTime.Now,
            //                                    ModifiedUserId = adminUserId,
            //                                    CreatedUserId = adminUserId,
            //                                    SimProId = job.ID
            //                                };
            //                                Data.Inspections i = db.Inspections.Add(inspection);
            //                                if (i != null) { inspectionsCount++; }
            //                            }
            //                            await db.SaveChangesAsync();
            //                        }
            //                    }

            //                }
            //                result += $"{count} inspections, {inspectionsCount} new inspections added. ";
            //            }
            //        }
            //    }
            //}

            //stopwatch.Stop();
            //log.Info($"SyncLocal | User: {user.Id} | Details: SimPro data cache ({get}) => Database. New data added: {clientCount} Client(s), {sitesCount} Site(s), {locationCount} Location(s), {inspectionsCount} Inspection(s). Time elapsed (milliseconds): {stopwatch.Elapsed.TotalMilliseconds}"); /* LOG */
            //string asLog = new AnchorSafe.Data.ASLogs().AddLogEntry($"Sync Data (SimPro) - SimPro data cache ({get}) => Database. New data added: {clientCount} Client(s), {sitesCount} Site(s), {locationCount} Location(s), {inspectionsCount} Inspection(s). Time elapsed (milliseconds): {stopwatch.Elapsed.TotalMilliseconds}", (int)Dashboard.LogType.DataPull, user.Id);

            //return Ok(new
            //{
            //    Success = result,
            //    ProcessTime = string.Format("{0}:{1}", stopwatch.Elapsed.TotalMinutes, stopwatch.Elapsed.ToString("ss\\.ff"))
            //});
        }

        [HttpGet]
        public async Task<IHttpActionResult> SyncToSimPro(int id)
        {
            string token = TokenService.GetTokenFromHeader(Request.Headers.Authorization);
            string username = TokenService.ValidateToken(token);
            if (username == null && !Api.IsApplicationDevMode()) { return ResponseMessage(Request.CreateResponse(HttpStatusCode.Forbidden, new { result = "Access Denied." })); }

            Data.Users user = await Services.UserService.GetUserByUsername(username);
            if (user == null || !user.IsAuthorised)
            {
                return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "User not found." }));
            }


            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            string msg = string.Empty;

            using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
            {
                Data.Inspections inspection = await db.Inspections.FindAsync(id);
                if (inspection == null)
                    return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "No inspection found." }));

                if (inspection.SimProId == null)
                    return ResponseMessage(Request.CreateResponse(HttpStatusCode.BadRequest, new { result = "Inspection must match an existing SimPRO job." }));


                // Get existing job from SimPRO
                SimPro.SimProProvider spp = new SimPro.SimProProvider(simProSettings);
                Job simProJob = spp.GetSingle<Job>(inspection.SimProId.Value, "jobs");
                if (simProJob != null)
                {
                    // Update data
                    if (simProJob.Site.ID != inspection.SiteId)
                    {
                        // Update job site?
                    }
                    foreach (AnchorSafe.SimPro.DTO.Models.CustomField cf in simProJob.CustomFields)
                    {
                        if (cf.Name == "Building Name / Number" && !string.IsNullOrEmpty(cf.ListItems.FirstOrDefault().ToString()))
                        {
                            if (cf.ListItems.FirstOrDefault().ToString() != inspection.Locations.ToString())
                            {
                                // Update job building custom field
                            }
                        }
                    }

                    // TODO: Update job assets (anchor, strop etc counts)

                    simProJob.Status = new SimPro.DTO.Models.Status
                    {
                        ID = inspection.InspectionStatusId.Value,
                        Name = inspection.InspectionStatus.Description
                    };

                    msg = string.Format("SimPRO job {0}: {1}", simProJob.ID, simProJob.Name);


                    // Sync with SimPRO
                    bool syncResult = spp.Update<Job>(simProJob, simProJob.ID, "job");
                    if (syncResult)
                    {
                        // Update all complete inspections with job id - SimProSyncDate
                        List<Data.Inspections> inspections = await db.Inspections.Where(x => x.InspectionStatusId == (int)AnchorSafe.API.Models.DTO.InspectionStatus.Queued).ToListAsync();
                        foreach (Data.Inspections ins in inspections)
                        {
                            ins.SimProSyncDate = DateTime.Now;
                            ins.InspectionStatusId = (int)AnchorSafe.API.Models.DTO.InspectionStatus.Archived;
                        }
                        await db.SaveChangesAsync();
                    }

                    msg += ". SYNC: " + (syncResult ? "SUCCESS" : "FAILED");
                }
            }

            stopwatch.Stop();
            log.Info($"SyncToSimPro | User: {user.Id} | Details: Inspection sync item ({id}) => SimPRO. {msg}. Time elapsed (milliseconds): {stopwatch.Elapsed.TotalMilliseconds}"); /* LOG */

            return Ok(new
            {
                Success = msg,
                ProcessTime = string.Format("{0}:{1}", stopwatch.Elapsed.TotalMinutes, stopwatch.Elapsed.ToString("ss\\.ff"))
            });
        }


        private async Task<Int32> GetInspectionStageId(string stage)
        {
            int id = 1; // Unassigned/Pending
            if (!string.IsNullOrEmpty(stage))
            {
                using (Data.AnchorSafe_DbContext db = new AnchorSafe.Data.AnchorSafe_DbContext())
                {
                    JobStageEnum jobStage = (JobStageEnum)Enum.Parse(typeof(JobStageEnum), stage);
                    string jobStageDescription = jobStage.GetDescription(); //JobStage.GetDisplayAttributeFrom((Enum)jobStage, typeof(JobStageEnum));
                    Data.InspectionStatus status = await db.InspectionStatus.FirstOrDefaultAsync(x => x.Description == jobStageDescription);
                    if (status != null) { id = status.Id; }
                }
            }
            return id;
        }
    }
}
