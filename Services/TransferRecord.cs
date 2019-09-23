using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharprFileSync.Services
{
    public class SharprTransferRecord
    {
        public string Guid { get; set; }
        public string FileName { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Result { get; set; }
    }
}
