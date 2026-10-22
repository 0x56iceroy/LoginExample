namespace LoginExample.ViewModels
{
    public class CategoryManagementViewModel
    {
        public CategoryCreateViewModel Create { get; set; } = new CategoryCreateViewModel();

        public List<CategoryIndexViewModel> Categories { get; set; } = new();
        public List<CategoryIndexViewModel> DeletedCategories { get; set; } = new();
    }
}
