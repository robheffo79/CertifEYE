using AnchorSafe.SimPro.DTO.Models;
using AnchorSafe.SimPro.DTO.Models.Asset;
using AnchorSafe.SimPro.DTO.Models.People;
using AnchorSafe.SimPro.DTO.Models.Projects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AnchorSafe.SimPro
{
    public class SimProProvider
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly SimProSettings settings;
        private List<SimProRequestResource> resources;

        public SimProProvider(SimProSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            resources = new List<SimProRequestResource>()
            {
                { new SimProRequestResource() { ResourceName = "jobs", EndPoint = $"companies/{settings.CompanyId}/jobs/", CustomColumns = "?columns=ID,Type,Customer,Site,Name,Stage,CustomFields,CompletedDate" } },
                { new SimProRequestResource() { ResourceName = "job", EndPoint = $"companies/{settings.CompanyId}/jobs/[JOB_ID]", CustomColumns = "" } },
                { new SimProRequestResource() { ResourceName = "customers", EndPoint = $"companies/{settings.CompanyId}/customers/companies/", CustomColumns = "?" } },
                { new SimProRequestResource() { ResourceName = "employees", EndPoint = $"companies/{settings.CompanyId}/employees/", CustomColumns = "?columns=ID,Name,Position" } },
                { new SimProRequestResource() { ResourceName = "sites", EndPoint = $"companies/{settings.CompanyId}/sites/", CustomColumns = "?columns=ID,Name,Address,Customers,Archived" } },
                { new SimProRequestResource() { ResourceName = "assets", EndPoint = $"companies/{settings.CompanyId}/customerAssets/", CustomColumns = "?columns=ID,AssetType,Site,CustomFields" } },
            };
        }

        public String ProviderName => "SimPRO";

        public bool Test()
        {
            HttpResponseMessage response = Request($"info/").Result;
            log.Info($"Test | Details: Test Request to SimPro API | Result: {response.IsSuccessStatusCode}"); /* LOG */
            return null == response ? false : response.IsSuccessStatusCode;
        }

        public async Task<Boolean> GetData(int ps = -1, int p = 1, bool latest = true, string get = "all")
        {
            string sparams = "";
            if (ps > 0 && ps <= 250) { sparams += "&pageSize=" + ps; }

            int ok = 0;
            if (get == "jobs" || get == "all")
                if (await FetchDataResource<JobListItem, JobContainer>(resources.Where(x => x.ResourceName == "jobs").FirstOrDefault(), sparams, p, latest)) { ok++; }
            if (get == "customers" || get == "clients" || get == "all")
                if (await FetchDataResource<CustomerListItem, CustomerContainer>(resources.Where(x => x.ResourceName == "customers").FirstOrDefault(), sparams, p, latest)) { ok++; }
            if (get == "employees" || get == "all")
                if (await FetchDataResource<EmployeeListItem, EmployeeContainer>(resources.Where(x => x.ResourceName == "employees").FirstOrDefault(), sparams, p, latest)) { ok++; }
            if (get == "sites" || get == "all")
                if (await FetchDataResource<SiteListItem, SiteContainer>(resources.Where(x => x.ResourceName == "sites").FirstOrDefault(), sparams, p, latest)) { ok++; }
            if (get == "assets" || get == "all")
                if (await FetchDataResource<AssetListItem, AssetContainer>(resources.Where(x => x.ResourceName == "assets").FirstOrDefault(), sparams, p, latest)) { ok++; }

            return ok > 0;
        }

        public T GetSingle<T>(int id, string resourceName)
        {
            SimProRequestResource resource = resources.Where(x => x.ResourceName == resourceName).FirstOrDefault();
            HttpResponseMessage res = Request(resource.EndPoint + id.ToString()).Result;
            T item = res.Content.ReadAsAsync<T>().Result;
            if (item != null)
            {
                return item;
            }
            else { throw new Exception("No item found for ID " + id + "."); }
        }

        public bool Update<T>(T item, int id, string resourceName)
        {
            SimProRequestResource resource = resources.Where(x => x.ResourceName == resourceName).FirstOrDefault();
            HttpResponseMessage res = Request(resource.EndPoint + id.ToString(), item).Result;
            bool result = res.Content.ReadAsAsync<Boolean>().Result;
            if (result)
            {
                return result;
            }
            else { throw new Exception("Could not update item with ID " + id + "."); }
        }

        public async Task<Boolean> FetchDataResource<T, U>(SimProRequestResource resource, string sparams, int p, bool latest, int id = -1)
        {
            bool result = false;
            DateTime lastUpdate = GetLastCacheUpdate(resource.ResourceName);
            List<T> dataList = await RequestData<T>(resource.EndPoint, null, $"{resource.CustomColumns}{sparams}", p, true, ((latest) ? lastUpdate.ToString() : ""));
            if (dataList != null && dataList.Any())
            {
                // If 'latest' then append data. Else write all.
                if (latest)
                {
                    int remoteCount = dataList.Count();
                    dataList = AppendData<T, U>(dataList, $"{settings.CachePath}{resource.ResourceName}_data.json");
                }

                SimpleContainer<T> objectContainer = new SimpleContainer<T>();
                objectContainer.LastUpdated = DateTime.Now;
                objectContainer.Items = dataList;

                if (objectContainer.Items.Any())
                {
                    //objectContainer.Items.Sort();   // Re-order by ID
                    using (System.IO.StreamWriter file = new System.IO.StreamWriter($"{settings.CachePath}{resource.ResourceName}_data.json"))
                    {
                        file.Write(new System.Text.StringBuilder(Newtonsoft.Json.JsonConvert.SerializeObject(objectContainer).ToString()));
                    }
                }
                result = true;
                log.Info($"FetchDataResource | Details: Pulled {resource.ResourceName} (id {id}) data from SimPRO API | Result: {dataList.Count} record(s)"); /* LOG */
            }
            return result;
        }

        public async Task<List<T>> RequestData<T>(string req, Object payload = null, string filter = "", int page = 1, bool fetchAllPages = false, string since = "")
        {
            HttpResponseMessage res = await Request(req, payload, filter + ((page > 1) ? "&page=" + page : ""), since);
            int pages = 1;
            int.TryParse(res.Headers.GetValues("Result-Pages").FirstOrDefault(), out pages);
            List<T> items = res.Content.ReadAsAsync<List<T>>().Result;
            if (fetchAllPages && pages > 1)
            {
                if (pages >= page++)
                {
                    res.Dispose(); // Clear for next request
                    List<T> newItems = await RequestData<T>(req, payload, filter, page++, fetchAllPages, since);
                    items.AddRange(newItems);
                    items = items.Distinct().ToList();
                }
            }
            return items;
        }

        public List<T> AppendData<T, U>(List<T> list, string cacheFile)
        {
            string data = "";
            if (System.IO.File.Exists(cacheFile))
            {
                using (System.IO.StreamReader file = new System.IO.StreamReader(cacheFile))
                {
                    data = file.ReadToEnd();
                    if (data != null && !string.IsNullOrEmpty(data.ToString()))
                    {
                        U itemContainer = Newtonsoft.Json.JsonConvert.DeserializeObject<U>(data.ToString());
                        if (itemContainer != null)
                        {
                            System.Reflection.PropertyInfo pi = typeof(U).GetProperty("Items");
                            List<T> items = (List<T>)pi.GetValue(itemContainer);
                            list.AddRange(items);
                            IEnumerable<T> distinctlist = list.Distinct();    // Clear duplicate IDs
                            return distinctlist.ToList();
                        }
                    }
                }
            }
            return list;
        }

        private async Task<HttpResponseMessage> Request(String path, Object payload = null, String p = "", String lastModified = "")
        {
            using (HttpClient client = new HttpClient())
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                //String credentials = Credentials(path, out Uri uri, null == payload ? "GET" : "POST", filter);
                string url = new Uri(string.Format("https://{0}/{1}/", settings.Host, settings.Version)).ToString();
                Uri uri = new Uri(new Uri(url), string.Format("{0}{1}", path, p)); // Use relative param to avoid forced trailing slash

                client.BaseAddress = uri;
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(lastModified))
                {
                    DateTime lastModifiedDate;
                    if (DateTime.TryParse(lastModified, out lastModifiedDate))
                    {
                        client.DefaultRequestHeaders.IfModifiedSince = lastModifiedDate.ToUniversalTime();
                        lastModified = lastModifiedDate.ToUniversalTime().ToString("ddd, d MMMM yyyy HH:mm:ss K"); // Reuse for logging
                    }
                }
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Key);

                HttpResponseMessage response = null == payload ? await client.GetAsync(uri.PathAndQuery).ConfigureAwait(false) : await client.PostAsJsonAsync(uri.PathAndQuery, payload);
                response.EnsureSuccessStatusCode();

                return response;
            }
        }

        //private async Task<HttpResponseMessage> Patch(String path, Object payload) { }

        /*private String Credentials(String path, out Uri uri, String method = "GET", String filter = "")
        {
            uri = new Uri(string.Format("https://{0}/{1}/{2}/?{3}", settings.Host, settings.Version, path, filter));

            String timestamp = ((Int64)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString();
            String nonce = Guid.NewGuid().ToString("N");
            String mac = String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n\n", timestamp, nonce, method, uri.PathAndQuery, uri.Host, settings.Port);

            mac = Convert.ToBase64String((new HMACSHA256(Encoding.ASCII.GetBytes(settings.Secret))).ComputeHash(Encoding.ASCII.GetBytes(mac)));

            return String.Format("id=\"{0}\", ts=\"{1}\", nonce=\"{2}\", mac=\"{3}\"", settings.Key, timestamp, nonce, mac);
        }*/


        public DateTime GetLastCacheUpdate(string entity)
        {
            DateTime lastUpdated = DateTime.Now;
            string data = "";  // Reset

            if (System.IO.File.Exists($"{settings.CachePath}{entity}_data.json"))
            {
                using (System.IO.StreamReader file = new System.IO.StreamReader($"{settings.CachePath}{entity}_data.json"))
                {
                    data = file.ReadToEnd();
                    if (data != null && !string.IsNullOrEmpty(data.ToString()))
                    {
                        SiteContainer container = Newtonsoft.Json.JsonConvert.DeserializeObject<SiteContainer>(data.ToString());
                        lastUpdated = container.LastUpdated;
                    }
                }
            }
            return lastUpdated;
        }
    }
}
