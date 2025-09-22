using System;

namespace AnchorSafe.SimPro
{
    public class SimProSettings
    {
        public String Host { get; set; }
        public Int32 Port { get; set; }
        public String Version { get; set; }
        public String Key { get; set; }
        public String Secret { get; set; }
        public String GrantType { get; set; }
        public Int32 CompanyId { get; set; }
        public String CachePath { get; set; }
    }

    public class SimProRequestResource
    {
        public String ResourceName { get; set; }
        public String EndPoint { get; set; }
        public String CustomColumns { get; set; }
    }
}
