using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzDocument.Shared.Models
{
    public class Documentation
    {
        public string BaseUrl { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        public string Version { get; set; } = "1.00";

        public List<DocumentationEndpoint> Endpoints { get; set; } = new List<DocumentationEndpoint>();
    }
}
