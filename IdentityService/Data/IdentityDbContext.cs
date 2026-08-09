using System;
using IdentityService.Configuration;
using IdentityService.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Data
{
    public class IdentityDbContext : DbContext
    {
       
            
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        public IdentityDbContext(DbContextOptions<IdentityDbContext> options) 
            : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(
             typeof(IdentityDbContext).Assembly);
        }
        
    }


}
