using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    // Show the /// <summary> and <response> comments from the controllers in Swagger UI.
    // (Needs <GenerateDocumentationFile>true</GenerateDocumentationFile> in RaceDay.API.csproj.)
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Configure Entity Framework Core with SQL Server
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- Session management ----
// The session stores WHO is logged in (user id + role) on the server.
// The browser only holds a small cookie containing the session id.
builder.Services.AddDistributedMemoryCache();   // where session data is kept (in the server's memory)
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;  // JavaScript cannot read the cookie
    options.Cookie.IsEssential = true;  // needed for login, so no cookie-consent banner needed
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseSession();  // must come BEFORE MapControllers so controllers can read the session
app.UseAuthorization();
app.MapControllers();

app.Run();