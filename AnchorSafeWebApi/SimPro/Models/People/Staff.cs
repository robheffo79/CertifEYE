namespace AnchorSafe.SimPro.DTO.Models.People
{
    public class StaffMember
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }    // "employee", "contractor", "plant"
        public int TypeId { get; set; }
    }

    public class Salesperson : StaffMember { }
    public class ProjectManager : StaffMember { }
    public class Technician : StaffMember { }
}
