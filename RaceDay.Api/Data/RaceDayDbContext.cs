using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core code-first context. The model mirrors the Part 1 ERD and RaceDaySQL.sql
/// (same table names, column names, data types, keys and delete behaviours).
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<AppUser>(e =>
        {
            e.ToTable("AppUser");
            e.HasKey(x => x.UserID);
            e.Property(x => x.UserName).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.UserSurname).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.Email).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsUnicode(false).IsRequired();
            e.Property(x => x.Role).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        mb.Entity<EventType>(e =>
        {
            e.ToTable("EventType");
            e.HasKey(x => x.EventTypeID);
            e.Property(x => x.TypeName).HasMaxLength(50).IsUnicode(false).IsRequired();
            e.HasData(
                new EventType { EventTypeID = 1, TypeName = "Run" },
                new EventType { EventTypeID = 2, TypeName = "Walk" },
                new EventType { EventTypeID = 3, TypeName = "Cycle" });
        });

        mb.Entity<Event>(e =>
        {
            e.ToTable("Event");
            e.HasKey(x => x.EventID);
            e.Property(x => x.EventName).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500).IsUnicode(false).IsRequired();
            e.Property(x => x.EventDate).HasColumnType("datetime");
            e.Property(x => x.EventLocation).HasMaxLength(100).IsUnicode(false).IsRequired();
            e.Property(x => x.Distance).HasColumnType("float");

            e.HasOne(x => x.EventType).WithMany(t => t.Events)
                .HasForeignKey(x => x.EventTypeID).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Organiser).WithMany(u => u.OrganisedEvents)
                .HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Category>(e =>
        {
            e.ToTable("Category");
            e.HasKey(x => x.CategoryID);
            e.Property(x => x.CategoryName).HasMaxLength(50).IsRequired();      // NVARCHAR(50)
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();      // NVARCHAR(200)
            e.Property(x => x.Age).HasMaxLength(20).IsUnicode(false).IsRequired();
            e.Property(x => x.Distance).HasColumnType("float");

            // ON DELETE CASCADE when the parent event is removed (as in the Part 1 script)
            e.HasOne(x => x.Event).WithMany(ev => ev.Categories)
                .HasForeignKey(x => x.EventID).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Enrolment>(e =>
        {
            e.ToTable("Enrolment");
            e.HasKey(x => x.EnrolmentID);
            e.Property(x => x.EnrolmentDate).HasColumnType("datetime");
            e.Property(x => x.Status).HasMaxLength(20).IsUnicode(false).IsRequired();

            e.HasOne(x => x.Event).WithMany(ev => ev.Enrolments)
                .HasForeignKey(x => x.EventID).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Category).WithMany(c => c.Enrolments)
                .HasForeignKey(x => x.CategoryID).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.User).WithMany(u => u.Enrolments)
                .HasForeignKey(x => x.UserID).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Result>(e =>
        {
            e.ToTable("Result");
            e.HasKey(x => x.ResultID);
            e.Property(x => x.FinishTime).HasColumnType("time");
            e.Property(x => x.PublishedDate).HasColumnType("datetime");

            // ON DELETE CASCADE from Enrolment (as in the Part 1 script)
            e.HasOne(x => x.Enrolment).WithMany(en => en.Results)
                .HasForeignKey(x => x.EnrolmentID).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Event).WithMany(ev => ev.Results)
                .HasForeignKey(x => x.EventID).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
