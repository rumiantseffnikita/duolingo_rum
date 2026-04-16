using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using duolingo_rum.Models;
using Microsoft.Extensions.Configuration;

namespace duolingo_rum.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private readonly bool _useRealAI;
        private readonly Random _random = new Random();
        private readonly string _apiKey;
        private readonly string _folderId;

        public AIService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();

                _apiKey = config["YandexAI:ApiKey"];
                _folderId = config["YandexAI:FolderId"];
                _useRealAI = !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_folderId);
            }
            catch
            {
                _useRealAI = false;
            }
        }

        // ─────────────────────────────────────────
        // ПУБЛИЧНОЕ API
        // ─────────────────────────────────────────

        /// <summary>
        /// Фидбек при НЕПРАВИЛЬНОМ ответе.
        /// </summary>
        public async Task<string> GenerateFeedback(Word word, string userAnswer)
        {
            if (_useRealAI)
            {
                var prompt = $@"Ты — дружелюбный AI-репетитор английского языка.
Студент переводил слово и ошибся.
Слово: '{word.Word1}'
Правильный перевод: '{word.Translation}'
Ответ студента: '{userAnswer}'
{(word.ExampleSentence != null ? $"Пример: {word.ExampleSentence}" : "")}

Напиши короткий мотивирующий фидбек (максимум 2 предложения на русском):
— мягко укажи на ошибку
— дай конкретный мнемонический совет как запомнить
— используй 1-2 эмодзи";

                return await CallYandexGPT(prompt, maxTokens: 120)
                       ?? DemoFeedback(word, userAnswer);
            }

            return DemoFeedback(word, userAnswer);
        }

        /// <summary>
        /// Похвала при ПРАВИЛЬНОМ ответе — появляется иногда, не каждый раз.
        /// </summary>
        public async Task<string> GeneratePraise(Word word)
        {
            // Показываем похвалу в ~40% случаев чтобы не надоедало
            if (_random.NextDouble() > 0.4)
                return string.Empty;

            if (_useRealAI)
            {
                var prompt = $@"Студент правильно перевёл слово '{word.Word1}' = '{word.Translation}'.
Напиши очень короткую похвалу (1 предложение, русский, 1 эмодзи). Разнообразь варианты.";

                return await CallYandexGPT(prompt, maxTokens: 60)
                       ?? DemoPraise();
            }

            return DemoPraise();
        }

        /// <summary>
        /// Генерация примера предложения прямо в уроке.
        /// </summary>
        public async Task<string> GenerateExampleSentence(Word word)
        {
            // Если в БД уже есть пример — берём его
            if (!string.IsNullOrWhiteSpace(word.ExampleSentence))
            {
                var translation = word.ExampleTranslation ?? string.Empty;
                return $"📖 {word.ExampleSentence}\n💬 {translation}";
            }

            if (_useRealAI)
            {
                var prompt = $@"Составь одно простое предложение на английском со словом '{word.Word1}' (перевод: '{word.Translation}').
Уровень: beginner. 
Формат строго:
EN: <предложение>
RU: <перевод предложения>";

                var result = await CallYandexGPT(prompt, maxTokens: 80);
                return result != null ? $"📖 {result}" : DemoExample(word);
            }

            return DemoExample(word);
        }

        /// <summary>
        /// Ежедневный совет на дашборде.
        /// </summary>
        public async Task<string> GenerateDailyTip(User user, int wordsLearned, int streakDays)
        {
            if (_useRealAI)
            {
                var prompt = $@"Ты — AI-мотиватор приложения для изучения английского.
Статистика: выучено слов: {wordsLearned}, серия: {streakDays} дней, XP: {user.TotalXp ?? 0}.
Напиши персонализированный совет дня (30-50 слов, русский, 1-2 эмодзи). Будь конкретным и дружелюбным.";

                return await CallYandexGPT(prompt, maxTokens: 150)
                       ?? DemoDailyTip(wordsLearned, streakDays);
            }

            return DemoDailyTip(wordsLearned, streakDays);
        }

        /// <summary>
        /// AI-анализ слабых мест по истории ошибок.
        /// </summary>
        public async Task<string> GenerateWeaknessAnalysis(int totalCorrect, int totalWrong, int streak)
        {
            var accuracy = totalCorrect + totalWrong > 0
                ? (int)((double)totalCorrect / (totalCorrect + totalWrong) * 100)
                : 0;

            if (_useRealAI)
            {
                var prompt = $@"Студент изучает английский. Его статистика:
— Точность: {accuracy}%
— Правильных: {totalCorrect}, ошибок: {totalWrong}
— Серия дней: {streak}

Напиши короткий анализ (2-3 предложения, русский, эмодзи): что идёт хорошо и над чем поработать.";

                return await CallYandexGPT(prompt, maxTokens: 150)
                       ?? DemoWeaknessAnalysis(accuracy);
            }

            return DemoWeaknessAnalysis(accuracy);
        }

        // ─────────────────────────────────────────
        // YANDEX GPT
        // ─────────────────────────────────────────

        private async Task<string?> CallYandexGPT(string prompt, int maxTokens = 150)
        {
            try
            {
                var body = new
                {
                    modelUri = $"gpt://{_folderId}/yandexgpt/latest",
                    completionOptions = new { temperature = 0.7, maxTokens },
                    messages = new[] { new { role = "user", text = prompt } }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(body),
                    Encoding.UTF8,
                    "application/json"
                );

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Api-Key {_apiKey}");

                var response = await _httpClient.PostAsync(
                    "https://llm.api.cloud.yandex.net/foundationModels/v1/completion",
                    content
                );

                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"YandexGPT error: {json}");
                    return null;
                }

                using var doc = JsonDocument.Parse(json);
                return doc.RootElement
                    .GetProperty("result")
                    .GetProperty("alternatives")[0]
                    .GetProperty("message")
                    .GetProperty("text")
                    .GetString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CallYandexGPT ERROR: {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────
        // ДЕМО-РЕЖИМ (когда AI недоступен)
        // ─────────────────────────────────────────

        private string DemoFeedback(Word word, string userAnswer)
        {
            var tips = new[]
            {
                $"❌ Почти! '{word.Word1}' = '{word.Translation}'. 💡 Придумай ассоциацию с русским словом.",
                $"📚 Ошибка: '{userAnswer}' → правильно '{word.Translation}'. 🔊 Произнеси вслух 3 раза!",
                $"🧠 '{word.Word1}' переводится как '{word.Translation}'. Повтори через час — запомнишь навсегда!",
                $"💪 Не сдавайся! Правильно: {word.Translation}. ✨ Этот урок сделает тебя лучше!"
            };
            return tips[_random.Next(tips.Length)];
        }

        private string DemoPraise()
        {
            var praise = new[]
            {
                "🔥 Отлично! Так держать!",
                "⭐ Правильно! Ты делаешь успехи!",
                "💪 Молодец! Продолжай в том же духе!",
                "🎯 Точно в цель!"
            };
            return praise[_random.Next(praise.Length)];
        }

        private string DemoExample(Word word)
        {
            return $"📖 I see a {word.Word1.ToLower()} here.\n💬 Я вижу здесь «{word.Translation.ToLower()}».";
        }

        private string DemoDailyTip(int wordsLearned, int streakDays)
        {
            if (streakDays >= 7)
                return $"🔥 Неделя подряд! {wordsLearned} слов — серьёзный результат. Сегодня попробуй читать простой текст на английском.";
            if (wordsLearned >= 20)
                return $"⭐ {wordsLearned} слов уже в копилке! Повтори 10 старых — интервальное повторение в 2 раза эффективнее.";
            if (streakDays > 0)
                return $"📅 Серия: {streakDays} дней! Маленькие шаги каждый день важнее редких марафонов.";

            var tips = new[]
            {
                "💡 10 минут в день эффективнее, чем час раз в неделю. Начни прямо сейчас!",
                "🚀 Первые 100 слов — самые важные. У тебя всё получится!",
                "🎧 Совет: слушай английские подкасты в фоне — мозг привыкает к звучанию.",
                "📖 Учи слова в контексте примеров — запоминается в 3 раза быстрее."
            };
            return tips[_random.Next(tips.Length)];
        }

        private string DemoWeaknessAnalysis(int accuracy)
        {
            if (accuracy >= 80)
                return $"🎯 Точность {accuracy}% — отличный результат! Продолжай в том же темпе и скоро перейдёшь на более сложные слова.";
            if (accuracy >= 60)
                return $"📊 Точность {accuracy}% — хороший прогресс. 💡 Уделяй больше внимания словам, которые повторяются в уроках — это твои слабые места.";
            return $"💪 Точность {accuracy}% — есть куда расти! Не торопись, лучше делай меньше слов, но увереннее. Повторяй ошибочные слова каждый день.";
        }
    }
}