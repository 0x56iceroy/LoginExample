using System.ComponentModel.DataAnnotations;

namespace LoginExample.ViewModels
{
    public class ArticleEditViewModel
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Content { get; set; } = string.Empty;
    }
}
