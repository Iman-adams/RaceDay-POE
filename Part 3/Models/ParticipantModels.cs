using System.ComponentModel.DataAnnotations;

namespace RaceDay.MVC.Models
{
    // ======================================================================
    //  Data coming back from the API
    // ======================================================================

    // A user's own profile (the API never sends the password)
    public class UserProfileModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
    }

    // ======================================================================
    //  Models the pages are built from
    // ======================================================================

    // The filters on the events page. They travel in the address (?Search=...&EventType=...)
    public class EventFilter
    {
        public string? Search { get; set; }
        public string? EventType { get; set; }
        public string? Location { get; set; }
        public string? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    public class EventListViewModel
    {
        public EventFilter Filter { get; set; } = new();
        public List<EventModel> Events { get; set; } = new();

        // Cities for the location drop-down
        public List<string> Locations { get; set; } = new();

        // The categories of each event (to show distances on the cards)
        public Dictionary<int, List<CategoryModel>> CategoriesByEvent { get; set; } = new();
    }

    public class EventDetailsViewModel
    {
        public EventModel Event { get; set; } = new();
        public List<CategoryModel> Categories { get; set; } = new();

        // Categories this participant has already entered
        public HashSet<int> EnrolledCategoryIds { get; set; } = new();

        public bool IsParticipant { get; set; }

        // Enrolling is only offered for upcoming events that have not happened yet
        public bool CanEnrol => IsParticipant && Event.Status == "Upcoming" && Event.EventDate > DateTime.Now;
    }

    // One line of the "My enrolments" page
    public class MyEnrolmentRow
    {
        public EnrollmentModel Enrollment { get; set; } = new();
        public EventModel? Event { get; set; }
        public bool CanCancel => Event != null && Event.Status == "Upcoming";
    }

    // One line of the "My results" page
    public class MyResultRow
    {
        public ResultModel Result { get; set; } = new();

        // How many runners finished in the same category (for "47th of 312")
        public int FinishersInCategory { get; set; }
    }

    // ======================================================================
    //  Forms
    // ======================================================================

    public class EnrolFormViewModel
    {
        public int CategoryId { get; set; }

        // Shown so the participant can check what they are entering (not typed in)
        public int EventId { get; set; }
        public string? EventTitle { get; set; }
        public DateTime EventDate { get; set; }
        public string? CategoryName { get; set; }
        public decimal DistanceKm { get; set; }
        public decimal EntryFee { get; set; }
        public DateTime StartDateTime { get; set; }

        [Required(ErrorMessage = "Please enter an emergency contact number.")]
        [RegularExpression(@"^[0-9 +()\-]{7,20}$", ErrorMessage = "Please enter a valid phone number, for example 082 123 4567.")]
        [Display(Name = "Emergency contact number")]
        public string? EmergencyContact { get; set; }
    }

    public class ProfileViewModel
    {
        public string? Role { get; set; }

        [Required(ErrorMessage = "Please enter your full name.")]
        [StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;

        public string? Gender { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Date of birth")]
        public DateTime? DateOfBirth { get; set; }

        // Leave empty to keep the current password
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Your new password must be at least 8 characters long.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password (optional)")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
        [Display(Name = "Confirm new password")]
        public string? ConfirmPassword { get; set; }
    }
}