using System;

namespace AnchorSafe.API.Models.DTO
{
    public class Category
    {
        public int Id { get; set; }
        public string CategoryName { get; set; }
        public int CategoryOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime DateModified { get; set; }
        public int? ModifiedUserId { get; set; }
        public DateTime DateCreated { get; set; }
        public int? CreatedUserId { get; set; }
        //public virtual ICollection<Definitions> Definitions { get; set; }
        //public virtual ICollection<InspectionItems> InspectionItems { get; set; }

        public Category() { }

        public Category(Data.Categories category)
        {
            Id = category.Id;
            CategoryName = category.CategoryName;
            CategoryOrder = category.CategoryOrder;
            IsActive = category.IsActive;
            DateModified = category.DateModified;
            ModifiedUserId = category.ModifiedUserId;
            DateCreated = category.DateCreated;
            CreatedUserId = category.CreatedUserId;
        }
    }
}