using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DotNetCourse.Models
{
    public class FileNode
    {
        [Key]
        public string Id { get; set; }
        [Required]
        public int DisplayOrder { get; set; } = 0;

		[Required]
        public string Key { get; set; }

        public string? Value { get; set; }

        [Required]
        public int Depth { get; set; }

        public string ? ParentId { get; set; }
        [ForeignKey("ParentId")]
        public FileNode? Parent { get; set; }

        [Required]
        public int InputFileId { get; set; }

        [ForeignKey("InputFileId")]
        public InputFile InputFile { get; set; }
    }
}
