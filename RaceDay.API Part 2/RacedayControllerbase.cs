using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers
{
    // Base class for every controller. It holds the session + role-checking logic in ONE place  so each endpoint only needs two lines to protect itself,
    // for example:
    //     var denied = RequireRole(OrganiserRole);
    //     if (denied != null) return denied;
    // How it works: when a user logs in, AuthController stores their id and role in the server-side SESSION.
    // The browser only keeps a session cookie; the role lives on the server, so the user cannot fake it.
    public abstract class RaceDayControllerBase : ControllerBase
    {
        // Keys used to store values in the session
        public const string SessionUserIdKey = "UserId";
        public const string SessionRoleKey = "UserRole";

        // The only two roles in the system
        public const string OrganiserRole = "Organiser";
        public const string ParticipantRole = "Participant";

        protected readonly RaceDayDbContext _context;

        protected RaceDayControllerBase(RaceDayDbContext context)
        {
            _context = context;
        }

        // ---------------- Who is calling? ----------------
        // We read the session through the feature collection so that a missing session
        // (for example in a unit test that does not set one up) gives null instead of an exception.
        private ISession? CurrentSession => HttpContext?.Features.Get<ISessionFeature>()?.Session;

        /// <summary>The logged-in user's id, or null if nobody is logged in.</summary>
        protected int? CurrentUserId => CurrentSession?.GetInt32(SessionUserIdKey);

        /// <summary>The logged-in user's role, or null if nobody is logged in.</summary>
        protected string? CurrentRole => CurrentSession?.GetString(SessionRoleKey);

        protected bool IsOrganiser => CurrentRole == OrganiserRole;

        // ---------------- Access checks ----------------
        // Each check returns null when the caller is allowed, or an error result to return immediately.

        /// <summary>401 Unauthorized if nobody is logged in.</summary>
        protected ActionResult? RequireLogin()
        {
            if (CurrentUserId == null)
            {
                return Unauthorized(new { message = "You must log in first." });
            }
            return null;
        }

        /// <summary>401 if not logged in, 403 Forbidden if logged in with the wrong role.</summary>
        protected ActionResult? RequireRole(params string[] allowedRoles)
        {
            var notLoggedIn = RequireLogin();
            if (notLoggedIn != null) return notLoggedIn;

            if (CurrentRole == null || !allowedRoles.Contains(CurrentRole))
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = $"This action requires the role: {string.Join(" or ", allowedRoles)}." });
            }
            return null;
        }

        /// <summary>A reusable 403 result for "you are logged in but this is not yours".</summary>
        protected ObjectResult ForbiddenResult(string message)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message });
        }

        // ---------------- Ownership checks (Organisers only manage THEIR OWN events) ----------------
        /// <summary>Is the logged-in organiser the one who created this event?</summary>
        protected Task<bool> OrganiserOwnsEventAsync(int eventId)
        {
            var me = CurrentUserId ?? -1;
            return _context.Events.AnyAsync(e => e.EventId == eventId && e.OrganiserId == me);
        }

        /// <summary>Does this category belong to one of the logged-in organiser's events?</summary>
        protected Task<bool> OrganiserOwnsCategoryAsync(int categoryId)
        {
            var me = CurrentUserId ?? -1;
            return _context.EventCategories.AnyAsync(c => c.CategoryId == categoryId &&
                _context.Events.Any(ev => ev.EventId == c.EventId && ev.OrganiserId == me));
        }

        /// <summary>Does this enrolment belong to a category of one of the logged-in organiser's events?</summary>
        protected Task<bool> OrganiserOwnsEnrollmentAsync(int enrollmentId)
        {
            return EnrollmentsOfMyEvents().AnyAsync(en => en.EnrollmentId == enrollmentId);
        }

        // Every enrolment (across all categories) that belongs to the logged-in organiser's events.
        // Used so an organiser can only ever see enrolments for THEIR events, never someone else's.
        protected IQueryable<Enrollment> EnrollmentsOfMyEvents()
        {
            var me = CurrentUserId ?? -1;
            return _context.Enrollments.Where(en =>
                _context.EventCategories.Any(c => c.CategoryId == en.CategoryId &&
                    _context.Events.Any(ev => ev.EventId == c.EventId && ev.OrganiserId == me)));
        }
    }
}