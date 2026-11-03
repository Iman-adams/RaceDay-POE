using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers
{
    [Route("api/events")]
    [ApiController]
    public class EventsController : RaceDayControllerBase
    {
        public EventsController(RaceDayDbContext context) : base(context) { }

        /// <summary>Lists all events (soonest first), including venue and organiser. Public.</summary>
        /// <response code="200">Array of events.</response>
        // GET: api/events
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Event>>> GetEvents()
        {
            return await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organiser)
                .OrderBy(e => e.EventDate)
                .ToListAsync();
        }

        /// <summary>Gets one event including its Organiser and Venue. Public.</summary>
        /// <response code="200">The event.</response>
        /// <response code="404">No such event.</response>
        // GET: api/events/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Event>> GetEvent(int id)
        {
            var ev = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organiser)
                .FirstOrDefaultAsync(e => e.EventId == id);

            if (ev == null) return NotFound();
            return ev;
        }

        /// <summary>
        /// Creates an event. Organiser only. The organiser is taken from the SESSION,
        /// so an organiser cannot create events in someone else's name.
        /// </summary>
        /// <response code="201">The created event.</response>
        /// <response code="400">Invalid details or the venue does not exist.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Participants may not create events.</response>
        // POST: api/events
        [HttpPost]
        public async Task<ActionResult<Event>> PostEvent(Event ev)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (!await _context.Venues.AnyAsync(v => v.VenueId == ev.VenueId))
            {
                return BadRequest(new { message = "That venue does not exist." });
            }

            ev.EventId = 0;  // the database generates the id
            ev.OrganiserId = CurrentUserId!.Value;  // always the logged-in organiser
            ev.Organiser = null;  // ignore any nested objects in the body
            ev.Venue = null;

            _context.Events.Add(ev);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetEvent", new { id = ev.EventId }, ev);
        }

        /// <summary>Updates an event. Only the Organiser who created it may do this.</summary>
        /// <response code="200">The updated event.</response>
        /// <response code="400">Id mismatch or the venue does not exist.</response>
        /// <response code="403">Participant, or an organiser who does not own this event.</response>
        /// <response code="404">No such event.</response>
        // PUT: api/events/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEvent(int id, Event ev)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (id != ev.EventId) return BadRequest();

            var existing = await _context.Events.FindAsync(id);
            if (existing == null) return NotFound();

            if (existing.OrganiserId != CurrentUserId)
            {
                return ForbiddenResult("You can only edit events that you created.");
            }

            if (!await _context.Venues.AnyAsync(v => v.VenueId == ev.VenueId))
            {
                return BadRequest(new { message = "That venue does not exist." });
            }

            // Copy only the fields an organiser is allowed to change
            existing.VenueId = ev.VenueId;
            existing.Title = ev.Title;
            existing.Description = ev.Description;
            existing.EventDate = ev.EventDate;
            existing.Status = ev.Status;
            existing.EventType = ev.EventType;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        /// <summary>Deletes an event. Only the Organiser who created it may do this.</summary>
        /// <response code="204">Deleted.</response>
        /// <response code="403">Participant, or an organiser who does not own this event.</response>
        /// <response code="404">No such event.</response>
        /// <response code="409">The event still has categories - delete those first.</response>
        // DELETE: api/events/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound();

            if (ev.OrganiserId != CurrentUserId)
            {
                return ForbiddenResult("You can only delete events that you created.");
            }

            try
            {
                _context.Events.Remove(ev);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "This event still has categories. Delete them first." });
            }
            return NoContent();
        }
    }
}