using System;
using System.Collections.Generic;

namespace duolingo_rum.Models;

public partial class Topic
{
    public int Id { get; set; }

    public int LanguageId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? IconEmoji { get; set; }

    public int? SortOrder { get; set; }

    public virtual Language Language { get; set; } = null!;

    public virtual ICollection<Word> Words { get; set; } = new List<Word>();
}
