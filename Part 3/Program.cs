using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RaceDay.MVC.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Session: remembers who is logged in while they move between pages
builder.Services.AddHttpContextAccessor();  // lets ApiSessionHandler reach the current user's session
builder.Services.AddDistributedMemoryCache();  // where session data is kept (the server's memory)
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Mvc.Session";  // different from the API's cookie name
    options.Cookie.HttpOnly = true;  // JavaScript cannot read the cookie
    options.Cookie.IsEssential = true;  // needed for login to work
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

// Connection to the API
// AddHttpClient registers RaceDayApiClient so controllers can ask for it in their constructor.
// The API address comes from appsettings.json, so it can change without touching the code.
builder.Services.AddTransient<ApiSessionHandler>();
builder.Services.AddHttpClient<RaceDayApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"]
        ?? throw new InvalidOperationException("ApiSettings:BaseUrl is missing from appsettings.json.");

    // The address must end with "/" so relative paths like "api/events" are added on correctly
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
.AddHttpMessageHandler<ApiSessionHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// A page that does not exist (404) or is not allowed (403) shows our own friendly error page instead of the browser's blank default.
app.UseStatusCodePagesWithReExecute("/Home/Error");

var invariant = new[] { CultureInfo.InvariantCulture };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = invariant,
    SupportedUICultures = invariant
});

app.UseRouting();

app.UseSession();  // must come after UseRouting and before the pages run

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
