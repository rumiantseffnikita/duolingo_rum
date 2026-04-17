using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using duolingo_rum.Models;
using Microsoft.EntityFrameworkCore;

namespace duolingo_rum.Services
{
    public class StatisticsService : IDisposable
    {
        private readonly _43pRumiantsefContext _context;
        private bool _disposed = false;

        public StatisticsService(_43pRumiantsefContext context)
        {
            _context = context;
        }

        public StatisticsService()
        {
            _context = new _43pRumiantsefContext();
        }

        public async Task<UserStats> GetUserStats(Guid userId)
        {
            try
            {
                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);

                var sessions = await _context.LearningSessions
                    .AsNoTracking()
                    .Where(s => s.UserId == userId && s.FinishedAt != null)
                    .OrderByDescending(s => s.StartedAt)
                    .Take(20)
                    .ToListAsync();

                var totalWords = await _context.WordProgresses.CountAsync(wp => wp.UserId == userId);

                var today = DateTime.UtcNow.Date;
                var weekAgo = today.AddDays(-7);

                var allSessionIds = sessions.Select(s => s.Id).ToList();

                var todayCorrect = await _context.ExerciseResults
                    .Where(r => allSessionIds.Contains(r.SessionId)
                                && r.AnsweredAt.HasValue
                                && r.AnsweredAt.Value.Date == today
                                && r.IsCorrect)
                    .CountAsync();

                var weekCorrect = await _context.ExerciseResults
                    .Where(r => allSessionIds.Contains(r.SessionId)
                                && r.AnsweredAt.HasValue
                                && r.AnsweredAt.Value.Date >= weekAgo)
                    .CountAsync();

                var totalCorrect = sessions.Sum(s => s.CorrectAnswers ?? 0);
                var totalWrong = sessions.Sum(s => s.WrongAnswers ?? 0);
                var totalAnswered = totalCorrect + totalWrong;

                return new UserStats
                {
                    TotalXp = user?.TotalXp ?? 0,
                    StreakDays = user?.StreakDays ?? 0,
                    LongestStreak = user?.LongestStreak ?? 0,
                    TotalWordsLearned = totalWords,
                    TotalSessions = sessions.Count,
                    TotalCorrectAnswers = totalCorrect,
                    TotalWrongAnswers = totalWrong,
                    AccuracyPercent = totalAnswered > 0 ? (int)((double)totalCorrect / totalAnswered * 100) : 0,
                    TodayCorrect = todayCorrect,
                    WeeklyAnswers = weekCorrect,
                    RecentSessions = sessions
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetUserStats ERROR: {ex.Message}");
                return new UserStats();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _context?.Dispose();
                _disposed = true;
            }
        }
    }

    // DTO — чтобы не тащить кучу полей по всему коду
    public class UserStats
    {
        public int TotalXp { get; set; }
        public int StreakDays { get; set; }
        public int LongestStreak { get; set; }
        public int TotalWordsLearned { get; set; }
        public int TotalSessions { get; set; }
        public int TotalCorrectAnswers { get; set; }
        public int TotalWrongAnswers { get; set; }
        public int AccuracyPercent { get; set; }
        public int TodayCorrect { get; set; }
        public int WeeklyAnswers { get; set; }
        public List<LearningSession> RecentSessions { get; set; } = new();
    }
}