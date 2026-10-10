using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Models.ViewModels.Locations;

public class LocationListItemViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BatchesCount { get; set; }
    public bool IsActive { get; set; }
}

public class CreateLocationViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [StringLength(50, ErrorMessage = "StringLengthMax")]
    [Display(Name = "LocationCode")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(100, ErrorMessage = "StringLengthMax")]
    [Display(Name = "LocationName")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;
}

public class EditLocationViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(50, ErrorMessage = "StringLengthMax")]
    [Display(Name = "LocationCode")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(100, ErrorMessage = "StringLengthMax")]
    [Display(Name = "LocationName")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Status")]
    public bool IsActive { get; set; }
}
