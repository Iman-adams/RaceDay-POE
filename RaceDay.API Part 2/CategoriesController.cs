using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers
{
    // This controller has no [Route] on the class because its URLs start with two different
    // prefixes (/api/categories and /api/events/{id}/categories), so each action has its own route.
    [ApiController]
    public class CategoriesController : RaceDayControllerBase
    {
        public CategoriesController(RaceDayDbContext context) : base(context) { }

        /// <summary>Lists every category of every event. Public.</summary>
        /// <response code="200">Array of categories.</response>
        // GET: api/categories
        [HttpGet("api/categories")]
        public async Task<ActionResult<IEnumerable<EventCategory>>> GetCategories()
        {
            return await _context.EventCategories.ToListAsync();
        }

        /// <summary>Gets one category. Public.</summary>
        /// <response code="200">The category.</response>
        /// <response code="404">No such category.</response>
        // GET: api/categories/5
        [HttpGet("api/categories/{id}")]
        public async Task<ActionResult<EventCategory>> GetCategory(int id)
        {
            var category = await _context.EventCategories.FindAsync(id);
            if (category == null) return NotFound();
            return category;
        }

        /// <summary>Lists the categories of one event (e.g. 10km, 21km). Public.</summary>
        /// <response code="200">Array of categories for that event.</response>
        // GET: api/events/5/categories
        [HttpGet("api/events/{eventId}/categories")]
        public async Task<ActionResult<IEnumerable<EventCategory>>> GetCategoriesByEvent(int eventId)
        {
            return await _context.EventCategories.Where(c => c.EventId == eventId).ToListAsync();
        }

        /// <summary>Adds a category to an event. Only the Organiser who owns that event.</summary>
        /// <response code="201">The created category.</response>
        /// <response code="400">Invalid details (distance, fee or capacity).</response>
        /// <response code="403">Participant, or an organiser who does not own the event.</response>
        /// <response code="404">The event does not exist.</response>
        // POST: api/events/5/categories
        [HttpPost("api/events/{eventId}/categories")]
        public async Task<ActionResult<EventCategory>> PostCategory(int eventId, EventCategory category)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (!await _context.Events.AnyAsync(e => e.EventId == eventId)) return NotFound();

            if (!await OrganiserOwnsEventAsync(eventId))
            {
                return ForbiddenResult("You can only add categories to events that you created.");
            }

            var invalid = ValidateCategory(category);
            if (invalid != null) return invalid;

            category.CategoryId = 0;  // the database generates the id
            category.EventId = eventId;  // the event comes from the URL
            category.Event = null;

            _context.EventCategories.Add(category);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetCategory", new { id = category.CategoryId }, category);
        }

        /// <summary>Updates a category. Only the Organiser who owns the event.</summary>
        /// <response code="200">The updated category.</response>
        /// <response code="400">Id mismatch or invalid details.</response>
        /// <response code="403">Participant, or an organiser who does not own the event.</response>
        /// <response code="404">No such category.</response>
        // PUT: api/categories/5
        [HttpPut("api/categories/{id}")]
        public async Task<IActionResult> PutCategory(int id, EventCategory category)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (id != category.CategoryId) return BadRequest();

            var existing = await _context.EventCategories.FindAsync(id);
            if (existing == null) return NotFound();

            if (!await OrganiserOwnsCategoryAsync(id))
            {
                return ForbiddenResult("You can only edit categories of events that you created.");
            }

            var invalid = ValidateCategory(category);
            if (invalid != null) return invalid;

            // A category stays in the same event - only these details can change
            existing.CategoryName = category.CategoryName;
            existing.DistanceKm = category.DistanceKm;
            existing.EntryFee = category.EntryFee;
            existing.MaxParticipants = category.MaxParticipants;
            existing.StartDateTime = category.StartDateTime;
            existing.MinAge = category.MinAge;
            existing.RouteDescription = category.RouteDescription;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        /// <summary>Deletes a category. Only the Organiser who owns the event.</summary>
        /// <response code="204">Deleted.</response>
        /// <response code="403">Participant, or an organiser who does not own the event.</response>
        /// <response code="404">No such category.</response>
        /// <response code="409">People are already enrolled in this category.</response>
        // DELETE: api/categories/5
        [HttpDelete("api/categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            var category = await _context.EventCategories.FindAsync(id);
            if (category == null) return NotFound();

            if (!await OrganiserOwnsCategoryAsync(id))
            {
                return ForbiddenResult("You can only delete categories of events that you created.");
            }

            try
            {
                _context.EventCategories.Remove(category);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "People are already enrolled in this category." });
            }
            return NoContent();
        }

        // Basic sanity checks shared by POST and PUT. Returns an error result, or null if all is well.
        private ActionResult? ValidateCategory(EventCategory category)
        {
            if (category.DistanceKm <= 0)
                return BadRequest(new { message = "DistanceKm must be greater than 0." });
            if (category.EntryFee < 0)
                return BadRequest(new { message = "EntryFee cannot be negative." });
            if (category.MaxParticipants <= 0)
                return BadRequest(new { message = "MaxParticipants must be greater than 0." });
            if (category.MinAge < 0)
                return BadRequest(new { message = "MinAge cannot be negative." });
            return null;
        }
    }
}