// <copyright file="OAuthAuthenticationHandler.cs" company="open-source" >
//  Copyright binary (c) 2012  by Johnny Halife, Juan Pablo Garcia, Mauro Krikorian, Mariano Converti,
//                                Damian Martinez, Nicolas Bello Camilletti, and Ezequiel Morito
//   
//  Redistribution and use in source and binary forms, with or without modification, are permitted.
//
//  The names of its contributors may not be used to endorse or promote products derived from this software without specific prior written permission.
//
//  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// </copyright>

namespace Microsoft.Net.Http
{
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    using Microsoft.IdentityModel.Claims;
    using Microsoft.IdentityModel.Configuration;
    using Microsoft.IdentityModel.Swt;

    /// <summary>
    /// Authenticates the ongoing request using Windows Identity Foundation and 
    /// SimpleWebToken (wif.swf). Grabs the token from the header and performs the authentication
    /// due to WebApi requirement for ASP.NET we're also suppressing Forms Redirect by leveraging
    /// aspnet.suppressformsredirect Package.
    /// </summary>
    public class OAuthAuthenticationHandler : DelegatingHandler
    {
        /// <summary>
        /// Key used to stuff the Request with the authenticated principal after a
        /// successful login.
        /// </summary>
        public const string MessagePrincipalKey = "identity.currentprincipal";

        /// <summary>
        /// Current Windows Identify Foundation Configuration Instance
        /// </summary>
        private ServiceConfiguration serviceConfiguration;

        /// <summary>
        /// Gets a Singleton implementation of the Windows Identity Foundation configuration.
        /// </summary>
        public ServiceConfiguration ServiceConfiguration
        {
            get
            {
                if (this.serviceConfiguration == null)
                    this.serviceConfiguration = new ServiceConfiguration();

                if (!this.serviceConfiguration.IsInitialized)
                    this.serviceConfiguration.Initialize();

                return this.serviceConfiguration;
            }
        }

        /// <summary>
        /// Checks, extracs, and parses the SimpleWebToken from the Authentication Header.
        /// </summary>
        /// <param name="request">Ongoing request.</param>
        /// <returns>A parsed SimpleWebToken.</returns>
        internal static SimpleWebToken ExtractTokenFromHeader(HttpRequestMessage request)
        {
            var authorizationHeader = request.Headers.Authorization;

            if (authorizationHeader.Scheme == "OAuth")
                return new SimpleWebToken(authorizationHeader.Parameter);

            return null;
        }

        /// <summary>
        /// Performs OAuth Authentication of the on going request. Turns OAuth authentication into mandatory.
        /// </summary>
        /// <param name="request">Ongoing request</param>
        /// <param name="cancellationToken">Async Cancellation token</param>
        /// <returns>Task execution for the ASP.NET Web Api pipeline</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = ExtractTokenFromHeader(request);

            try
            {
                var identities = this.ServiceConfiguration.SecurityTokenHandlers.ValidateToken(token);
                var principal = ClaimsPrincipal.CreateFromIdentities(identities);

                // We authenticate the ongoing thread but also we stuff the principal on the message
                // after a brief chat with Glenn Block we understood that the only way to authenticate
                // a request and continue using it is to stuff it's authenticated user throught on 
                // the message properties.
                request.Properties[MessagePrincipalKey] = principal;
            }
            catch 
            {
                // We trap the exception from the validator (validation fails with an exception)
                return Task<HttpResponseMessage>.Factory.StartNew(() =>
                {
                    // HACK: Prevent ASP.NET Forms Authentication to redirect the user to the login page.
                    // This thread-safe approach adds a header with the suppression to be read on the 
                    // OnEndRequest event of the pipelien. In order to fully support the supression you should have the ASP.NET Module
                    // that does this (SuppressFormsAuthenticationRedirectModule).  
                    var response = new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
                    response.Headers.Add(SuppressFormsAuthenticationRedirectModule.SuppressFormsHeaderName, "true");

                    return response;
                });
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}