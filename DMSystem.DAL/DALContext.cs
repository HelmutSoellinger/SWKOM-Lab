using System;
using Microsoft.EntityFrameworkCore;
using DMSystem.DAL.Models;

namespace DMSystem.DAL
{
    public class DALContext : DbContext
    {
        public DALContext(DbContextOptions<DALContext> options) : base(options)
        {
        }

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

                entity.Property(d => d.LastModified)
                    .IsRequired();

                entity.Property(d => d.Author)
                    .IsRequired();

                entity.Property(d => d.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);
            });
        }
    }
}
