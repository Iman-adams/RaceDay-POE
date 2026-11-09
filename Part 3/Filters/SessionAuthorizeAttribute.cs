using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Filters
{
    // Put this on a controller (or a single action) to protect it:
    //   [SessionAuthorize]                        - any logged-in user
    //   [SessionAuthorize(RoleNames.Organiser)]   - Organisers only
    // Not logged in  -> sent to the Login page.
    // Wrong role     -> sent to the Access Denied page.
    // (The API checks the role again on every call, so this is for a good user experience, not the only protection.)
    public class SessionAuthorizeAttribute : ActionFilterAttribute
    {
        private readonly string[] _allowedRoles;

        public SessionAuthorizeAttribute(params string[] allowedRoles)
        {
            _allowedRoles = allowedRoles;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;

            // Not logged in?
            if (session.GetInt32(SessionKeys.UserId) == null)
            {
                if (context.Controller is Controller controller)
                {
                    controller.TempData["Info"] = "Please log in to continue.";
                }

                // Remember the page they wanted so we can send them back after login
                var request = context.HttpContext.Request;
                var returnUrl = request.Path + request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                return;
            }

            // Logged in, but is the role allowed?
            var role = session.GetString(SessionKeys.UserRole);
            if (_allowedRoles.Length > 0 && (role == null || !_allowedRoles.Contains(role)))
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
            }
        }
    }
}