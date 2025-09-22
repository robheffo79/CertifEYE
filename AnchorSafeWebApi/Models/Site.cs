using System;
using System.Collections.Generic;
using System.Linq;

namespace AnchorSafe.API.Models.DTO
{
    public class Site
    {
        public int Id { get; set; }
        public int? ClientId { get; set; }
        public string SiteName { get; set; }
        public int? SimProId { get; set; }
        public string Street { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Postcode { get; set; }
        public bool IsActive { get; set; }
        public DateTime DateModified { get; set; }
        public DateTime DateCreated { get; set; }
        public List<Location> Locations { get; set; }

        public Site() { }

        public Site(Data.Sites site)
        {
            Id = site.Id;
            ClientId = site.ClientId;
            SiteName = site.SiteName;
            SimProId = site.SimProId;
            Street = site.Street;
            City = site.City;
            State = site.State;
            Postcode = site.PostCode;
            IsActive = site.IsActive;
            DateCreated = site.DateCreated;

            Locations = new List<Location>();
            if (site.Locations.Any())
            {
                foreach (Data.Locations item in site.Locations) { Locations.Add(new Location(item)); }
            }
        }
    }
}