using Microsoft.AspNetCore.Mvc;
using RaceDay.MVC.Filters;
using RaceDay.MVC.Models;
using RaceDay.MVC.Services;

namespace RaceDay.MVC.Controllers
{
    // Everything in here is for Participants only.
    // Like the Organiser pages, nothing here touches the database: it all goes through the API, which checks again that the participant may do it.
    [SessionAuthorize(RoleNames.Participant)]
    public class ParticipantController : Controller
    {
        private readonly RaceDayApiClient _api;

        public ParticipantController(RaceDayApiClient api)
        {
            _api = api;
        }

        // The id of the participant who is logged in
        private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

        // My enrolments (the events I have entered, with colour-coded status)

        public async Task<IActionResult> MyEvents()
        {
            var rows = new List<MyEnrolmentRow>();

            var enrolments = await _api.GetAsync<List<EnrollmentModel>>($"api/enrollments/user/{CurrentUserId}");
            var events = await _api.GetAsync<List<EventModel>>("api/events");

            if (!enrolments.Success || !events.Success)
            {
                ViewBag.ErrorMessage = enrolments.ErrorMessage ?? events.ErrorMessage;
                return View(rows);
            }

            // The enrolment only knows its category, so look the event up from the events list
            var eventsById = (events.Data ?? new()).ToDictionary(e => e.EventId);

            rows = (enrolments.Data ?? new())
                .Select(en => new MyEnrolmentRow
                {
                    Enrollment = en,
                    Event = en.Category != null && eventsById.TryGetValue(en.Category.EventId, out var ev) ? ev : null
                })
                .OrderBy(r => r.Event?.EventDate)
                .ToList();

            return View(rows);
        }

        // Enrol in a category
        [HttpGet]
        public async Task<IActionResult> Enrol(int categoryId)
        {
            var model = await BuildEnrolFormAsync(categoryId);
            if (model == null) return RedirectToAction("Index", "Home");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enrol(EnrolFormViewModel form)
        {
            // Reload the event and category details (the form only posts the category and phone number)
            var model = await BuildEnrolFormAsync(form.CategoryId);
            if (model == null) return RedirectToAction("Index", "Home");
            model.EmergencyContact = form.EmergencyContact;

            if (!ModelState.IsValid) return View(model);

            var result = await _api.PostAsync<EnrollmentModel>("api/enrollments", new
            {
                categoryId = form.CategoryId,
                emergencyContact = form.EmergencyContact?.Trim()
            });

            if (!result.Success)
            {
                // e.g. "You are already enrolled in this category." / "This category is full."
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "We could not enrol you. Please try again.");
                return View(model);
            }

            TempData["Success"] = $"You are enrolled in {model.CategoryName} at {model.EventTitle}. Your entry is Pending until the organiser confirms it.";
            return RedirectToAction("MyEvents");
        }

        private async Task<EnrolFormViewModel?> BuildEnrolFormAsync(int categoryId)
        {
            var category = await _api.GetAsync<CategoryModel>($"api/categories/{categoryId}");
            if (!category.Success || category.Data == null)
            {
                TempData["Error"] = category.ErrorMessage ?? "We could not find that category.";
                return null;
            }

            var ev = await _api.GetAsync<EventModel>($"api/events/{category.Data.EventId}");
            if (!ev.Success || ev.Data == null)
            {
                TempData["Error"] = ev.ErrorMessage ?? "We could not find that event.";
                return null;
            }

            if (ev.Data.Status != "Upcoming" || ev.Data.EventDate <= DateTime.Now)
            {
                TempData["Error"] = "Entries for this event are closed.";
                return null;
            }

            return new EnrolFormViewModel
            {
                CategoryId = categoryId,
                EventId = ev.Data.EventId,
                EventTitle = ev.Data.Title,
                EventDate = ev.Data.EventDate,
                CategoryName = category.Data.CategoryName,
                DistanceKm = category.Data.DistanceKm,
                EntryFee = category.Data.EntryFee,
                StartDateTime = category.Data.StartDateTime
            };
        }

        //  Cancel an enrolment
        [HttpGet]
        public async Task<IActionResult> CancelEnrolment(int id)
        {
            var enrolment = await _api.GetAsync<EnrollmentModel>($"api/enrollments/{id}");
            if (!enrolment.Success || enrolment.Data == null)
            {
                TempData["Error"] = enrolment.ErrorMessage ?? "We could not find that enrolment.";
                return RedirectToAction("MyEvents");
            }

            return View(enrolment.Data);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelEnrolmentConfirmed(int id)
        {
            var result = await _api.DeleteAsync($"api/enrollments/{id}");
            if (result.Success)
                TempData["Success"] = "Your enrolment has been cancelled.";
            else
                TempData["Error"] = result.ErrorMessage;   // e.g. a result has already been recorded

            return RedirectToAction("MyEvents");
        }

        //  My results
        public async Task<IActionResult> MyResults()
        {
            var rows = new List<MyResultRow>();

            var results = await _api.GetAsync<List<ResultModel>>($"api/results/user/{CurrentUserId}");
            if (!results.Success)
            {
                ViewBag.ErrorMessage = results.ErrorMessage;
                return View(rows);
            }

            // For "47th of 312" we need to know how many runners finished in each category I raced in
            var finishers = new Dictionary<int, int>();
            foreach (var categoryId in (results.Data ?? new()).Select(r => r.CategoryId).Distinct())
            {
                var inCategory = await _api.GetAsync<List<ResultModel>>($"api/results/category/{categoryId}");
                finishers[categoryId] = inCategory.Success ? (inCategory.Data ?? new()).Count(r => r.Completed != 0) : 0;
            }

            rows = (results.Data ?? new())
                .OrderByDescending(r => r.EventDate)
                .Select(r => new MyResultRow { Result = r, FinishersInCategory = finishers.GetValueOrDefault(r.CategoryId) })
                .ToList();

            return View(rows);
        }

        //  Profile
        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _api.GetAsync<UserProfileModel>($"api/users/{CurrentUserId}");
            if (!user.Success || user.Data == null)
            {
                TempData["Error"] = user.ErrorMessage ?? "We could not load your profile.";
                return RedirectToAction("MyEvents");
            }

            return View(new ProfileViewModel
            {
                Role = user.Data.Role,
                FullName = user.Data.FullName,
                Email = user.Data.Email,
                Gender = user.Data.Gender,
                DateOfBirth = user.Data.DateOfBirth
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            model.Role = HttpContext.Session.GetString(SessionKeys.UserRole);

            if (model.DateOfBirth.HasValue && model.DateOfBirth.Value.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(model.DateOfBirth), "Your date of birth cannot be in the future.");
            }

            if (!ModelState.IsValid) return View(model);

            var result = await _api.PutAsync<UserProfileModel>($"api/users/{CurrentUserId}", new
            {
                fullName = model.FullName.Trim(),
                email = model.Email.Trim(),
                gender = string.IsNullOrWhiteSpace(model.Gender) ? null : model.Gender,
                dateOfBirth = model.DateOfBirth,
                passwordHash = string.IsNullOrWhiteSpace(model.NewPassword) ? null : model.NewPassword   // the API hashes it
            });

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "We could not save your profile. Please try again.");
                return View(model);
            }

            // Keep the name shown in the menu up to date
            HttpContext.Session.SetString(SessionKeys.UserName, model.FullName.Trim());

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction("Profile");
        }
    }
}
