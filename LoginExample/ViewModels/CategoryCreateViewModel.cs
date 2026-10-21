using System.ComponentModel.DataAnnotations;

namespace LoginExample.ViewModels
{
    public class CategoryCreateViewModel
    {

        public int Id { get; set; }

        [Required(ErrorMessage = "請輸入分類名稱")]
        [StringLength(100, ErrorMessage = "分類名稱不可超過100個字元")]
        public string Name { get; set; } = string.Empty;

        public int ArticleCount { get; set; }

    }
}
