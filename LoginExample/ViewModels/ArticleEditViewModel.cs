using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace LoginExample.ViewModels
{
    public class ArticleEditViewModel
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Content { get; set; } = string.Empty;
        [Range(1, int.MaxValue, ErrorMessage = "請選擇有效的文章分類")]
        public int CategoryId { get; set; }
        public List<SelectListItem> Categories { get; set; } = new();
    }
}
