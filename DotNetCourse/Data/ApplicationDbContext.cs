using DotNetCourse.Models;
using Microsoft.EntityFrameworkCore;

namespace DotNetCourse.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {           
        }

        public DbSet<InputFile> Files { get; set; }
        public DbSet<FileNode> Nodes { get; set; }

    }
}
