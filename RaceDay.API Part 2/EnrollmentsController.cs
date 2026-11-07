using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers
{
    [Route("api/enrollments")]
    [ApiController]
    public class EnrollmentsController : RaceDayControllerBase
    {
        public EnrollmentsController(RaceDayDbContext context) : base(context) { }

        /// <summary>
        /// Lists enrolments for the events the logged-in Organiser created.
        /// Organiser only (the plan's "Admin" route uses the Organiser role).
        /// </summary>
        /// <response code="200">Array of enrolments (with user and category).</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Participants may not list all enrolments.</response>
        // GET: api/enrollments
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Enrollment>>> GetEnrollments()
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            return await EnrollmentsOfMyEvents()
                .Include(e => e.User)
                .Include(e => e.Category)
                .ToListAsync();
        }

        /// <summary>
        /// Gets one enrolment. A Participant can only see their own; an Organiser can only see
        /// enrolments for their own events.
        /// </summary>
        /// <response code="200">The enrolment.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">It belongs to someone else.</response>
        /// <response code="404">No such enrolment.</response>
        // GET: api/enrollments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Enrollment>> GetEnrollment(int id)
        {
            var denied = RequireLogin();
            if (denied != null) return denied;

            var enrollment = await _context.Enrollments
                .Include(e => e.User)
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.EnrollmentId == id);

            if (enrollment == null) return NotFound();

            var allowed = IsOrganiser
                ? await OrganiserOwnsEnrollmentAsync(id)
                : enrollment.UserId == CurrentUserId;

            if (!allowed) return ForbiddenResult("You do not have access to this enrolment.");
            return enrollment;
        }

        /// <summary>
        /// Lists one participant's enrolments. A Participant can only ask for their OWN id.
        /// (An Organiser sees only that person's enrolments in the organiser's own events.)
        /// </summary>
        /// <response code="200">Array of enrolments.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">A participant asked for someone else's enrolments.</response>
        // GET: api/enrollments/user/5
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<Enrollment>>> GetUserEnrollments(int userId)
        {
            var denied = RequireLogin();
            if (denied != null) return denied;

            if (!IsOrganiser && CurrentUserId != userId)
            {
                return ForbiddenResult("You can only view your own enrolments.");
            }

            // Organisers are limited to enrolments in their own events; participants see all of their own
            var source = IsOrganiser ? EnrollmentsOfMyEvents() : _context.Enrollments;

            return await source
                .Where(e => e.UserId == userId)
                .Include(e => e.Category)
                .ToListAsync();
        }

        /// <summary>
        /// Enters the logged-in Participant into a race category. The user comes from the SESSION
        /// (a participant cannot enrol someone else). Checks: category exists, not already enrolled,
        /// category not full, and the participant meets the minimum age.
        /// </summary>
        /// <response code="201">The new enrolment (payment starts as "Pending").</response>
        /// <response code="400">Category missing, or the participant is below the minimum age.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Only Participants can enrol.</response>
        /// <response code="409">Already enrolled in this category, or the category is full.</response>
        // POST: api/enrollments
        [HttpPost]
        public async Task<ActionResult<Enrollment>> PostEnrollment(Enrollment enrollment)
        {
            var denied = RequireRole(ParticipantRole);
            if (denied != null) return denied;

            var me = CurrentUserId!.Value;

            var category = await _context.EventCategories.FindAsync(enrollment.CategoryId);
            if (category == null)
            {
                return BadRequest(new { message = "That category does not exist." });
            }

            var user = await _context.Users.FindAsync(me);
            if (user == null)
            {
                return Unauthorized(new { message = "Your account no longer exists." });
            }

            if (await _context.Enrollments.AnyAsync(e => e.UserId == me && e.CategoryId == category.CategoryId))
            {
                return Conflict(new { message = "You are already enrolled in this category." });
            }

            var taken = await _context.Enrollments.CountAsync(e => e.CategoryId == category.CategoryId);
            if (taken >= category.MaxParticipants)
            {
                return Conflict(new { message = "This category is full." });
            }

            if (category.MinAge.HasValue && user.DateOfBirth.HasValue)
            {
                var age = AgeOn(user.DateOfBirth.Value, category.StartDateTime);
                if (age < category.MinAge.Value)
                {
                    return BadRequest(new { message = $"You must be at least {category.MinAge} years old for this category." });
                }
            }

            // Build a fresh enrolment instead of trusting the body: the participant cannot
            // choose their own user id, date, or mark themselves as "Paid".
            var newEnrollment = new Enrollment
            {
                UserId = me,
                CategoryId = category.CategoryId,
                EnrollmentDate = DateTime.UtcNow,
                PaymentStatus = "Pending",
                BibNumber = enrollment.BibNumber,
                EmergencyContact = enrollment.EmergencyContact
            };

            _context.Enrollments.Add(newEnrollment);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetEnrollment", new { id = newEnrollment.EnrollmentId }, newEnrollment);
        }

        /// <summary>
        /// Updates an enrolment's payment status, bib number or emergency contact.
        /// Only the Organiser who owns the event.
        /// </summary>
        /// <response code="200">The updated enrolment.</response>
        /// <response code="400">Id mismatch.</response>
        /// <response code="403">Participant, or an organiser who does not own the event.</response>
        /// <response code="404">No such enrolment.</response>
        // PUT: api/enrollments/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEnrollment(int id, Enrollment enrollment)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (id != enrollment.EnrollmentId) return BadRequest();

            var existing = await _context.Enrollments.FindAsync(id);
            if (existing == null) return NotFound();

            if (!await OrganiserOwnsEnrollmentAsync(id))
            {
                return ForbiddenResult("You can only edit enrolments for events that you created.");
            }

            // The participant and category cannot be changed - only these details
            existing.PaymentStatus = enrollment.PaymentStatus;
            existing.BibNumber = enrollment.BibNumber;
            existing.EmergencyContact = enrollment.EmergencyContact;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        /// <summary>
        /// Cancels an enrolment. The Participant who owns it, or the Organiser of that event.
        /// </summary>
        /// <response code="204">Cancelled.</response>
        /// <response code="403">It belongs to someone else.</response>
        /// <response code="404">No such enrolment.</response>
        /// <response code="409">A result has already been recorded for it.</response>
        // DELETE: api/enrollments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEnrollment(int id)
        {
            var denied = RequireLogin();
            if (denied != null) return denied;

            var enrollment = await _context.Enrollments.FindAsync(id);
            if (enrollment == null) return NotFound();

            var allowed = IsOrganiser
                ? await OrganiserOwnsEnrollmentAsync(id)
                : enrollment.UserId == CurrentUserId;

            if (!allowed) return ForbiddenResult("You can only cancel your own enrolments.");

            try
            {
                _context.Enrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "A result has already been recorded for this enrolment." });
            }
            return NoContent();
        }

        // Age in whole years on a given date (used for the category's minimum-age rule)
        private static int AgeOn(DateTime dateOfBirth, DateTime onDate)
        {
            var age = onDate.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > onDate.AddYears(-age).Date) age--;  // birthday not reached yet this year
            return age;
        }
    }
}