using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharprFileSync.Services
{
    public class SharprAddUpdateRequest
    {
        public string refNumber { get; set;  }
        public string filename { get; set; }
        public string data { get; set; }
        public long file_size { get; set; }
        public string category { get; set; }
        public string classification { get; set; }
        public List<string> tags { get; set; }
    }
}
