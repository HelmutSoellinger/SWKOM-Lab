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
                    .HasMaxLength(100); // Define required field and max length

                entity.Property(d => d.LastModified)
                    .IsRequired();

                entity.Property(d => d.Author)
                    .IsRequired();

                entity.Property(d => d.Content)
                    .IsRequired()
                    .HasColumnType("bytea"); // Specify the column type for storing binary data

                entity.Property(d => d.Description)
                    .IsRequired(false); // Make Description optional
            });
        }
    }
}
