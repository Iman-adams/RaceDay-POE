using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace RaceDay.API.Models
{
    // These classes are the C# version of the tables in the SQL script.
    // [Column("...")] maps a C# property to the real column name in SQL Server.
    // [StringLength(n)] must be the SAME size as the VARCHAR(n) in the SQL script,
    // otherwise the API accepts text that SQL Server then refuses (truncation error).

    public class User
    {
        [Key]
        [Column("User_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        // The database column holds the password HASH (never the plain password).
        // [JsonIgnore] means this property is never read from or written to JSON, so a password hash can never leak out in an API response.
        [Required]
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;

        // Write-only JSON field. When a client sends {"PasswordHash": "typed password"}
        // (the field  name the Part 1 plan uses) it lands here and is copied into PasswordHash above,
        // where the controller hashes it before saving. It has no getter, so it is never sent back out.
        [NotMapped]
        [JsonPropertyName("PasswordHash")]
        public string PasswordInput
        {
            set => PasswordHash = value;
        }

        // Only two roles exist: "Organiser" and "Participant"
        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Participant";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(20)]
        public string? Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }
    }

    public class Venue
    {
        [Key]
        [Column("Venue_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int VenueId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string City { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Province { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Address { get; set; } = string.Empty;
    }

    public class Event
    {
        [Key]
        [Column("Event_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EventId { get; set; }

        [Column("Organiser_ID")]
        public int OrganiserId { get; set; }

        [Column("Venue_ID")]
        public int VenueId { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public DateTime EventDate { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Upcoming";

        [Required]
        [StringLength(50)]
        public string EventType { get; set; } = string.Empty;

        [ForeignKey("OrganiserId")]
        public User? Organiser { get; set; }

        [ForeignKey("VenueId")]
        public Venue? Venue { get; set; }
    }

    public class EventCategory
    {
        [Key]
        [Column("Category_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int CategoryId { get; set; }

        [Column("Event_ID")]
        public int EventId { get; set; }

        [Required]
        [StringLength(50)]
        public string CategoryName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(5,2)")]
        public decimal DistanceKm { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal EntryFee { get; set; }

        public int MaxParticipants { get; set; }

        public DateTime StartDateTime { get; set; }

        public int? MinAge { get; set; }

        public string? RouteDescription { get; set; }

        [ForeignKey("EventId")]
        public Event? Event { get; set; }
    }

    // The SQL table is called "Enrollment" (singular) but the DbSet is "Enrollments" (plural).
    // Without this [Table] line EF looks for a table called "Enrollments" and fails with "Invalid object name 'Enrollments'".
    [Table("Enrollment")]
    public class Enrollment
    {
        [Key]
        [Column("Enrollment_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int EnrollmentId { get; set; }

        [Column("User_ID")]
        public int UserId { get; set; }

        [Column("Category_ID")]
        public int CategoryId { get; set; }

        public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Pending";

        [StringLength(20)]
        public string? BibNumber { get; set; }

        [StringLength(50)]
        public string? EmergencyContact { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("CategoryId")]
        public EventCategory? Category { get; set; }
    }

    public class Result
    {
        [Key]
        [Column("Result_ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ResultId { get; set; }

        [Column("Enrollment_ID")]
        public int EnrollmentId { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal? FinishTimeMinutes { get; set; }

        public int? OverallRank { get; set; }

        // 1 = finished, 0 = did not finish
        public int? Completed { get; set; }

        public int? CategoryRank { get; set; }

        [ForeignKey("EnrollmentId")]
        public Enrollment? Enrollment { get; set; }
    }
}