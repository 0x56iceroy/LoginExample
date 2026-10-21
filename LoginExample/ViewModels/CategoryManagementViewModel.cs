namespace LoginExample.ViewModels
{
    public class CategoryManagementViewModel
    {
        public CategoryCreateViewModel Create { get; set; } = new CategoryCreateViewModel();

        public List<CategoryIndexViewModel> Categories { get; set; } = new();
    }
}
