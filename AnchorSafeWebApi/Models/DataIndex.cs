using System;

namespace AnchorSafe.API.Models.v2
{
    public class DataIndex
    {
        public DateTime Timestamp { get; set; }
        public long PageSize { get; set; }

        public DataIndexStat Inspections { get; set; } = new DataIndexStat();
        public DataIndexStat Definitions { get; set; } = new DataIndexStat();
        public DataIndexStat Categories { get; set; } = new DataIndexStat();
        public DataIndexStat Clients { get; set; } = new DataIndexStat();
        public DataIndexStat Sites { get; set; } = new DataIndexStat();
        public DataIndexStat Locations { get; set; } = new DataIndexStat();
    }

    public class DataIndexStat
    {
        public long Count { get; set; }
        public long Pages { get; set; }
    }
}