using AnchorSafe.SimPro.DTO.Models.People;
using System.Collections.Generic;

namespace AnchorSafe.SimPro.DTO.Models.Asset
{
    public class AssetContainer : SimpleContainer<AssetListItem> { }

    public class AssetListItem : SimpleItem
    {
        public int ID { get; set; }
        public SimpleAssetType AssetType { get; set; }
        public SimpleSite Site { get; set; }
        public List<CustomFieldItem> CustomFields { get; set; }
    }

    public class SimpleAssetType
    {
        public int ID { get; set; }
        public string Name { get; set; }

    }

    public class AssetType
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public Models.Projects.JobCostCenter JobCostCenter { get; set; }
        public Models.Projects.QuoteCostCenter QuoteCostCenter { get; set; }
        public string DefaultTechnician { get; set; }

        // SimPro Notes: 
        // GET /api/v1.0/companies/{companyID}/setup/assetTypes/{assetTypeID}
        // PATCH /api/v1.0/companies/{companyID}/setup/assetTypes/{assetTypeID}
    }


    // TO DO: Asset Type Custom Fields

}
