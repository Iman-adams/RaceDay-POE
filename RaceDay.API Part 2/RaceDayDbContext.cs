using Microsoft.EntityFrameworkCore;
using RaceDay.API.Models;

namespace RaceDay.API.Data
{
    // The DbContext is EF Core's connection to the database.
    // Each DbSet<T> represents one table.
    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Venue> Venues { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<EventCategory> EventCategories { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }  // table name is "Enrollment" (see [Table] on the model)
        public DbSet<Result> Results { get; set; }

        // Extra rules that migrations turn into database constraints
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // No two accounts can share an email, and Role can only be one of the two real roles
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();
                entity.ToTable(t => t.HasCheckConstraint("CK_Users_Role", "[Role] IN ('Organiser', 'Participant')"));
            });

            // A participant can enter a category only once
            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.UserId, e.CategoryId })
                .IsUnique();

            // One enrolment has at most one result
            modelBuilder.Entity<Result>()
                .HasIndex(r => r.EnrollmentId)
                .IsUnique();

            // Never delete child rows automatically. Without this, SQL Server rejects the migration ("multiple cascade paths"),
            // and the API's "this still has events/enrolments" 409 replies would never happen because rows would silently disappear instead.
            foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}