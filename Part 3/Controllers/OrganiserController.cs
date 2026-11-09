using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Filters;
using RaceDay.MVC.Models;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers
{
    // Everything in here is for Organisers only.
    // The pages never touch the database: every read and write goes through the API (via RaceDayApiClient),
    // and the API checks again that the organiser owns whatever they are changing.
    [SessionAuthorize(RoleNames.Organiser)]
    public class OrganiserController : Controller
    {
        private readonly RaceDayApiClient _api;

        public OrganiserController(RaceDayApiClient api)
        {
            _api = api;
        }

        // The id of the organiser who is logged in
        private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

        // Helpers
        // Loads an event and checks it belongs to the logged-in organiser.
        // Returns null (and sets an error message) when it cannot be used.
        private async Task<EventModel?> LoadOwnEventAsync(int eventId)
        {
            var result = await _api.GetAsync<EventModel>($"api/events/{eventId}");

            if (!result.Success || result.Data == null)
            {
                TempData["Error"] = result.ErrorMessage ?? "We could not find that event.";
                return null;
            }

            if (result.Data.OrganiserId != CurrentUserId)
            {
                TempData["Error"] = "You can only manage events that you created.";
                return null;
            }

            return result.Data;
        }

        // Fills the pages that show an event's tab bar (Categories / Enrolments / Results)
        private void SetEventHeader(EventModel ev)
        {
            ViewBag.EventId = ev.EventId;
            ViewBag.EventTitle = ev.Title;
        }

        // Turns an API failure into an error shown at the top of a form
        private void AddApiError(string? message)
        {
            ModelState.AddModelError(string.Empty, message ?? "Something went wrong. Please try again.");
        }

        // Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var eventsResult = await _api.GetAsync<List<EventModel>>("api/events");
            var enrolmentsResult = await _api.GetAsync<List<EnrollmentModel>>("api/enrollments");
            var categoriesResult = await _api.GetAsync<List<CategoryModel>>("api/categories");

            var model = new DashboardViewModel();

            if (!eventsResult.Success || !enrolmentsResult.Success || !categoriesResult.Success)
            {
                ViewBag.ErrorMessage = eventsResult.ErrorMessage ?? enrolmentsResult.ErrorMessage ?? categoriesResult.ErrorMessage;
                return View(model);
            }

            // Only this organiser's events (the API returns everyone's)
            var myEvents = (eventsResult.Data ?? new()).Where(e => e.OrganiserId == CurrentUserId).OrderBy(e => e.EventDate).ToList();
            var enrolments = enrolmentsResult.Data ?? new();  // the API only returns enrolments for MY events
            var categories = categoriesResult.Data ?? new();

            foreach (var ev in myEvents)
            {
                var forEvent = enrolments.Where(en => en.Category?.EventId == ev.EventId).ToList();
                model.Rows.Add(new DashboardEventRow
                {
                    Event = ev,
                    Categories = categories.Count(c => c.EventId == ev.EventId),
                    Enrolments = forEvent.Count,
                    Pending = forEvent.Count(en => en.PaymentStatus == "Pending")
                });
            }

            model.TotalEvents = myEvents.Count;
            model.TotalEnrolments = enrolments.Count;
            model.PendingEnrolments = enrolments.Count(en => en.PaymentStatus == "Pending");

            // Upcoming = still to happen and not cancelled
            var upcoming = myEvents.Where(e => e.EventDate >= DateTime.Now && e.Status != "Cancelled").ToList();
            model.UpcomingCount = upcoming.Count;
            model.UpcomingEvents = upcoming.Take(5).ToList();

            return View(model);
        }

        // Events (create, read, update, delete)
        public async Task<IActionResult> Events()
        {
            var eventsResult = await _api.GetAsync<List<EventModel>>("api/events");
            var categoriesResult = await _api.GetAsync<List<CategoryModel>>("api/categories");

            var rows = new List<EventListRow>();

            if (!eventsResult.Success || !categoriesResult.Success)
            {
                ViewBag.ErrorMessage = eventsResult.ErrorMessage ?? categoriesResult.ErrorMessage;
                return View(rows);
            }

            var categories = categoriesResult.Data ?? new();
            foreach (var ev in (eventsResult.Data ?? new()).Where(e => e.OrganiserId == CurrentUserId).OrderBy(e => e.EventDate))
            {
                rows.Add(new EventListRow
                {
                    Event = ev,
                    Categories = categories.Where(c => c.EventId == ev.EventId).OrderBy(c => c.DistanceKm).ToList()
                });
            }

            return View(rows);
        }

        // Loads the venues for the location drop-down. Returns false if the API could not be reached.
        private async Task<bool> FillVenuesAsync(EventFormViewModel model)
        {
            var venues = await _api.GetAsync<List<VenueModel>>("api/venues");
            if (!venues.Success)
            {
                AddApiError(venues.ErrorMessage);
                return false;
            }
            model.Venues = venues.Data ?? new();
            return true;
        }

        [HttpGet]
        public async Task<IActionResult> CreateEvent()
        {
            var model = new EventFormViewModel { EventDate = DateTime.Today.AddDays(30).AddHours(7) };
            await FillVenuesAsync(model);
            return View("EventForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(EventFormViewModel model)
        {
            // The venue list is needed again whenever the form is shown, so load it first
            await FillVenuesAsync(model);
            if (!ModelState.IsValid) return View("EventForm", model);

            var body = new
            {
                venueId = model.VenueId,
                title = model.Title.Trim(),
                description = model.Description.Trim(),
                eventDate = model.EventDate,
                status = model.Status,
                eventType = model.EventType
            };

            var result = await _api.PostAsync<EventModel>("api/events", body);
            if (!result.Success)
            {
                AddApiError(result.ErrorMessage);
                return View("EventForm", model);
            }

            TempData["Success"] = $"\"{model.Title}\" has been created. Now add its race categories.";
            return RedirectToAction("Categories", new { eventId = result.Data?.EventId });
        }

        [HttpGet]
        public async Task<IActionResult> EditEvent(int id)
        {
            var ev = await LoadOwnEventAsync(id);
            if (ev == null) return RedirectToAction("Events");

            var model = new EventFormViewModel
            {
                EventId = ev.EventId,
                Title = ev.Title,
                Description = ev.Description,
                EventDate = ev.EventDate,
                VenueId = ev.VenueId,
                EventType = ev.EventType,
                Status = ev.Status
            };
            await FillVenuesAsync(model);
            return View("EventForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEvent(EventFormViewModel model)
        {
            await FillVenuesAsync(model);
            if (!ModelState.IsValid) return View("EventForm", model);

            var body = new
            {
                eventId = model.EventId,
                venueId = model.VenueId,
                title = model.Title.Trim(),
                description = model.Description.Trim(),
                eventDate = model.EventDate,
                status = model.Status,
                eventType = model.EventType
            };

            var result = await _api.PutAsync<EventModel>($"api/events/{model.EventId}", body);
            if (!result.Success)
            {
                AddApiError(result.ErrorMessage);
                return View("EventForm", model);
            }

            TempData["Success"] = $"\"{model.Title}\" has been updated.";
            return RedirectToAction("Events");
        }

        [HttpGet]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var ev = await LoadOwnEventAsync(id);
            if (ev == null) return RedirectToAction("Events");

            return View("ConfirmDelete", new ConfirmDeleteViewModel
            {
                Heading = "Delete event",
                Message = $"Are you sure you want to delete \"{ev.Title}\"? This cannot be undone.",
                Id = id,
                PostAction = "DeleteEventConfirmed",
                CancelAction = "Events"
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteEventConfirmed(int id)
        {
            var result = await _api.DeleteAsync($"api/events/{id}");
            if (result.Success)
                TempData["Success"] = "The event has been deleted.";
            else
                TempData["Error"] = result.ErrorMessage;   // e.g. "This event still has categories. Delete them first."

            return RedirectToAction("Events");
        }

        // Venues (so an organiser can add a new location)

        [HttpGet]
        public IActionResult CreateVenue() => View(new VenueFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateVenue(VenueFormViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _api.PostAsync<VenueModel>("api/venues", new
            {
                name = model.Name.Trim(),
                city = model.City.Trim(),
                province = model.Province.Trim(),
                address = model.Address.Trim()
            });

            if (!result.Success)
            {
                AddApiError(result.ErrorMessage);
                return View(model);
            }

            TempData["Success"] = $"The venue \"{model.Name}\" has been added. You can now choose it as a location.";
            return RedirectToAction("CreateEvent");
        }

        // Categories (the races inside an event, e.g. 10km and 21km)

        public async Task<IActionResult> Categories(int eventId)
        {
            var ev = await LoadOwnEventAsync(eventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            var model = new CategoriesPageModel { Event = ev };

            var result = await _api.GetAsync<List<CategoryModel>>($"api/events/{eventId}/categories");
            if (!result.Success)
                ViewBag.ErrorMessage = result.ErrorMessage;
            else
                model.Categories = (result.Data ?? new()).OrderBy(c => c.DistanceKm).ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CreateCategory(int eventId)
        {
            var ev = await LoadOwnEventAsync(eventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            // Start time defaults to the event's own date and time
            return View("CategoryForm", new CategoryFormViewModel { EventId = eventId, StartDateTime = ev.EventDate });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateCategory(CategoryFormViewModel model)
        {
            var ev = await LoadOwnEventAsync(model.EventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            if (!ModelState.IsValid) return View("CategoryForm", model);

            var result = await _api.PostAsync<CategoryModel>($"api/events/{model.EventId}/categories", ToCategoryBody(model));
            if (!result.Success)
            {
                AddApiError(result.ErrorMessage);
                return View("CategoryForm", model);
            }

            TempData["Success"] = $"The category \"{model.CategoryName}\" has been added.";
            return RedirectToAction("Categories", new { eventId = model.EventId });
        }

        [HttpGet]
        public async Task<IActionResult> EditCategory(int id)
        {
            var category = await _api.GetAsync<CategoryModel>($"api/categories/{id}");
            if (!category.Success || category.Data == null)
            {
                TempData["Error"] = category.ErrorMessage ?? "We could not find that category.";
                return RedirectToAction("Events");
            }

            var ev = await LoadOwnEventAsync(category.Data.EventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            var c = category.Data;
            return View("CategoryForm", new CategoryFormViewModel
            {
                CategoryId = c.CategoryId,
                EventId = c.EventId,
                CategoryName = c.CategoryName,
                DistanceKm = c.DistanceKm,
                EntryFee = c.EntryFee,
                MaxParticipants = c.MaxParticipants,
                StartDateTime = c.StartDateTime,
                MinAge = c.MinAge,
                RouteDescription = c.RouteDescription
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditCategory(CategoryFormViewModel model)
        {
            var ev = await LoadOwnEventAsync(model.EventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            if (!ModelState.IsValid) return View("CategoryForm", model);

            var result = await _api.PutAsync<CategoryModel>($"api/categories/{model.CategoryId}", ToCategoryBody(model));
            if (!result.Success)
            {
                AddApiError(result.ErrorMessage);
                return View("CategoryForm", model);
            }

            TempData["Success"] = $"The category \"{model.CategoryName}\" has been updated.";
            return RedirectToAction("Categories", new { eventId = model.EventId });
        }

        [HttpGet]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _api.GetAsync<CategoryModel>($"api/categories/{id}");
            if (!category.Success || category.Data == null)
            {
                TempData["Error"] = category.ErrorMessage ?? "We could not find that category.";
                return RedirectToAction("Events");
            }

            var ev = await LoadOwnEventAsync(category.Data.EventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            return View("ConfirmDelete", new ConfirmDeleteViewModel
            {
                Heading = "Delete category",
                Message = $"Are you sure you want to delete the category \"{category.Data.CategoryName}\"? This cannot be undone.",
                Id = id,
                PostAction = "DeleteCategoryConfirmed",
                CancelAction = "Categories",
                CancelId = category.Data.EventId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCategoryConfirmed(int id, int eventId)
        {
            var result = await _api.DeleteAsync($"api/categories/{id}");
            if (result.Success)
                TempData["Success"] = "The category has been deleted.";
            else
                TempData["Error"] = result.ErrorMessage;  // e.g. "People are already enrolled in this category."

            return RedirectToAction("Categories", new { eventId });
        }

        // Builds the JSON the API expects for a category
        private static object ToCategoryBody(CategoryFormViewModel m) => new
        {
            categoryId = m.CategoryId,
            categoryName = m.CategoryName.Trim(),
            distanceKm = m.DistanceKm,
            entryFee = m.EntryFee,
            maxParticipants = m.MaxParticipants,
            startDateTime = m.StartDateTime,
            minAge = m.MinAge,
            routeDescription = string.IsNullOrWhiteSpace(m.RouteDescription) ? null : m.RouteDescription.Trim()
        };

        //  Enrolments for one event (who has entered which category, and their status)
        public async Task<IActionResult> Enrolments(int eventId)
        {
            var ev = await LoadOwnEventAsync(eventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            var model = new EnrolmentsPageModel { Event = ev };

            var result = await _api.GetAsync<List<EnrollmentModel>>("api/enrollments");
            if (!result.Success)
            {
                ViewBag.ErrorMessage = result.ErrorMessage;
                return View(model);
            }

            model.Enrollments = (result.Data ?? new())
                .Where(e => e.Category?.EventId == eventId)
                .OrderBy(e => e.Category?.CategoryName)
                .ThenBy(e => e.User?.FullName)
                .ToList();

            return View(model);
        }

        // Saves a new status (and bib number) for one enrolment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEnrolment(int enrollmentId, int eventId, string paymentStatus, string? bibNumber)
        {
            if (!UiHelper.EnrolmentStatuses.Contains(paymentStatus))
            {
                TempData["Error"] = "Please choose a valid status.";
                return RedirectToAction("Enrolments", new { eventId });
            }

            bibNumber = string.IsNullOrWhiteSpace(bibNumber) ? null : bibNumber.Trim();
            if (bibNumber != null && bibNumber.Length > 20)
            {
                TempData["Error"] = "A bib number can be at most 20 characters.";
                return RedirectToAction("Enrolments", new { eventId });
            }

            // The API replaces the whole enrolment on update, so read it first and keep the other details
            var current = await _api.GetAsync<EnrollmentModel>($"api/enrollments/{enrollmentId}");
            if (!current.Success || current.Data == null)
            {
                TempData["Error"] = current.ErrorMessage ?? "We could not find that enrolment.";
                return RedirectToAction("Enrolments", new { eventId });
            }

            var result = await _api.PutAsync<EnrollmentModel>($"api/enrollments/{enrollmentId}", new
            {
                enrollmentId,
                userId = current.Data.UserId,
                categoryId = current.Data.CategoryId,
                paymentStatus,
                bibNumber,
                emergencyContact = current.Data.EmergencyContact
            });

            if (result.Success)
                TempData["Success"] = "The enrolment has been updated.";
            else
                TempData["Error"] = result.ErrorMessage;

            return RedirectToAction("Enrolments", new { eventId });
        }

        // Results (enter finish times and positions)

        public async Task<IActionResult> Results(int eventId)
        {
            var ev = await LoadOwnEventAsync(eventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            var model = new ResultsPageModel { Event = ev };

            var enrolments = await _api.GetAsync<List<EnrollmentModel>>("api/enrollments");
            var results = await _api.GetAsync<List<ResultModel>>("api/results");
            if (!enrolments.Success || !results.Success)
            {
                ViewBag.ErrorMessage = enrolments.ErrorMessage ?? results.ErrorMessage;
                return View(model);
            }

            var resultsByEnrolment = (results.Data ?? new()).ToDictionary(r => r.EnrollmentId);

            model.Rows = (enrolments.Data ?? new())
                .Where(e => e.Category?.EventId == eventId)
                .OrderBy(e => e.Category?.CategoryName)
                .ThenBy(e => e.User?.FullName)
                .Select(e => new ResultRow
                {
                    Enrollment = e,
                    Result = resultsByEnrolment.TryGetValue(e.EnrollmentId, out var r) ? r : null
                })
                .ToList();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EnterResult(int enrollmentId)
        {
            var enrolment = await _api.GetAsync<EnrollmentModel>($"api/enrollments/{enrollmentId}");
            if (!enrolment.Success || enrolment.Data?.Category == null)
            {
                TempData["Error"] = enrolment.ErrorMessage ?? "We could not find that enrolment.";
                return RedirectToAction("Events");
            }

            var eventId = enrolment.Data.Category.EventId;
            var ev = await LoadOwnEventAsync(eventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            var model = new ResultFormViewModel
            {
                EnrollmentId = enrollmentId,
                EventId = eventId,
                RunnerName = enrolment.Data.User?.FullName ?? "Runner",
                BibNumber = enrolment.Data.BibNumber,
                CategoryName = enrolment.Data.Category.CategoryName
            };

            // If a result already exists, fill the form so the organiser can correct it
            var results = await _api.GetAsync<List<ResultModel>>("api/results");
            var existing = results.Data?.FirstOrDefault(r => r.EnrollmentId == enrollmentId);
            if (existing != null)
            {
                model.ResultId = existing.ResultId;
                model.Completed = existing.Completed != 0;
                model.FinishTime = existing.FinishTimeMinutes == null ? null : UiHelper.FormatFinishTime(existing.FinishTimeMinutes);
                model.OverallRank = existing.OverallRank;
                model.CategoryRank = existing.CategoryRank;
            }

            return View("ResultForm", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnterResult(ResultFormViewModel model)
        {
            var ev = await LoadOwnEventAsync(model.EventId);
            if (ev == null) return RedirectToAction("Events");
            SetEventHeader(ev);

            // Check the finish time: needed when the runner finished, and must look like h:mm:ss
            decimal minutes = 0;
            var hasTime = !string.IsNullOrWhiteSpace(model.FinishTime);
            if (hasTime && !UiHelper.TryParseFinishTime(model.FinishTime, out minutes))
            {
                ModelState.AddModelError(nameof(model.FinishTime), "Please enter the time like 1:45:30 (hours:minutes:seconds).");
            }
            else if (model.Completed && !hasTime)
            {
                ModelState.AddModelError(nameof(model.FinishTime), "Please enter the finish time, or untick \"Finished the race\".");
            }

            if (!ModelState.IsValid) return View("ResultForm", model);

            var body = new
            {
                resultId = model.ResultId,
                enrollmentId = model.EnrollmentId,
                finishTimeMinutes = hasTime ? (decimal?)minutes : null,
                overallRank = model.Completed ? model.OverallRank : null,
                categoryRank = model.Completed ? model.CategoryRank : null,
                completed = model.Completed ? 1 : 0
            };

            var result = model.ResultId == 0
                ? await _api.PostAsync<object>("api/results", body)
                : await _api.PutAsync<object>($"api/results/{model.ResultId}", body);

            if (!result.Success)
            {
                AddApiError(result.ErrorMessage);
                return View("ResultForm", model);
            }

            TempData["Success"] = $"The result for {model.RunnerName} has been saved.";
            return RedirectToAction("Results", new { eventId = model.EventId });
        }
    }
}