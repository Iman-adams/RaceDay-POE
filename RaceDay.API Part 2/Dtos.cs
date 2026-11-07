using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RaceDay.API.Models
{
    // DTOs ("data transfer objects") are small classes that describe exactly what an endpoint receives or returns.
    // They stop us exposing or accepting more than we should (for example a password hash, or a Role chosen by the caller).

    // Body for POST /api/auth/login.
    public class LoginRequest
    {
        [Required]
        public string Email { get; set; } = string.Empty;

        // Part 1's plan calls this field "PasswordHash", so we accept that name.
        // "Password" is accepted too because it is the more natural name for a form field.
        public string? PasswordHash { get; set; }
        public string? Password { get; set; }
    }

    // What a successful login returns (matches the Part 1 plan: User_ID + Role).
    public class LoginResponse
    {
        [JsonPropertyName("User_ID")]
        public int UserId { get; set; }

        [JsonPropertyName("Role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("FullName")]
        public string FullName { get; set; } = string.Empty;
    }

    // Body for PUT /api/users/{id}. The Part 1 plan also lists "Role" in this body,
    // but a user must NOT be able to promote themselves to Organiser,
    // so Role is deliberately not here (if a client sends it, it is simply ignored).
    public class UpdateProfileRequest
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(20)]
        public string? Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        // Optional: only fill this in to CHANGE the password. Leave empty to keep the current one.
        public string? PasswordHash { get; set; }
    }

    // A flat, safe-to-show view of a race result (leaderboards, personal history).
    // It deliberately leaves out private data such as the emergency contact and email.
    public class ResultView
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
}