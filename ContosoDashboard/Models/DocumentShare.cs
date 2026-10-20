using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentShare
{
    [Key]
    public int DocumentShareId { get; set; }

    [Required]
    public int DocumentId { get; set; }

    // Exactly one of SharedWithUserId / SharedWithProjectId is set
    public int? SharedWithUserId { get; set; }

    public int? SharedWithProjectId { get; set; }

    [Required]
    public int SharedByUserId { get; set; }

    public DateTime SharedDate { get; set; } = DateTime.UtcNow;

    [ForeignKey("DocumentId")]
    public virtual Document Document { get; set; } = null!;

    [ForeignKey("SharedWithUserId")]
    public virtual User? SharedWithUser { get; set; }

    [ForeignKey("SharedWithProjectId")]
    public virtual Project? SharedWithProject { get; set; }

    [ForeignKey("SharedByUserId")]
    public virtual User SharedBy { get; set; } = null!;
}
