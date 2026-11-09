namespace RaceDay.MVC.Services
{
    /// <summary>
    /// The outcome of one call to the API. Instead of throwing errors, every call returns one of these,
    /// so a controller can simply check Success and show a friendly message if something went wrong.
    /// </summary>
    public class ApiResult<T>
    {
        public bool Success { get; private set; }
        public T? Data { get; private set; }
        public int StatusCode { get; private set; }
        public string? ErrorMessage { get; private set; }

        public bool IsUnauthorized => StatusCode == 401;  // not logged in
        public bool IsForbidden => StatusCode == 403;  // logged in, but wrong role

        public static ApiResult<T> Ok(T? data, int statusCode = 200) =>
            new() { Success = true, Data = data, StatusCode = statusCode };

        public static ApiResult<T> Fail(int statusCode, string message) =>
            new() { Success = false, StatusCode = statusCode, ErrorMessage = message };
    }
}