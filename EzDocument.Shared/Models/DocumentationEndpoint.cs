using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzDocument.Shared.Models
{
    public class DocumentationEndpoint
    {
        public string HttpAction { get; set; } = null!;

        public string EndpointUrl { get; set; } = null!;

        public string Description { get; set; } = string.Empty;

        public object Payload { get; set; } = null!;

        public object QueryParams { get; set; } = null!;

        public object SuccessResponsePayload { get; set; } = null!;

        public object ErrorResponsePayload { get; set; } = null!;
    }
}
