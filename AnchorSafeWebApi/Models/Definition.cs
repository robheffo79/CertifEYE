using System;

namespace AnchorSafe.API.Models.DTO
{
    public class Definition
    {
        public int Id { get; set; }
        public int DefinitionTypeId { get; set; }
        public int CategoryId { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        public DateTime DateModified { get; set; }
        public int? ModifiedUserId { get; set; }
        public DateTime DateCreated { get; set; }
        public int? CreatedUserId { get; set; }
        public int? AssociatedDefinitionId { get; set; }
        public Category Category { get; set; }
        public DefinitionType DefinitionType { get; set; }

        public Definition() { }

        public Definition(Data.Definitions definition)
        {
            Id = definition.Id;
            DefinitionTypeId = definition.DefinitionTypeId;
            CategoryId = definition.CategoryId;
            Description = definition.Description;
            IsActive = definition.IsActive;
            DateModified = definition.DateModified;
            ModifiedUserId = definition.ModifiedUserId;
            DateCreated = definition.DateCreated;
            CreatedUserId = definition.CreatedUserId;
            AssociatedDefinitionId = definition.AssociatedDefinitionId;

            Category = new Category();
            DefinitionType = new DefinitionType();

            if (definition.Categories != null) { Category = new Category(definition.Categories); }
            if (definition.DefinitionTypes != null) { DefinitionType = new DefinitionType(definition.DefinitionTypes); }
        }
    }

    public class DefinitionType
    {
        public int Id { get; set; }
        public string Description { get; set; }

        public DefinitionType() { }
        public DefinitionType(Data.DefinitionTypes definitionType)
        {
            Id = definitionType.Id;
            Description = definitionType.Description;
        }
    }
}