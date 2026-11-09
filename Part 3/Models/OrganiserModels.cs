using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    // Data coming BACK from the API (these only carry data to the views)

    public class CategoryModel
    {
        public int CategoryId { get; set; }
        public int EventId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public decimal DistanceKm { get; set; }
        public decimal EntryFee { get; set; }
        public int MaxParticipants { get; set; }
        public DateTime StartDateTime { get; set; }
        public int? MinAge { get; set; }
        public string? RouteDescription { get; set; }
    }

    public class EnrollmentModel
    {
        public int EnrollmentId { get; set; }
        public int UserId { get; set; }
        public int CategoryId { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string? BibNumber { get; set; }
        public string? EmergencyContact { get; set; }

        // The API includes the runner and the category with each enrolment
        public UserSummaryModel? User { get; set; }
        public CategoryModel? Category { get; set; }
    }

    // One finished (or did-not-finish) race result, already joined with names by the API
    public class ResultModel
    {
        public int ResultId { get; set; }
        public int EnrollmentId { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? BibNumber { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public decimal? FinishTimeMinutes { get; set; }
        public int? OverallRank { get; set; }
        public int? CategoryRank { get; set; }
        public int? Completed { get; set; }
    }

    // Models the pages are built from

    // One row of the dashboard's "enrolments per event" table
    public class DashboardEventRow
    {
        public EventModel Event { get; set; } = new();
        public int Categories { get; set; }
        public int Enrolments { get; set; }
        public int Pending { get; set; }
    }

    public class DashboardViewModel
    {
        public List<DashboardEventRow> Rows { get; set; } = new();
        public List<EventModel> UpcomingEvents { get; set; } = new();
        public int TotalEvents { get; set; }
        public int UpcomingCount { get; set; }
        public int TotalEnrolments { get; set; }
        public int PendingEnrolments { get; set; }
    }

    // One row of the organiser's "My Events" table
    public class EventListRow
    {
        public EventModel Event { get; set; } = new();
        public List<CategoryModel> Categories { get; set; } = new();
    }

    public class CategoriesPageModel
    {
        public EventModel Event { get; set; } = new();
        public List<CategoryModel> Categories { get; set; } = new();
    }

    public class EnrolmentsPageModel
    {
        public EventModel Event { get; set; } = new();
        public List<EnrollmentModel> Enrollments { get; set; } = new();
    }

    // One line of the results page: an enrolment, plus its result if one has been entered
    public class ResultRow
    {
        public EnrollmentModel Enrollment { get; set; } = new();
        public ResultModel? Result { get; set; }
    }

    public class ResultsPageModel
    {
        public EventModel Event { get; set; } = new();
        public List<ResultRow> Rows { get; set; } = new();
    }

    // Used by the "Are you sure?" page that appears before anything is deleted
    public class ConfirmDeleteViewModel
    {
        public string Heading { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int Id { get; set; }
        public string PostAction { get; set; } = string.Empty;  // the action that does the delete
        public string CancelAction { get; set; } = string.Empty;  // where "Cancel" goes
        public int? CancelId { get; set; }
    }

    // Form models (what the organiser types in).
    // The rules are checked in the browser and again on the server.

    public class EventFormViewModel
    {
        public int EventId { get; set; }  // 0 when creating a new event

        [Required(ErrorMessage = "Please enter the event name.")]
        [StringLength(100, ErrorMessage = "The event name can be at most 100 characters.")]
        [Display(Name = "Event name")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a description.")]
        [StringLength(500, ErrorMessage = "The description can be at most 500 characters.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose the date and start time.")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date and start time")]
        public DateTime? EventDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please choose a location.")]
        [Display(Name = "Location (venue)")]
        public int VenueId { get; set; }

        [Required(ErrorMessage = "Please choose the event type.")]
        [RegularExpression("^(Running|Cycling|Walking)$", ErrorMessage = "Please choose Running, Cycling or Walking.")]
        [Display(Name = "Event type")]
        public string EventType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose the status.")]
        [RegularExpression("^(Upcoming|Completed|Cancelled)$", ErrorMessage = "Please choose Upcoming, Completed or Cancelled.")]
        public string Status { get; set; } = "Upcoming";

        // Fills the location drop-down. Not typed in by the user.
        public List<VenueModel> Venues { get; set; } = new();
    }

    public class VenueFormViewModel
    {
        [Required(ErrorMessage = "Please enter the venue name.")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the city.")]
        [StringLength(50)]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the province.")]
        [StringLength(50)]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the street address.")]
        [StringLength(150)]
        public string Address { get; set; } = string.Empty;
    }

    public class CategoryFormViewModel
    {
        public int CategoryId { get; set; }  // 0 when creating a new category
        public int EventId { get; set; }

        [Required(ErrorMessage = "Please enter the category name, for example 10km Road Run.")]
        [StringLength(50, ErrorMessage = "The category name can be at most 50 characters.")]
        [Display(Name = "Category name")]
        public string CategoryName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter the distance.")]
        [Range(0.1, 999.99, ErrorMessage = "The distance must be between 0.1 and 999.99 km.")]
        [Display(Name = "Distance (km)")]
        public decimal? DistanceKm { get; set; }

        [Required(ErrorMessage = "Please enter the entry fee (0 if it is free).")]
        [Range(0, 1000000, ErrorMessage = "The entry fee must be 0 or more.")]
        [Display(Name = "Entry fee (R)")]
        public decimal? EntryFee { get; set; }

        [Required(ErrorMessage = "Please enter the maximum number of participants.")]
        [Range(1, 1000000, ErrorMessage = "There must be room for at least 1 participant.")]
        [Display(Name = "Maximum participants")]
        public int? MaxParticipants { get; set; }

        [Required(ErrorMessage = "Please choose the start date and time.")]
        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        [Display(Name = "Start date and time")]
        public DateTime? StartDateTime { get; set; }

        [Range(0, 120, ErrorMessage = "The minimum age must be between 0 and 120.")]
        [Display(Name = "Minimum age (optional)")]
        public int? MinAge { get; set; }

        [Display(Name = "Route description (optional)")]
        [StringLength(1000, ErrorMessage = "The route description can be at most 1000 characters.")]
        public string? RouteDescription { get; set; }
    }

    public class ResultFormViewModel
    {
        public int ResultId { get; set; }  // 0 when no result exists yet
        public int EnrollmentId { get; set; }
        public int EventId { get; set; }

        // Shown at the top of the form so the organiser knows whose result this is
        // (nullable on purpose: ASP.NET treats a non-nullable string as "required", and these are not typed in)
        public string? RunnerName { get; set; }
        public string? BibNumber { get; set; }
        public string? CategoryName { get; set; }

        [Display(Name = "Finished the race")]
        public bool Completed { get; set; } = true;

        [Display(Name = "Finish time (h:mm:ss)")]
        [StringLength(10)]
        public string? FinishTime { get; set; }

        [Range(1, 1000000, ErrorMessage = "The position must be 1 or higher.")]
        [Display(Name = "Overall position")]
        public int? OverallRank { get; set; }

        [Range(1, 1000000, ErrorMessage = "The position must be 1 or higher.")]
        [Display(Name = "Position in category")]
        public int? CategoryRank { get; set; }
    }
}