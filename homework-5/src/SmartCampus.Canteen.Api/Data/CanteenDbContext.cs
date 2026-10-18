using Microsoft.EntityFrameworkCore;
using SmartCampus.Canteen.Api.Models;

namespace SmartCampus.Canteen.Api.Data
{
    public class CanteenDbContext : DbContext
    {
        private readonly MigrationPhase _phase;

        public CanteenDbContext(DbContextOptions<CanteenDbContext> options, MigrationSettings migration)
            : base(options)
        {
            _phase = migration.ReceiptAmountPhase;
        }

        public DbSet<Receipt> Receipts => Set<Receipt>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var receipt = modelBuilder.Entity<Receipt>();

            receipt.ToTable("Receipts");
            receipt.HasKey(r => r.Id);
            receipt.Property(r => r.StudentCardId).HasMaxLength(32).IsRequired();
            receipt.Property(r => r.Description).HasMaxLength(256).IsRequired();
            receipt.Property(r => r.Amount).HasColumnType("numeric(10,2)");
            receipt.Property(r => r.Currency).HasMaxLength(3).IsRequired();
            receipt.Property(r => r.Status).HasConversion<int>();

#pragma warning disable CS0618 // Price устарела, но нужна до фазы Contract
            if (_phase == MigrationPhase.Expand)
            {
                receipt.Property(r => r.Price).HasColumnType("numeric(10,2)");
            }
            else
            {
                // После 03_contract.sql колонки Price в базе нет — EF Core не должен к ней обращаться.
                receipt.Ignore(r => r.Price);
            }
#pragma warning restore CS0618
        }
    }
}
