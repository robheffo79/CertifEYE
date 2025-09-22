using System;

namespace AnchorSafe.API.Models.DTO
{
    public class Client
    {
        public int Id { get; set; }
        public string ClientName { get; set; }
        public int SimProId { get; set; }
        public DateTime DateModified { get; set; }
        public DateTime DateCreated { get; set; }
        //public virtual ICollection<Sites> Sites { get; set; }

        public Client() { }

        public Client(Data.Clients client)
        {
            Id = client.Id;
            ClientName = client.ClientName;
            SimProId = client.SimProId;
            DateCreated = client.DateCreated;
        }
    }
}