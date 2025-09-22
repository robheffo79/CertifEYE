using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnchorSafe.Data
{
    public class ReportItems
    {
        public int CategoryId { get; set; }
        public string CatName { get; set; }
        public int CatOrder { get; set; }
        public List<InspectionItems> Items { get; set; }

    }
}
