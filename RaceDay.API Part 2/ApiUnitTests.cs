using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.API.Controllers;
using RaceDay.API.Data;
using RaceDay.API.Models;
using RaceDay.API.Security;
using Xunit;

namespace RaceDay.Tests
{
    public class ApiUnitTests
    {
        // =====================================================================
        // Helpers
        // =====================================================================

        // A brand-new in-memory database for every test, so tests never affect each other
        private static RaceDayDbContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<RaceDayDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var db = new RaceDayDbContext(options);
            db.Database.EnsureCreated();
            return db;
        }

        // Gives a controller a fake HTTP request with a session.
        // role == null means "nobody is logged in" (an anonymous visitor).
        private static T WithSession<T>(T controller, string? role, int userId = 0) where T : ControllerBase
        {
            var httpContext = new DefaultHttpContext { Session = new TestSession() };
            if (role != null)
            {
                httpContext.Session.SetString(RaceDayControllerBase.SessionRoleKey, role);
                httpContext.Session.SetInt32(RaceDayControllerBase.SessionUserIdKey, userId);
            }
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static User MakeUser(int id, string role, string email, string password = "secret123", DateTime? dob = null) => new User
        {
            UserId = id,
            FullName = $"Test {role} {id}",
            Email = email,
            PasswordHash = PasswordHasher.Hash(password),
            Role = role,
            DateOfBirth = dob ?? new DateTime(1995, 1, 1)
        };

        // Seeds: organiser 1, organiser 2, participant 3, participant 4,
        //        venue 201, event 301 (owned by organiser 1), category 401 (max 2 people)
        private static async Task<RaceDayDbContext> SeedAsync()
        {
            var db = GetDatabaseContext();
            db.Users.Add(MakeUser(1, "Organiser", "org1@test.co.za"));
            db.Users.Add(MakeUser(2, "Organiser", "org2@test.co.za"));
            db.Users.Add(MakeUser(3, "Participant", "p3@test.co.za"));
            db.Users.Add(MakeUser(4, "Participant", "p4@test.co.za"));
            db.Venues.Add(new Venue { VenueId = 201, Name = "Green Point", City = "Cape Town", Province = "Western Cape", Address = "1 Main Rd" });
            db.Events.Add(new Event { EventId = 301, OrganiserId = 1, VenueId = 201, Title = "Coastal Run", Description = "Scenic", EventDate = DateTime.UtcNow.AddDays(30), Status = "Upcoming", EventType = "Running" });
            db.EventCategories.Add(new EventCategory { CategoryId = 401, EventId = 301, CategoryName = "10km", DistanceKm = 10, EntryFee = 120, MaxParticipants = 2, StartDateTime = DateTime.UtcNow.AddDays(30), MinAge = 14 });
            await db.SaveChangesAsync();
            return db;
        }

        // =====================================================================
        // 1. User registration and login
        // =====================================================================

        [Fact]
        public async Task Register_ValidUser_ReturnsCreated_AndStoresHashedPassword()
        {
            var db = GetDatabaseContext();
            var controller = WithSession(new AuthController(db), null);
            var newUser = new User { FullName = "Test Runner", Email = "Runner@RaceDay.co.za", PasswordHash = "plainPassword1", Role = "Participant" };

            var result = await controller.Register(newUser);

            Assert.IsType<CreatedAtActionResult>(result);
            var saved = db.Users.Single();
            Assert.NotEqual("plainPassword1", saved.PasswordHash);          // never stored as typed
            Assert.True(PasswordHasher.Verify("plainPassword1", saved.PasswordHash));
            Assert.Equal("runner@raceday.co.za", saved.Email);              // email normalised to lower case
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsConflict()
        {
            var db = await SeedAsync();
            var controller = WithSession(new AuthController(db), null);

            var result = await controller.Register(new User { FullName = "Copy", Email = "ORG1@test.co.za", PasswordHash = "secret123", Role = "Participant" });

            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task Register_UnknownRole_ReturnsBadRequest()
        {
            var db = GetDatabaseContext();
            var controller = WithSession(new AuthController(db), null);

            var result = await controller.Register(new User { FullName = "Sneaky", Email = "s@test.co.za", PasswordHash = "secret123", Role = "Admin" });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOk_AndStoresRoleInSession()
        {
            var db = await SeedAsync();
            var controller = WithSession(new AuthController(db), null);

            var result = await controller.Login(new LoginRequest { Email = "org1@test.co.za", Password = "secret123" });

            var ok = Assert.IsType<OkObjectResult>(result);
            var body = Assert.IsType<LoginResponse>(ok.Value);
            Assert.Equal(1, body.UserId);
            Assert.Equal("Organiser", body.Role);
            // The session now remembers who is logged in
            Assert.Equal("Organiser", controller.HttpContext.Session.GetString(RaceDayControllerBase.SessionRoleKey));
            Assert.Equal(1, controller.HttpContext.Session.GetInt32(RaceDayControllerBase.SessionUserIdKey));
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            var db = await SeedAsync();
            var controller = WithSession(new AuthController(db), null);

            var result = await controller.Login(new LoginRequest { Email = "org1@test.co.za", Password = "wrong-password" });

            Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Null(controller.HttpContext.Session.GetString(RaceDayControllerBase.SessionRoleKey));
        }

        [Fact]
        public async Task Login_UnknownEmail_ReturnsUnauthorized()
        {
            var db = await SeedAsync();
            var controller = WithSession(new AuthController(db), null);

            var result = await controller.Login(new LoginRequest { Email = "nobody@test.co.za", Password = "secret123" });

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        // =====================================================================
        // 2. Event management (Organisers can manage events, others cannot)
        // =====================================================================

        [Fact]
        public async Task CreateEvent_Organiser_ReturnsCreated_AndUsesSessionUserAsOrganiser()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EventsController(db), "Organiser", 2);

            // The body claims organiser 1, but the API must use the logged-in organiser (2)
            var result = await controller.PostEvent(new Event { OrganiserId = 1, VenueId = 201, Title = "New Run", Description = "Fun", EventDate = DateTime.UtcNow.AddDays(5), EventType = "Running" });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var ev = Assert.IsType<Event>(created.Value);
            Assert.Equal(2, ev.OrganiserId);
        }

        [Fact]
        public async Task CreateEvent_ParticipantRole_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EventsController(db), "Participant", 3);

            var result = await controller.PostEvent(new Event { VenueId = 201, Title = "Unauthorized Run", Description = "Participants cannot create events", EventType = "Running" });

            var forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, forbidden.StatusCode);
            Assert.Equal(1, db.Events.Count());   // nothing new was saved
        }

        [Fact]
        public async Task CreateEvent_NotLoggedIn_ReturnsUnauthorized401()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EventsController(db), null);

            var result = await controller.PostEvent(new Event { VenueId = 201, Title = "Anonymous Run", Description = "x", EventType = "Running" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task UpdateEvent_OwnerOrganiser_ReturnsOk()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EventsController(db), "Organiser", 1);

            var result = await controller.PutEvent(301, new Event { EventId = 301, VenueId = 201, Title = "Renamed Run", Description = "Updated", EventDate = DateTime.UtcNow.AddDays(31), Status = "Upcoming", EventType = "Running" });

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Renamed Run", db.Events.Single().Title);
        }

        [Fact]
        public async Task UpdateEvent_DifferentOrganiser_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EventsController(db), "Organiser", 2);   // organiser 2 does not own event 301

            var result = await controller.PutEvent(301, new Event { EventId = 301, VenueId = 201, Title = "Hijacked", Description = "x", EventType = "Running" });

            var forbidden = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, forbidden.StatusCode);
            Assert.Equal("Coastal Run", db.Events.Single().Title);
        }

        [Fact]
        public async Task DeleteEvent_OwnerOrganiser_ReturnsNoContent()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EventsController(db), "Organiser", 1);

            var result = await controller.DeleteEvent(301);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal(0, db.Events.Count());
        }

        [Fact]
        public async Task GetEvents_Anonymous_ReturnsList()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EventsController(db), null);   // events are public

            var result = await controller.GetEvents();

            var events = Assert.IsAssignableFrom<IEnumerable<Event>>(result.Value);
            Assert.Single(events);
        }

        // =====================================================================
        // 3. Venues and categories (role rules)
        // =====================================================================

        [Fact]
        public async Task GetVenues_ReturnsListOfVenues()
        {
            var db = GetDatabaseContext();
            db.Venues.Add(new Venue { VenueId = 201, Name = "Green Point Stadium", City = "Cape Town", Province = "Western Cape", Address = "1 Fritz Sonnenberg Rd" });
            await db.SaveChangesAsync();
            var controller = WithSession(new VenuesController(db), null);

            var result = await controller.GetVenues();

            var venues = Assert.IsAssignableFrom<IEnumerable<Venue>>(result.Value);
            Assert.Single(venues);
        }

        [Fact]
        public async Task CreateVenue_ParticipantRole_ReturnsForbidden403()
        {
            var db = GetDatabaseContext();
            var controller = WithSession(new VenuesController(db), "Participant", 3);

            var result = await controller.PostVenue(new Venue { Name = "X", City = "Y", Province = "Z", Address = "A" });

            var forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, forbidden.StatusCode);
        }

        [Fact]
        public async Task AddCategory_OwnerOrganiser_ReturnsCreated()
        {
            var db = await SeedAsync();
            var controller = WithSession(new CategoriesController(db), "Organiser", 1);

            var result = await controller.PostCategory(301, new EventCategory { CategoryName = "21km", DistanceKm = 21.1m, EntryFee = 250, MaxParticipants = 500, StartDateTime = DateTime.UtcNow.AddDays(30) });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var category = Assert.IsType<EventCategory>(created.Value);
            Assert.Equal(301, category.EventId);
        }

        [Fact]
        public async Task AddCategory_ParticipantRole_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            var controller = WithSession(new CategoriesController(db), "Participant", 3);

            var result = await controller.PostCategory(301, new EventCategory { CategoryName = "5km", DistanceKm = 5, EntryFee = 50, MaxParticipants = 100 });

            var forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, forbidden.StatusCode);
        }

        // =====================================================================
        // 4. Event enrolments (Participants enrol, and it is recorded correctly)
        // =====================================================================

        [Fact]
        public async Task Enrol_Participant_ReturnsCreated_AndLinksLoggedInUserToCategory()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EnrollmentsController(db), "Participant", 3);

            // The body claims to be user 4, but the API must record the logged-in participant (3)
            var result = await controller.PostEnrollment(new Enrollment { UserId = 4, CategoryId = 401, PaymentStatus = "Paid", EmergencyContact = "0821234567" });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var enrollment = Assert.IsType<Enrollment>(created.Value);
            Assert.Equal(3, enrollment.UserId);
            Assert.Equal(401, enrollment.CategoryId);
            Assert.Equal("Pending", enrollment.PaymentStatus);   // participants cannot mark themselves as paid
            Assert.Equal(1, db.Enrollments.Count());
        }

        [Fact]
        public async Task Enrol_OrganiserRole_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EnrollmentsController(db), "Organiser", 1);

            var result = await controller.PostEnrollment(new Enrollment { CategoryId = 401 });

            var forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, forbidden.StatusCode);
            Assert.Equal(0, db.Enrollments.Count());
        }

        [Fact]
        public async Task Enrol_NotLoggedIn_ReturnsUnauthorized401()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EnrollmentsController(db), null);

            var result = await controller.PostEnrollment(new Enrollment { CategoryId = 401 });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task Enrol_SameCategoryTwice_ReturnsConflict()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EnrollmentsController(db), "Participant", 3);
            await controller.PostEnrollment(new Enrollment { CategoryId = 401 });

            var second = await controller.PostEnrollment(new Enrollment { CategoryId = 401 });

            Assert.IsType<ConflictObjectResult>(second.Result);
            Assert.Equal(1, db.Enrollments.Count());
        }

        [Fact]
        public async Task Enrol_CategoryFull_ReturnsConflict()
        {
            var db = await SeedAsync();   // category 401 allows only 2 people
            db.Users.Add(MakeUser(5, "Participant", "p5@test.co.za"));
            await db.SaveChangesAsync();
            await WithSession(new EnrollmentsController(db), "Participant", 3).PostEnrollment(new Enrollment { CategoryId = 401 });
            await WithSession(new EnrollmentsController(db), "Participant", 4).PostEnrollment(new Enrollment { CategoryId = 401 });

            var third = await WithSession(new EnrollmentsController(db), "Participant", 5).PostEnrollment(new Enrollment { CategoryId = 401 });

            Assert.IsType<ConflictObjectResult>(third.Result);
            Assert.Equal(2, db.Enrollments.Count());
        }

        [Fact]
        public async Task Enrol_BelowMinimumAge_ReturnsBadRequest()
        {
            var db = await SeedAsync();   // category 401 needs age 14+
            db.Users.Add(MakeUser(6, "Participant", "kid@test.co.za", dob: DateTime.UtcNow.AddYears(-10)));
            await db.SaveChangesAsync();
            var controller = WithSession(new EnrollmentsController(db), "Participant", 6);

            var result = await controller.PostEnrollment(new Enrollment { CategoryId = 401 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetUserEnrollments_AnotherParticipantsId_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            var controller = WithSession(new EnrollmentsController(db), "Participant", 3);

            var result = await controller.GetUserEnrollments(4);   // participant 3 asks for participant 4's enrolments

            var forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, forbidden.StatusCode);
        }

        [Fact]
        public async Task GetEnrollments_Organiser_OnlySeesEnrolmentsForOwnEvents()
        {
            var db = await SeedAsync();
            // A second event (owned by organiser 2) with its own category and an enrolment
            db.Events.Add(new Event { EventId = 302, OrganiserId = 2, VenueId = 201, Title = "Other Run", Description = "x", EventDate = DateTime.UtcNow.AddDays(9), Status = "Upcoming", EventType = "Running" });
            db.EventCategories.Add(new EventCategory { CategoryId = 402, EventId = 302, CategoryName = "5km", DistanceKm = 5, EntryFee = 50, MaxParticipants = 50, StartDateTime = DateTime.UtcNow.AddDays(9) });
            db.Enrollments.Add(new Enrollment { EnrollmentId = 501, UserId = 3, CategoryId = 401 });   // belongs to organiser 1's event
            db.Enrollments.Add(new Enrollment { EnrollmentId = 502, UserId = 4, CategoryId = 402 });   // belongs to organiser 2's event
            await db.SaveChangesAsync();

            var result = await WithSession(new EnrollmentsController(db), "Organiser", 1).GetEnrollments();

            var list = Assert.IsAssignableFrom<IEnumerable<Enrollment>>(result.Value).ToList();
            Assert.Single(list);
            Assert.Equal(501, list[0].EnrollmentId);
        }

        // =====================================================================
        // 5. Results (Organisers capture, Participants view their own)
        // =====================================================================

        [Fact]
        public async Task CaptureResult_OwnerOrganiser_ReturnsCreated()
        {
            var db = await SeedAsync();
            db.Enrollments.Add(new Enrollment { EnrollmentId = 501, UserId = 3, CategoryId = 401 });
            await db.SaveChangesAsync();
            var controller = WithSession(new ResultsController(db), "Organiser", 1);

            var result = await controller.PostResult(new Result { EnrollmentId = 501, FinishTimeMinutes = 48.5m, OverallRank = 1, CategoryRank = 1, Completed = 1 });

            Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(1, db.Results.Count());
        }

        [Fact]
        public async Task CaptureResult_ParticipantRole_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            db.Enrollments.Add(new Enrollment { EnrollmentId = 501, UserId = 3, CategoryId = 401 });
            await db.SaveChangesAsync();
            var controller = WithSession(new ResultsController(db), "Participant", 3);

            var result = await controller.PostResult(new Result { EnrollmentId = 501, FinishTimeMinutes = 1m, OverallRank = 1 });

            var forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, forbidden.StatusCode);
            Assert.Equal(0, db.Results.Count());
        }

        [Fact]
        public async Task CaptureResult_OrganiserOfAnotherEvent_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            db.Enrollments.Add(new Enrollment { EnrollmentId = 501, UserId = 3, CategoryId = 401 });   // event 301 belongs to organiser 1
            await db.SaveChangesAsync();
            var controller = WithSession(new ResultsController(db), "Organiser", 2);

            var result = await controller.PostResult(new Result { EnrollmentId = 501, FinishTimeMinutes = 40m, OverallRank = 1 });

            var forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, forbidden.StatusCode);
        }

        [Fact]
        public async Task GetResultsByUser_Participant_SeesOwnResults_ButNotOthers()
        {
            var db = await SeedAsync();
            db.Enrollments.Add(new Enrollment { EnrollmentId = 501, UserId = 3, CategoryId = 401, BibNumber = "1001" });
            db.Results.Add(new Result { ResultId = 601, EnrollmentId = 501, FinishTimeMinutes = 55.2m, OverallRank = 3, CategoryRank = 2, Completed = 1 });
            await db.SaveChangesAsync();
            var controller = WithSession(new ResultsController(db), "Participant", 3);

            var own = await controller.GetResultsByUser(3);
            var other = await controller.GetResultsByUser(4);

            var rows = Assert.IsAssignableFrom<IEnumerable<ResultView>>(own.Value).ToList();
            Assert.Single(rows);
            Assert.Equal("Test Participant 3", rows[0].FullName);
            Assert.Equal("Coastal Run", rows[0].EventTitle);
            var forbidden = Assert.IsType<ObjectResult>(other.Result);
            Assert.Equal(403, forbidden.StatusCode);
        }

        // =====================================================================
        // 6. User profile
        // =====================================================================

        [Fact]
        public async Task GetUsers_ParticipantRole_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            var controller = WithSession(new UsersController(db), "Participant", 3);

            var result = await controller.GetUsers();

            var forbidden = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(403, forbidden.StatusCode);
        }

        [Fact]
        public async Task UpdateProfile_Self_UpdatesDetails_ButNeverTheRole()
        {
            var db = await SeedAsync();
            var controller = WithSession(new UsersController(db), "Participant", 3);

            var result = await controller.PutUser(3, new UpdateProfileRequest { FullName = "New Name", Email = "new3@test.co.za" });

            Assert.IsType<OkObjectResult>(result);
            var user = db.Users.Single(u => u.UserId == 3);
            Assert.Equal("New Name", user.FullName);
            Assert.Equal("Participant", user.Role);
            Assert.True(PasswordHasher.Verify("secret123", user.PasswordHash));   // password untouched when none supplied
        }

        [Fact]
        public async Task UpdateProfile_SomeoneElse_ReturnsForbidden403()
        {
            var db = await SeedAsync();
            var controller = WithSession(new UsersController(db), "Participant", 3);

            var result = await controller.PutUser(4, new UpdateProfileRequest { FullName = "Hacked", Email = "h@test.co.za" });

            var forbidden = Assert.IsType<ObjectResult>(result);
            Assert.Equal(403, forbidden.StatusCode);
        }
    }

    // =========================================================================
    // A tiny fake session so controllers can be tested without a web server.
    // It stores values in a dictionary, just like the real session stores them per user.
    // =========================================================================
    public class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _sessionStorage = new();

        public bool IsAvailable => true;
        public string Id => "TestSession";
        public IEnumerable<string> Keys => _sessionStorage.Keys;

        public void Clear() => _sessionStorage.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _sessionStorage.Remove(key);
        public void Set(string key, byte[] value) => _sessionStorage[key] = value;
        public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => _sessionStorage.TryGetValue(key, out value);
    }
}