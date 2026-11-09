namespace RaceDay.MVC.Models
{
    // These classes describe the JSON the API sends back. They only CARRY data to the views.
    // There is no database code and no business rules here - all of that stays in the API.

    public class EventModel
    {
        public int EventId { get; set; }
        public int OrganiserId { get; set; }
        public int VenueId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;

        // The API includes these two with every event
        public UserSummaryModel? Organiser { get; set; }
        public VenueModel? Venue { get; set; }
    }

    public class VenueModel
    {
        public int VenueId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
    }

    // Just the parts of a user that other pages need to show (never a password)
    public class UserSummaryModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}