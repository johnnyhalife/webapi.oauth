namespace Microsoft.Net.Http
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Web.Http;

    public abstract class ControllerFilteredMessageProcessingHandler : MessageProcessingHandler
    {
        public IList<string> ConfiguredControllers { get; set; }

        protected abstract HttpRequestMessage ProcessRequestHandler(HttpRequestMessage request, CancellationToken cancellationToken);

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var routeData = request.GetRouteData();
            var controllerName = routeData.Values.ContainsKey("controller") ?
                routeData.Values["controller"].ToString() :
                string.Empty;

            if (this.ConfiguredControllers == null ||
                this.ConfiguredControllers.Any(c => c.Equals(controllerName, StringComparison.OrdinalIgnoreCase)))
            {
                return this.ProcessRequestHandler(request, cancellationToken);
            }

            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            return response;
        }
    }
}
