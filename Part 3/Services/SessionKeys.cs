namespace RaceDay.MVC.Services
{
    // The names of the values we keep in the user's session.
    // Using constants (instead of typing the text each time) means a spelling mistake becomes a build error.
    public static class SessionKeys
    {
        public const string UserId = "UserId";
        public const string UserName = "UserName";
        public const string UserRole = "UserRole";

        // The API's own session cookie. We keep it here so we can send it back to the API on every call.
        public const string ApiCookie = "ApiCookie";
    }

    // The only two roles in RaceDay (these must match the roles the API uses)
    public static class RoleNames
    {
        public const string Organiser = "Organiser";
        public const string Participant = "Participant";
    }
}