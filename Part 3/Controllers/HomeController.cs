using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Filters;
using RaceDay.MVC.Models;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers;

public class HomeController : Controller
{
    private readonly RaceDayApiClient _api;

    // ASP.NET Core gives us the API client automatically (it was registered in Program.cs)
    public HomeController(RaceDayApiClient api)
    {
        _api = api;
    }

    // The events page: only for logged-in users. It asks the API for all events, then narrows them down using the filters chosen on the page.
    [SessionAuthorize]
    public async Task<IActionResult> Index(EventFilter filter)
    {
        // First visit (no filters in the address yet): show the upcoming events
        if (!Request.Query.ContainsKey(nameof(EventFilter.Status)))
        {
            filter.Status = "Upcoming";
        }

        var model = new EventListViewModel { Filter = filter };

        var eventsResult = await _api.GetAsync<List<EventModel>>("api/events");
        if (!eventsResult.Success)
        {
            // Show a friendly message on the page instead of crashing
            ViewBag.ErrorMessage = eventsResult.ErrorMessage;
            return View(model);
        }

        var all = eventsResult.Data ?? new List<EventModel>();

        // Cities for the location drop-down
        model.Locations = all.Where(e => e.Venue != null).Select(e => e.Venue!.City).Distinct().OrderBy(c => c).ToList();

        // Categories are only used to show each event's distances on its card
        var categoriesResult = await _api.GetAsync<List<CategoryModel>>("api/categories");
        if (categoriesResult.Success)
        {
            model.CategoriesByEvent = (categoriesResult.Data ?? new())
                .GroupBy(c => c.EventId)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.DistanceKm).ToList());
        }

        // Apply the filters one by one
        IEnumerable<EventModel> events = all;

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var text = filter.Search.Trim();
            events = events.Where(e =>
                e.Title.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                (e.Venue != null && e.Venue.Name.Contains(text, StringComparison.OrdinalIgnoreCase)));
        }
        if (!string.IsNullOrWhiteSpace(filter.EventType))
            events = events.Where(e => e.EventType == filter.EventType);
        if (!string.IsNullOrWhiteSpace(filter.Location))
            events = events.Where(e => e.Venue != null && e.Venue.City == filter.Location);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            events = events.Where(e => e.Status == filter.Status);
        if (filter.DateFrom.HasValue)
            events = events.Where(e => e.EventDate.Date >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
            events = events.Where(e => e.EventDate.Date <= filter.DateTo.Value.Date);

        model.Events = events.OrderBy(e => e.EventDate).ToList();
        return View(model);
    }

    // One event with its race categories (and an Enrol button for participants)
    [SessionAuthorize]
    public async Task<IActionResult> Details(int id)
    {
        var eventResult = await _api.GetAsync<EventModel>($"api/events/{id}");
        if (!eventResult.Success || eventResult.Data == null)
        {
            TempData["Error"] = eventResult.ErrorMessage ?? "We could not find that event.";
            return RedirectToAction("Index");
        }

        var model = new EventDetailsViewModel
        {
            Event = eventResult.Data,
            IsParticipant = HttpContext.Session.GetString(SessionKeys.UserRole) == RoleNames.Participant
        };

        var categories = await _api.GetAsync<List<CategoryModel>>($"api/events/{id}/categories");
        if (categories.Success)
            model.Categories = (categories.Data ?? new()).OrderBy(c => c.DistanceKm).ToList();
        else
            ViewBag.ErrorMessage = categories.ErrorMessage;

        // For participants: which of these categories have they already entered?
        if (model.IsParticipant)
        {
            var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
            var mine = await _api.GetAsync<List<EnrollmentModel>>($"api/enrollments/user/{userId}");
            if (mine.Success)
                model.EnrolledCategoryIds = (mine.Data ?? new()).Select(e => e.CategoryId).ToHashSet();
        }

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
