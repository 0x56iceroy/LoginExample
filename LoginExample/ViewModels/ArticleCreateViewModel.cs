using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace LoginExample.ViewModels
{
    public class ArticleCreateViewModel
    {
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "請選擇文章分類")]
        public int CategoryId { get; set; } = 1;

        public List<SelectListItem> Categories { get; set; } = new();
    }
}
