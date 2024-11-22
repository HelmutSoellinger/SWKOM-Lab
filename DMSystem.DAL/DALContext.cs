using System;
using Microsoft.EntityFrameworkCore;
using DMSystem.DAL.Models;

namespace DMSystem.DAL
{
    public class DALContext : DbContext
    {
        // Constructor accepting DbContextOptions
        public DALContext(DbContextOptions<DALContext> options) : base(options)
        {
        }

        // DbSet for Documents
        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(d => d.Id);

                entity.Property(d => d.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                // Correct DateOnly to DateTime conversion
                entity.Property(d => d.LastModified)
                    .IsRequired();

                entity.Property(d => d.Author)
                    .IsRequired();

                entity.Property(d => d.Content)
                    .IsRequired()
                    .HasColumnType("bytea"); // PostgreSQL-specific binary type

                entity.Property(d => d.Description)
                    .IsRequired(false); // Optional
            });
        }
    }
}
