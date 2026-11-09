using System.Net;

namespace RaceDay.MVC.Services
{
    public class ApiSessionHandler : DelegatingHandler
    {
        // Must match the cookie name set in the API's Program.cs
        private const string ApiSessionCookieName = ".RaceDay.Session";

        private readonly IHttpContextAccessor _accessor;

        public ApiSessionHandler(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var session = _accessor.HttpContext?.Session;

            // Send the stored API cookie along with this request
            var storedCookie = session?.GetString(SessionKeys.ApiCookie);
            if (!string.IsNullOrEmpty(storedCookie))
            {
                request.Headers.TryAddWithoutValidation("Cookie", storedCookie);
            }

            var response = await base.SendAsync(request, cancellationToken);

            if (session != null)
            {
                if (response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
                {
                    foreach (var header in setCookieHeaders)
                    {
                        var nameAndValue = header.Split(';')[0].Trim();
                        if (nameAndValue.StartsWith(ApiSessionCookieName + "=", StringComparison.Ordinal))
                        {
                            session.SetString(SessionKeys.ApiCookie, nameAndValue);
                        }
                    }
                }

                // If the API says "not logged in", log the user out here too
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    session.Clear();
                }
            }

            return response;
        }
    }
}
