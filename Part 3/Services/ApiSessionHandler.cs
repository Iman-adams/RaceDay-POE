using System.Net;

namespace RaceDay.MVC.Services
{
    // Sits between the MVC site and the API on every call.
    // Why it is needed: the API remembers who is logged in using a session cookie.
    // A browser would send that cookie automatically, but here the MVC SERVER is the one calling the API, so we must  do it by hand:
    //  1. When the API replies with its session cookie (at login), keep it in the user's MVC session.
    //  2. On every later call, send that cookie back to the API.
    // Each user has their own MVC session, so each user gets their own API session.
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
                // If the API sent us its session cookie, remember it.
                // The header looks like: .RaceDay.Session=abc123; path=/; samesite=lax; httponly
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

                // If the API says "not logged in" (for example its session expired), log the user out here too
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    session.Clear();
                }
            }

            return response;
        }
    }
}