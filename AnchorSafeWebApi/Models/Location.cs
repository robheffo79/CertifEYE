using AnchorSafe.Data;
using System;

namespace AnchorSafe.API.Models.DTO
{
    public class Location
    {
        public int Id { get; set; }
        public string LocationName { get; set; }
        public int? SiteId { get; set; }
        public int? SimProId { get; set; }
        public DateTime DateModified { get; set; }
        public DateTime DateCreated { get; set; }

        public Location() { }

        public Location(Locations location)
        {
            Id = location.Id;
            LocationName = location.LocationName;
            SiteId = location.SiteId;
            SimProId = location.SimProId;
            DateCreated = location.DateCreated;
        }
    }
}