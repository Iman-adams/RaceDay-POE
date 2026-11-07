using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;
using RaceDay.API.Security;

namespace RaceDay.API.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : RaceDayControllerBase
    {
        private const int MinPasswordLength = 6;

        public AuthController(RaceDayDbContext context) : base(context) { }

        // Registers a new account. The body's "PasswordHash" field holds the plain password the user typed; the API hashes it before saving.
        // Role must be "Organiser" or "Participant".
        
        /// <response code="201">Account created.</response>
        /// <response code="400">Missing/invalid details, password too short, or unknown role.</response>
        /// <response code="409">That email is already registered.</response>
        // POST: /api/auth/register
        [HttpPost("register")]
        [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            // Only the two real roles are allowed - nobody can register as anything else
            if (user.Role != OrganiserRole && user.Role != ParticipantRole)
            {
                return BadRequest(new { message = "Role must be 'Organiser' or 'Participant'." });
            }

            if (string.IsNullOrWhiteSpace(user.PasswordHash) || user.PasswordHash.Length < MinPasswordLength)
            {
                return BadRequest(new { message = $"Password must be at least {MinPasswordLength} characters." });
            }

            // Emails are stored in lower case so "Iman@x.com" and "iman@x.com" are the same account
            var email = user.Email.Trim().ToLower();
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == email))
            {
                return Conflict(new { message = "Email already registered." });
            }

            user.UserId = 0;                       // the database generates the id
            user.Email = email;
            user.CreatedAt = DateTime.UtcNow;
            user.PasswordHash = PasswordHasher.Hash(user.PasswordHash);   // never store the plain password

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetUser", "Users", new { id = user.UserId }, user);
        }

        /// <summary>
        /// Logs in with email + password. On success the user's id and role are stored in the
        /// server-side session and the browser receives a session cookie.
        /// </summary>
        /// <response code="200">Logged in. Returns User_ID, Role and FullName.</response>
        /// <response code="401">Wrong email or password.</response>
        // POST: /api/auth/login
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest login)
        {
            var email = login.Email.Trim().ToLower();
            var password = login.Password ?? login.PasswordHash ?? string.Empty;

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            // Same message for "no such user" and "wrong password" so attackers can't tell which emails exist
            if (user == null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid email or password." });
            }

            // Remember who this is for all later requests (this is the "session")
            HttpContext.Session.Clear();
            HttpContext.Session.SetInt32(SessionUserIdKey, user.UserId);
            HttpContext.Session.SetString(SessionRoleKey, user.Role);

            return Ok(new LoginResponse { UserId = user.UserId, Role = user.Role, FullName = user.FullName });
        }

        /// <summary>Logs out: clears the session on the server.</summary>
        /// <response code="204">Logged out.</response>
        // POST: /api/auth/logout
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return NoContent();
        }

        /// <summary>Returns who is currently logged in (handy for the MVC site and for testing).</summary>
        /// <response code="200">The logged-in user's id and role.</response>
        /// <response code="401">Nobody is logged in.</response>
        // GET: /api/auth/me
        [HttpGet("me")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me()
        {
            var denied = RequireLogin();
            if (denied != null) return denied;

            var user = await _context.Users.FindAsync(CurrentUserId!.Value);
            if (user == null) return Unauthorized(new { message = "Your account no longer exists." });

            return Ok(new LoginResponse { UserId = user.UserId, Role = user.Role, FullName = user.FullName });
        }
    }
}