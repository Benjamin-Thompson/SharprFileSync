using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharprFileSync.Services
{
    public class SharprInitResults
    {
        public int UploadSuccessCount { get; set; }
        public int UploadFailCount { get; set; }
        public int TotalFileCount { get; set; }
    }
}
