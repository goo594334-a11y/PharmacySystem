using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Models.ViewModels.Categories;

public class CategoryListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ProductsCount { get; set; }
    public bool IsActive { get; set; }
}

public class CreateCategoryViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [StringLength(100, ErrorMessage = "StringLengthMax")]
    [Display(Name = "CategoryName")]
    public string Name { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;
}

public class EditCategoryViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(100, ErrorMessage = "StringLengthMax")]
    [Display(Name = "CategoryName")]
    public string Name { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; }
}
