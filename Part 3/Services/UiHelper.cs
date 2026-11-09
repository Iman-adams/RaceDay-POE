namespace RaceDay.MVC.Services
{
    // Small helpers used by the views so the same rules are not repeated on every page.

    public static class UiHelper
    {
        // The statuses an organiser can give an enrolment
        public static readonly string[] EnrolmentStatuses = { "Pending", "Paid", "Confirmed" };

        // Colour-coded labels: green = good, amber = waiting, red = bad, blue/grey = event states
        public static string StatusClass(string? status) => status switch
        {
            "Confirmed" or "Paid" => "bg-success",
            "Pending" => "bg-warning text-dark",
            "Cancelled" => "bg-danger",
            "Upcoming" => "bg-primary",
            "Completed" => "bg-secondary",
            _ => "bg-secondary"
        };

        // 125.5 minutes -> "2:05:30"  (the API stores the finish time as minutes)
        public static string FormatFinishTime(decimal? minutes)
        {
            if (minutes == null) return "-";
            var totalSeconds = (int)Math.Round(minutes.Value * 60m);
            var time = TimeSpan.FromSeconds(totalSeconds);
            return $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}";
        }

        // "2:05:30" -> 125.5 minutes. Returns false when the text is not a valid time.
        public static bool TryParseFinishTime(string? text, out decimal minutes)
        {
            minutes = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var parts = text.Trim().Split(':');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var s)) return false;
            if (h < 0 || h > 99 || m < 0 || m > 59 || s < 0 || s > 59) return false;

            // The API keeps 2 decimal places, so round to that
            minutes = Math.Round((h * 3600m + m * 60m + s) / 60m, 2);
            return minutes > 0;
        }

        // 1 -> "1st", 2 -> "2nd", 47 -> "47th"
        public static string Ordinal(int number)
        {
            var lastTwo = number % 100;
            if (lastTwo is >= 11 and <= 13) return number + "th";
            return (number % 10) switch
            {
                1 => number + "st",
                2 => number + "nd",
                3 => number + "rd",
                _ => number + "th"
            };
        }
    }
}