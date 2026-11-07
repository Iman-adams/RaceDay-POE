using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;
using RaceDay.API.Models;

namespace RaceDay.API.Controllers
{
    [Route("api/results")]
    [ApiController]
    public class ResultsController : RaceDayControllerBase
    {
        public ResultsController(RaceDayDbContext context) : base(context) { }

        // Joins the result to its enrolment, runner, category and event so the response has
        // everything a leaderboard needs (runner name, bib, category, event) in one flat object.
        // It leaves out private fields like the runner's email and emergency contact.
        private IQueryable<ResultView> ResultViews()
        {
            return from r in _context.Results
                   join en in _context.Enrollments on r.EnrollmentId equals en.EnrollmentId
                   join u in _context.Users on en.UserId equals u.UserId
                   join c in _context.EventCategories on en.CategoryId equals c.CategoryId
                   join ev in _context.Events on c.EventId equals ev.EventId
                   select new ResultView
                   {
                       ResultId = r.ResultId,
                       EnrollmentId = r.EnrollmentId,
                       UserId = u.UserId,
                       FullName = u.FullName,
                       BibNumber = en.BibNumber,
                       CategoryId = c.CategoryId,
                       CategoryName = c.CategoryName,
                       EventId = ev.EventId,
                       EventTitle = ev.Title,
                       EventDate = ev.EventDate,
                       FinishTimeMinutes = r.FinishTimeMinutes,
                       OverallRank = r.OverallRank,
                       CategoryRank = r.CategoryRank,
                       Completed = r.Completed
                   };
        }

        /// <summary>Lists all race results. Public.</summary>
        /// <response code="200">Array of results.</response>
        // GET: api/results
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ResultView>>> GetResults()
        {
            return await ResultViews().ToListAsync();
        }

        /// <summary>Gets one race result. Public.</summary>
        /// <response code="200">The result.</response>
        /// <response code="404">No such result.</response>
        // GET: api/results/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ResultView>> GetResult(int id)
        {
            var result = await ResultViews().FirstOrDefaultAsync(r => r.ResultId == id);
            if (result == null) return NotFound();
            return result;
        }

        /// <summary>Leaderboard for one category, best overall rank first. Public.</summary>
        /// <response code="200">Array of results for that category.</response>
        // GET: api/results/category/5
        [HttpGet("category/{categoryId}")]
        public async Task<ActionResult<IEnumerable<ResultView>>> GetResultsByCategory(int categoryId)
        {
            return await ResultViews()
                .Where(r => r.CategoryId == categoryId)
                .OrderBy(r => r.OverallRank == null)  // results without a rank go last
                .ThenBy(r => r.OverallRank)
                .ToListAsync();
        }

        /// <summary>
        /// A participant's personal results history. A Participant can only ask for their OWN id;
        /// an Organiser can ask for any runner.
        /// </summary>
        /// <response code="200">Array of that runner's results, newest event first.</response>
        /// <response code="401">Not logged in.</response>
        /// <response code="403">A participant asked for someone else's results.</response>
        // GET: api/results/user/5
        [HttpGet("user/{userId}")]
        public async Task<ActionResult<IEnumerable<ResultView>>> GetResultsByUser(int userId)
        {
            var denied = RequireLogin();
            if (denied != null) return denied;

            if (!IsOrganiser && CurrentUserId != userId)
            {
                return ForbiddenResult("You can only view your own results.");
            }

            return await ResultViews()
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.EventDate)
                .ToListAsync();
        }

        /// <summary>
        /// Records the official result for an enrolment. Only the Organiser who owns that event.
        /// </summary>
        /// <response code="201">The created result.</response>
        /// <response code="400">The enrolment does not exist.</response>
        /// <response code="403">Participant, or an organiser who does not own the event.</response>
        /// <response code="409">A result already exists for this enrolment.</response>
        // POST: api/results
        [HttpPost]
        public async Task<ActionResult<Result>> PostResult(Result result)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (!await _context.Enrollments.AnyAsync(e => e.EnrollmentId == result.EnrollmentId))
            {
                return BadRequest(new { message = "That enrolment does not exist." });
            }

            if (!await OrganiserOwnsEnrollmentAsync(result.EnrollmentId))
            {
                return ForbiddenResult("You can only record results for events that you created.");
            }

            if (await _context.Results.AnyAsync(r => r.EnrollmentId == result.EnrollmentId))
            {
                return Conflict(new { message = "A result already exists for this enrolment. Update it instead." });
            }

            result.ResultId = 0;  // the database generates the id
            result.Enrollment = null;  // ignore any nested object in the body

            _context.Results.Add(result);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetResult", new { id = result.ResultId }, result);
        }

        /// <summary>Updates a race result. Only the Organiser who owns that event.</summary>
        /// <response code="200">The updated result.</response>
        /// <response code="400">Id mismatch.</response>
        /// <response code="403">Participant, or an organiser who does not own the event.</response>
        /// <response code="404">No such result.</response>
        // PUT: api/results/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutResult(int id, Result result)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            if (id != result.ResultId) return BadRequest();

            var existing = await _context.Results.FindAsync(id);
            if (existing == null) return NotFound();

            if (!await OrganiserOwnsEnrollmentAsync(existing.EnrollmentId))
            {
                return ForbiddenResult("You can only edit results for events that you created.");
            }

            // The enrolment a result belongs to cannot change - only the times and ranks
            existing.FinishTimeMinutes = result.FinishTimeMinutes;
            existing.OverallRank = result.OverallRank;
            existing.Completed = result.Completed;
            existing.CategoryRank = result.CategoryRank;

            await _context.SaveChangesAsync();
            return Ok(existing);
        }

        /// <summary>Deletes a race result. Only the Organiser who owns that event.</summary>
        /// <response code="204">Deleted.</response>
        /// <response code="403">Participant, or an organiser who does not own the event.</response>
        /// <response code="404">No such result.</response>
        // DELETE: api/results/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteResult(int id)
        {
            var denied = RequireRole(OrganiserRole);
            if (denied != null) return denied;

            var result = await _context.Results.FindAsync(id);
            if (result == null) return NotFound();

            if (!await OrganiserOwnsEnrollmentAsync(result.EnrollmentId))
            {
                return ForbiddenResult("You can only delete results for events that you created.");
            }

            _context.Results.Remove(result);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}