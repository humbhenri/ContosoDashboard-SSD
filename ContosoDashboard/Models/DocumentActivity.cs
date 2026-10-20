using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoDashboard.Models;

public class DocumentActivity
{
    [Key]
    public int DocumentActivityId { get; set; }

    // No FK to Document so audit history survives deletion
    public int DocumentId { get; set; }

    [MaxLength(255)]
    public string DocumentTitle { get; set; } = string.Empty;

    [MaxLength(255)]
    public string FileType { get; set; } = string.Empty;

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(30)]
    public string Action { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public virtual User User { get; set; } = null!;
}

public static class DocumentActions
{
    public const string Upload = "Upload";
    public const string Download = "Download";
    public const string EditMetadata = "EditMetadata";
    public const string ReplaceFile = "ReplaceFile";
    public const string Delete = "Delete";
    public const string Share = "Share";
    public const string RevokeShare = "RevokeShare";
}
