using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using duolingo_rum.Models;
using Microsoft.EntityFrameworkCore;

namespace duolingo_rum.Services
{
    public class AchievementService : IDisposable
    {
        private readonly _43pRumiantsefContext _context;
        private bool _disposed = false;
        public AchievementService(_43pRumiantsefContext context)
        {
            _context = context;
        }

        public AchievementService()
        {
            _context = new _43pRumiantsefContext();
        }

        /// <summary>
        /// Проверяем все достижения после урока и выдаём незаработанные.
        /// Возвращает список НОВЫХ достижений (для показа попапа).
        /// </summary>
        public async Task<List<Achievement>> CheckAndAwardAchievements(Guid userId)
        {
            var newlyEarned = new List<Achievement>();

            try
            {
                // Загружаем юзера со статистикой
                var user = await _context.Users.FindAsync(userId);
                if (user == null) return newlyEarned;

                // Уже полученные достижения
                var alreadyEarned = await _context.UserAchievements
                    .Where(ua => ua.UserId == userId)
                    .Select(ua => ua.AchievementId)
                    .ToListAsync();

                // Все достижения из БД
                var allAchievements = await _context.Achievements.ToListAsync();

                // Считаем статистику пользователя
                var wordsLearned = await _context.WordProgresses
                    .CountAsync(wp => wp.UserId == userId);

                var totalSessions = await _context.LearningSessions
                    .CountAsync(s => s.UserId == userId);

                var totalCorrect = await _context.LearningSessions
                    .Where(s => s.UserId == userId)
                    .SumAsync(s => s.CorrectAnswers ?? 0);

                var perfectSessions = await _context.LearningSessions
                    .Where(s => s.UserId == userId && s.FinishedAt != null
                                && s.WrongAnswers == 0 && s.WordsStudied > 0)
                    .CountAsync();

                Debug.WriteLine($"CheckAchievements: words={wordsLearned}, sessions={totalSessions}, correct={totalCorrect}, perfect={perfectSessions}, streak={user.StreakDays}, xp={user.TotalXp}");

                foreach (var achievement in allAchievements)
                {
                    // Пропускаем уже полученные
                    if (alreadyEarned.Contains(achievement.Id))
                        continue;

                    bool conditionMet = achievement.ConditionType switch
                    {
                        "words_learned" => wordsLearned >= (achievement.ConditionValue ?? 0),
                        "streak_days" => (user.StreakDays ?? 0) >= (achievement.ConditionValue ?? 0),
                        "total_xp" => (user.TotalXp ?? 0) >= (achievement.ConditionValue ?? 0),
                        "sessions_count" => totalSessions >= (achievement.ConditionValue ?? 0),
                        "correct_answers" => totalCorrect >= (achievement.ConditionValue ?? 0),
                        "perfect_sessions" => perfectSessions >= (achievement.ConditionValue ?? 0),
                        _ => false
                    };

                    if (!conditionMet) continue;

                    // Выдаём достижение
                    _context.UserAchievements.Add(new UserAchievement
                    {
                        UserId = userId,
                        AchievementId = achievement.Id,
                        EarnedAt = DateTime.UtcNow
                    });

                    // Начисляем XP за достижение
                    if ((achievement.XpReward ?? 0) > 0)
                    {
                        user.TotalXp = (user.TotalXp ?? 0) + achievement.XpReward!.Value;
                        user.UpdatedAt = DateTime.UtcNow;
                    }

                    newlyEarned.Add(achievement);
                    Debug.WriteLine($"Achievement earned: {achievement.Name}");
                }

                if (newlyEarned.Count > 0)
                    await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckAndAwardAchievements ERROR: {ex.Message}");
            }

            return newlyEarned;
        }

        /// <summary>
        /// Все достижения с флагом — получено или нет.
        /// </summary>
        public async Task<List<(Achievement Achievement, bool IsEarned, DateTime? EarnedAt)>> GetAllAchievementsWithStatus(Guid userId)
        {
            try
            {
                var all = await _context.Achievements.ToListAsync();

                var earned = await _context.UserAchievements
                    .Where(ua => ua.UserId == userId)
                    .ToListAsync();

                return all.Select(a =>
                {
                    var ua = earned.FirstOrDefault(e => e.AchievementId == a.Id);
                    return (a, ua != null, ua?.EarnedAt);
                }).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetAllAchievementsWithStatus ERROR: {ex.Message}");
                return new List<(Achievement, bool, DateTime?)>();
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
}