using System.ComponentModel.DataAnnotations;

namespace LoginExample.Models
{
    public class Category
    {
        public int Id { get; set; }
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public ICollection<Article> Articles { get; set; } = new List<Article>();

    }
}
