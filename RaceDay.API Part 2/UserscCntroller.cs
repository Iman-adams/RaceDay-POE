using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;
using RaceDay.API.Security;

namespace RaceDay.API.Controllers
{
    [Route("api/users")]
    [ApiController]
    public class UsersController : RaceDayControllerBase
    {
        public UsersController(RaceDayDbContext context) : base(context) { }

        /// <summary>Lists every user. Organiser only (the plan's "Admin" routes use the Organiser role).</summary>
        /// <response code="200">Array of users (password hashes are never returned).</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Participants may not list all users.</response>
        // GET: api/users
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            return await _context.Users.ToListAsync();
        }

        /// <summary>
        /// Gets one user's profile. A Participant can only view their own profile;
        /// an Organiser can view any profile.
        /// </summary>
        /// <response code="200">The user.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">A participant tried to view someone else's profile.</response>
        /// <response code="404">No such user.</response>
        // GET: api/users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var denied = RequireLogin();
            if (denied != null) return denied;

            if (!IsOrganiser && CurrentUserId != id)
            {
                return ForbiddenResult("You can only view your own profile.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return user;
        }

        /// <summary>
        /// Updates the logged-in user's OWN profile (name, email, gender, date of birth and,
        /// optionally, a new password). Role can never be changed here.
        /// </summary>
        /// <response code="200">The updated user.</response>
        /// <response code="400">Invalid details.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Trying to edit someone else's profile.</response>
        /// <response code="404">No such user.</response>
        /// <response code="409">That email belongs to another account.</response>
        // PUT: api/users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, [FromBody] UpdateProfileRequest request)
        {
            var denied = RequireLogin();
            if (denied != null) return denied;

            if (CurrentUserId != id)
            {
                return ForbiddenResult("You can only edit your own profile.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // The new email must not belong to a different account
            var email = request.Email.Trim().ToLower();
            if (await _context.Users.AnyAsync(u => u.UserId != id && u.Email.ToLower() == email))
            {
                return Conflict(new { message = "That email is already used by another account." });
            }

            user.FullName = request.FullName;
            user.Email = email;
            user.Gender = request.Gender;
            user.DateOfBirth = request.DateOfBirth;

            // Only change the password if a new one was supplied (and always hash it)
            if (!string.IsNullOrWhiteSpace(request.PasswordHash))
            {
                if (request.PasswordHash.Length < 6)
                {
                    return BadRequest(new { message = "Password must be at least 6 characters." });
                }
                user.PasswordHash = PasswordHasher.Hash(request.PasswordHash);
            }

            await _context.SaveChangesAsync();
            return Ok(user);
        }

        /// <summary>Deletes a user. Organiser only; you cannot delete your own account.</summary>
        /// <response code="204">Deleted.</response>
        /// <response code="400">Tried to delete yourself.</response>
        /// <response code="403">Participants may not delete users.</response>
        /// <response code="404">No such user.</response>
        /// <response code="409">The user still has events or enrolments, so they cannot be removed.</response>
        // DELETE: api/users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (CurrentUserId == id)
            {
                return BadRequest(new { message = "You cannot delete your own account." });
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            try
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A foreign key (events/enrolments still point at this user) blocked the delete
                return Conflict(new { message = "This user still has events or enrolments." });
            }
            return NoContent();
        }
    }
}