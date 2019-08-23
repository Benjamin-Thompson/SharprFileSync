using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharprFileSync.Services
{
    public class SharprExportResults
    {
        public int TotalFiles { get; set; }
        public int SuccessfulFiles { get; set; }
        public int FailedFiles { get; set; }
    }
}
