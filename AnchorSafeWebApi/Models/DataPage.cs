using System;
using System.Collections.Generic;
using System.Linq;

namespace AnchorSafe.API.Models.v2
{
    public class DataPage<T>
    {
        public DateTime Timestamp { get; }

        public long TotalRecords { get; }
        public long Page { get; }
        public long PageSize { get; }
        public long Pages { get => (TotalRecords + PageSize - 1) / PageSize; }

        public IEnumerable<T> Data { get; }

        public DataPage(DateTime timestamp, long totalRecords, long page, long pageSize, IEnumerable<T> data)
        {
            Timestamp = timestamp;
            TotalRecords = totalRecords;
            Page = page;
            PageSize = pageSize;
            Data = data.ToArray();
        }
    }
}