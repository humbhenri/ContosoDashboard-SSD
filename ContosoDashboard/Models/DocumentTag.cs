using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentTag
{
    [Key]
    public int DocumentTagId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    // Stored trimmed and lower-cased
    [Required]
    [MaxLength(50)]
    public string Tag { get; set; } = string.Empty;

    [ForeignKey("DocumentId")]
    public virtual Document Document { get; set; } = null!;
}
