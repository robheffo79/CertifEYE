using System.Collections.Generic;

namespace AnchorSafe.API.Models
{
    public class DataPackage
    {
        public List<DTO.Inspection> Inspections { get; set; }
        public List<DTO.Definition> Definitions { get; set; }
        public List<DTO.Category> Categories { get; set; }
        public List<DTO.Client> Clients { get; set; }
        public List<DTO.Site> Sites { get; set; }
        public List<DTO.Location> Locations { get; set; }
        public string Debug { get; set; }

        public DataPackage()
        {
            Inspections = new List<DTO.Inspection>();
            Definitions = new List<DTO.Definition>();
            Categories = new List<DTO.Category>();
            Clients = new List<DTO.Client>();
            Sites = new List<DTO.Site>();
            Locations = new List<DTO.Location>();
        }
    }
}