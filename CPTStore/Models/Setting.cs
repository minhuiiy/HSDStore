using System.ComponentModel.DataAnnotations;

namespace CPTStore.Models
{
    public class Setting
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Key { get; set; } = string.Empty;
        
        [Required]
        public string Value { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        
        public DateTime? UpdatedAt { get; set; }
    }
}