using System.ComponentModel.DataAnnotations;

namespace DotNetCourse.Models
{
    public class InputFile
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public string Type { get; set; }
        [Required]
        public string Content { get; set; }
    }
}
