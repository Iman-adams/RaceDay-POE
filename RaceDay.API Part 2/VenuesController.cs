using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers
{
    [Route("api/venues")]
    [ApiController]
    public class VenuesController : RaceDayControllerBase
    {
        public VenuesController(RaceDayDbContext context) : base(context) { }

        /// <summary>Lists all venues. Public.</summary>
        /// <response code="200">Array of venues.</response>
        // GET: api/venues
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Venue>>> GetVenues()
        {
            return await _context.Venues.OrderBy(v => v.Name).ToListAsync();
        }

        /// <summary>Gets one venue. Public.</summary>
        /// <response code="200">The venue.</response>
        /// <response code="404">No such venue.</response>
        // GET: api/venues/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Venue>> GetVenue(int id)
        {
            var venue = await _context.Venues.FindAsync(id);
            if (venue == null) return NotFound();
            return venue;
        }

        /// <summary>Creates a venue. Organiser only.</summary>
        /// <response code="201">The created venue.</response>
        /// <response code="400">Invalid details.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">Participants may not create venues.</response>
        // POST: api/venues
        [HttpPost]
        public async Task<ActionResult<Venue>> PostVenue(Venue venue)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            venue.VenueId = 0;   // the database generates the id
            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetVenue", new { id = venue.VenueId }, venue);
        }

        /// <summary>Updates a venue. Organiser only.</summary>
        /// <response code="200">The updated venue.</response>
        /// <response code="400">Id in the URL does not match the body.</response>
        /// <response code="403">Participants may not edit venues.</response>
        /// <response code="404">No such venue.</response>
        // PUT: api/venues/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutVenue(int id, Venue venue)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (id != venue.VenueId) return BadRequest();

            var existing = await _context.Venues.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Name = venue.Name;
            existing.City = venue.City;
            existing.Province = venue.Province;
            existing.Address = venue.Address;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        /// <summary>Deletes a venue. Organiser only.</summary>
        /// <response code="204">Deleted.</response>
        /// <response code="403">Participants may not delete venues.</response>
        /// <response code="404">No such venue.</response>
        /// <response code="409">Events still use this venue.</response>
        // DELETE: api/venues/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVenue(int id)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            var venue = await _context.Venues.FindAsync(id);
            if (venue == null) return NotFound();

            try
            {
                _context.Venues.Remove(venue);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { message = "Events still use this venue." });
            }
            return NoContent();
        }
    }
}