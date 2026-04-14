using System;
using System.Collections.Generic;

namespace duolingo_rum.Models;

public partial class Language
{
    public int Id { get; set; }

    public string Code { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string NativeName { get; set; } = null!;

    public string? FlagEmoji { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<LearningSession> LearningSessions { get; set; } = new List<LearningSession>();

    public virtual ICollection<Topic> Topics { get; set; } = new List<Topic>();

    public virtual ICollection<User> UserNativeLanguages { get; set; } = new List<User>();

    public virtual ICollection<User> UserTargetLanguages { get; set; } = new List<User>();

    public virtual ICollection<Word> Words { get; set; } = new List<Word>();
}
