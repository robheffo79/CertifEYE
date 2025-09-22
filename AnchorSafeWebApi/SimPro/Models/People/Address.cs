namespace AnchorSafe.SimPro.DTO.Models
{
    public class Address
    {
        [Newtonsoft.Json.JsonProperty("Address")]
        public string StreetAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string PostalCode { get; set; }
        public string Country { get; set; }
    }

    public class BillingAddress : Address { }
}
