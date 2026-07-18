using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Konqvist.Data.Infrastructure;

/// <summary>
///   Persistence row for a single gameplay state, keyed by (Slot, GameDefinitionId).
///   The full <see cref="Konqvist.Data.Models.GameplayState"/> is stored as validated
///   JSON in <see cref="Payload"/>.
/// </summary>
public class GameplayStateEntity
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Slot { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string GameDefinitionId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(max)")]
    public string Payload { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
}
