using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharprFileSync.Services
{
    public class SharprSettings
    {
        public string SharprURL { get; set; }
        public string SharprUser { get; set; }
        public string SharprPass { get; set; }
        public string LocalDocumentListName { get; set; }
        public DateTime? InitialExportDate { get; set; }
        public bool NotSet { get; set; }
    }
}
