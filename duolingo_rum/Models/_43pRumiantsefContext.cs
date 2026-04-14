using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace duolingo_rum.Models;

public partial class _43pRumiantsefContext : DbContext
{
    public _43pRumiantsefContext()
    {
    }

    public _43pRumiantsefContext(DbContextOptions<_43pRumiantsefContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Achievement> Achievements { get; set; }

    public virtual DbSet<ExerciseResult> ExerciseResults { get; set; }

    public virtual DbSet<Language> Languages { get; set; }

    public virtual DbSet<LearningSession> LearningSessions { get; set; }

    public virtual DbSet<Topic> Topics { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAchievement> UserAchievements { get; set; }

    public virtual DbSet<Word> Words { get; set; }

    public virtual DbSet<WordProgress> WordProgresses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=edu.pg.ngknn.ru; Port=5442; Database=43P_Rumiantsef; Username=43P; Password=444444");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("duolingo", "difficulty_level", new[] { "beginner", "intermediate", "advanced" })
            .HasPostgresEnum("duolingo", "exercise_type", new[] { "flashcard", "multiple_choice", "translation", "listening", "fill_blank" })
            .HasPostgresExtension("duolingo", "pgcrypto");

        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("achievements_pkey");

            entity.ToTable("achievements", "duolingo");

            entity.HasIndex(e => e.Code, "achievements_code_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.ConditionType)
                .HasMaxLength(50)
                .HasColumnName("condition_type");
            entity.Property(e => e.ConditionValue).HasColumnName("condition_value");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IconEmoji)
                .HasMaxLength(10)
                .HasColumnName("icon_emoji");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.XpReward)
                .HasDefaultValue(0)
                .HasColumnName("xp_reward");
        });

        modelBuilder.Entity<ExerciseResult>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("exercise_results_pkey");

            entity.ToTable("exercise_results", "duolingo");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AiFeedback).HasColumnName("ai_feedback");
            entity.Property(e => e.AnsweredAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("answered_at");
            entity.Property(e => e.IsCorrect).HasColumnName("is_correct");
            entity.Property(e => e.ResponseTimeMs).HasColumnName("response_time_ms");
            entity.Property(e => e.SessionId).HasColumnName("session_id");
            entity.Property(e => e.UserAnswer).HasColumnName("user_answer");
            entity.Property(e => e.WordId).HasColumnName("word_id");

            entity.HasOne(d => d.Session).WithMany(p => p.ExerciseResults)
                .HasForeignKey(d => d.SessionId)
                .HasConstraintName("exercise_results_session_id_fkey");

            entity.HasOne(d => d.Word).WithMany(p => p.ExerciseResults)
                .HasForeignKey(d => d.WordId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("exercise_results_word_id_fkey");
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("languages_pkey");

            entity.ToTable("languages", "duolingo");

            entity.HasIndex(e => e.Code, "languages_code_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(10)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.FlagEmoji)
                .HasMaxLength(10)
                .HasColumnName("flag_emoji");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.NativeName)
                .HasMaxLength(100)
                .HasColumnName("native_name");
        });

        modelBuilder.Entity<LearningSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("learning_sessions_pkey");

            entity.ToTable("learning_sessions", "duolingo");

            entity.HasIndex(e => e.UserId, "idx_sessions_user");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CorrectAnswers)
                .HasDefaultValue(0)
                .HasColumnName("correct_answers");
            entity.Property(e => e.DurationSec).HasColumnName("duration_sec");
            entity.Property(e => e.FinishedAt).HasColumnName("finished_at");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.StartedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("started_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WordsStudied)
                .HasDefaultValue(0)
                .HasColumnName("words_studied");
            entity.Property(e => e.WrongAnswers)
                .HasDefaultValue(0)
                .HasColumnName("wrong_answers");
            entity.Property(e => e.XpEarned)
                .HasDefaultValue(0)
                .HasColumnName("xp_earned");

            entity.HasOne(d => d.Language).WithMany(p => p.LearningSessions)
                .HasForeignKey(d => d.LanguageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("learning_sessions_language_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.LearningSessions)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("learning_sessions_user_id_fkey");
        });

        modelBuilder.Entity<Topic>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("topics_pkey");

            entity.ToTable("topics", "duolingo");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IconEmoji)
                .HasMaxLength(10)
                .HasColumnName("icon_emoji");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.SortOrder)
                .HasDefaultValue(0)
                .HasColumnName("sort_order");

            entity.HasOne(d => d.Language).WithMany(p => p.Topics)
                .HasForeignKey(d => d.LanguageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("topics_language_id_fkey");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users", "duolingo");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DailyGoalMinutes)
                .HasDefaultValue(15)
                .HasColumnName("daily_goal_minutes");
            entity.Property(e => e.DailyGoalWords)
                .HasDefaultValue(10)
                .HasColumnName("daily_goal_words");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.LastActivityDate).HasColumnName("last_activity_date");
            entity.Property(e => e.LongestStreak)
                .HasDefaultValue(0)
                .HasColumnName("longest_streak");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.NativeLanguageId).HasColumnName("native_language_id");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.StreakDays)
                .HasDefaultValue(0)
                .HasColumnName("streak_days");
            entity.Property(e => e.TargetLanguageId).HasColumnName("target_language_id");
            entity.Property(e => e.TotalXp)
                .HasDefaultValue(0)
                .HasColumnName("total_xp");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.NativeLanguage).WithMany(p => p.UserNativeLanguages)
                .HasForeignKey(d => d.NativeLanguageId)
                .HasConstraintName("users_native_language_id_fkey");

            entity.HasOne(d => d.TargetLanguage).WithMany(p => p.UserTargetLanguages)
                .HasForeignKey(d => d.TargetLanguageId)
                .HasConstraintName("users_target_language_id_fkey");
        });

        modelBuilder.Entity<UserAchievement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_achievements_pkey");

            entity.ToTable("user_achievements", "duolingo");

            entity.HasIndex(e => new { e.UserId, e.AchievementId }, "user_achievements_user_id_achievement_id_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AchievementId).HasColumnName("achievement_id");
            entity.Property(e => e.EarnedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("earned_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Achievement).WithMany(p => p.UserAchievements)
                .HasForeignKey(d => d.AchievementId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_achievements_achievement_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.UserAchievements)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("user_achievements_user_id_fkey");
        });

        modelBuilder.Entity<Word>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("words_pkey");

            entity.ToTable("words", "duolingo");

            entity.HasIndex(e => e.LanguageId, "idx_words_language");

            entity.HasIndex(e => e.TopicId, "idx_words_topic");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExampleSentence).HasColumnName("example_sentence");
            entity.Property(e => e.ExampleTranslation).HasColumnName("example_translation");
            entity.Property(e => e.FrequencyRank).HasColumnName("frequency_rank");
            entity.Property(e => e.LanguageId).HasColumnName("language_id");
            entity.Property(e => e.TopicId).HasColumnName("topic_id");
            entity.Property(e => e.Transcription)
                .HasMaxLength(255)
                .HasColumnName("transcription");
            entity.Property(e => e.Translation)
                .HasMaxLength(255)
                .HasColumnName("translation");
            entity.Property(e => e.Word1)
                .HasMaxLength(255)
                .HasColumnName("word");

            entity.HasOne(d => d.Language).WithMany(p => p.Words)
                .HasForeignKey(d => d.LanguageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("words_language_id_fkey");

            entity.HasOne(d => d.Topic).WithMany(p => p.Words)
                .HasForeignKey(d => d.TopicId)
                .HasConstraintName("words_topic_id_fkey");
        });

        modelBuilder.Entity<WordProgress>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("word_progress_pkey");

            entity.ToTable("word_progress", "duolingo");

            entity.HasIndex(e => new { e.UserId, e.NextReview }, "idx_word_progress_next_review");

            entity.HasIndex(e => e.UserId, "idx_word_progress_user");

            entity.HasIndex(e => new { e.UserId, e.WordId }, "word_progress_user_id_word_id_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CorrectCount)
                .HasDefaultValue(0)
                .HasColumnName("correct_count");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EasinessFactor)
                .HasPrecision(4, 2)
                .HasDefaultValueSql("2.5")
                .HasColumnName("easiness_factor");
            entity.Property(e => e.IntervalDays)
                .HasDefaultValue(1)
                .HasColumnName("interval_days");
            entity.Property(e => e.IsLearned)
                .HasDefaultValue(false)
                .HasColumnName("is_learned");
            entity.Property(e => e.LastReview).HasColumnName("last_review");
            entity.Property(e => e.NextReview)
                .HasDefaultValueSql("CURRENT_DATE")
                .HasColumnName("next_review");
            entity.Property(e => e.Repetitions)
                .HasDefaultValue(0)
                .HasColumnName("repetitions");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.WordId).HasColumnName("word_id");
            entity.Property(e => e.WrongCount)
                .HasDefaultValue(0)
                .HasColumnName("wrong_count");

            entity.HasOne(d => d.User).WithMany(p => p.WordProgresses)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("word_progress_user_id_fkey");

            entity.HasOne(d => d.Word).WithMany(p => p.WordProgresses)
                .HasForeignKey(d => d.WordId)
                .HasConstraintName("word_progress_word_id_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
