// Services/AIService.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using duolingo_rum.Models;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace duolingo_rum.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly bool _useRealAI;
        private readonly Random _random = new Random();

        private readonly string _apiKey;
        private readonly string _folderId;

        public AIService()
        {
            _httpClient = new HttpClient();

            // Загружаем конфигурацию
            try
            {
                _configuration = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();

                _apiKey = _configuration["YandexAI:ApiKey"];
                _folderId = _configuration["YandexAI:FolderId"];

                // Если ключи есть - используем реальный AI, иначе демо-режим
                _useRealAI = !string.IsNullOrEmpty(_apiKey) && !string.IsNullOrEmpty(_folderId);
            }
            catch
            {
                _useRealAI = false;
            }
        }

        /// <summary>
        /// Генерация персонализированного фидбека на ошибку
        /// </summary>
        public async Task<string> GenerateFeedback(Word word, string userAnswer)
        {
            if (_useRealAI)
            {
                return await RealAIFeedback(word, userAnswer);
            }
            else
            {
                return DemoFeedback(word, userAnswer);
            }
        }

        /// <summary>
        /// Генерация ежедневного совета
        /// </summary>
        public async Task<string> GenerateDailyTip(User user, int wordsLearned, int streakDays)
        {
            if (_useRealAI)
            {
                return await RealDailyTip(wordsLearned, streakDays);
            }
            else
            {
                return DemoDailyTip(wordsLearned, streakDays);
            }
        }

        /// <summary>
        /// Реальный AI через YandexGPT API
        /// </summary>
        private async Task<string> RealAIFeedback(Word word, string userAnswer)
        {
            try
            {
                var prompt = $@"
Ты - дружелюбный AI-репетитор английского языка.

Студент переводил слово и ошибся.
Слово: '{word.Word1}'
Правильный перевод: '{word.Translation}'
Ответ студента: '{userAnswer}'

Напиши короткий, мотивирующий фидбек (максимум 2 предложения на русском):
1. Укажи на ошибку мягко
2. Дай совет как запомнить правильно
3. Используй эмодзи

Пример хорошего ответа:
❌ Неправильно! 'Apple' переводится как 'Яблоко'. 💡 Совет: представь красное яблоко, когда слышишь это слово!
";

                return await CallYandexGPT(prompt);
            }
            catch (Exception ex)
            {
                return $"❌ Ошибка AI: {ex.Message}. Правильный перевод: {word.Translation}";
            }
        }

        /// <summary>
        /// Реальный AI для совета дня
        /// </summary>
        private async Task<string> RealDailyTip(int wordsLearned, int streakDays)
        {
            try
            {
                var prompt = $@"
Ты - AI-мотиватор в приложении для изучения языков.

Статистика пользователя:
- Выучено слов: {wordsLearned}
- Серия дней подряд: {streakDays}

Напиши короткий персонализированный совет дня (30-50 слов на русском):
- Похвали за прогресс (если есть)
- Дай конкретный совет по изучению языка
- Используй 1-2 эмодзи
- Будь дружелюбным и вдохновляющим
";

                return await CallYandexGPT(prompt);
            }
            catch (Exception ex)
            {
                return DemoDailyTip(wordsLearned, streakDays);
            }
        }

        /// <summary>
        /// Вызов YandexGPT API
        /// </summary>
        private async Task<string> CallYandexGPT(string prompt)
        {
            var requestBody = new
            {
                modelUri = $"gpt://{_folderId}/yandexgpt/latest",
                completionOptions = new
                {
                    temperature = 0.7,
                    maxTokens = 200
                },
                messages = new[]
                {
                    new { role = "user", text = prompt }
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Api-Key {_apiKey}");

            var response = await _httpClient.PostAsync(
                "https://llm.api.cloud.yandex.net/foundationModels/v1/completion",
                content
            );

            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API Error: {jsonResponse}");
            }

            using var doc = JsonDocument.Parse(jsonResponse);
            var result = doc.RootElement
                .GetProperty("result")
                .GetProperty("alternatives")[0]
                .GetProperty("message")
                .GetProperty("text")
                .GetString();

            return result ?? "Продолжай в том же духе! 🚀";
        }

        /// <summary>
        /// Демо-режим (имитация AI)
        /// </summary>
        private string DemoFeedback(Word word, string userAnswer)
        {
            var feedbacks = new[]
            {
                $"📚 '{word.Word1}' переводится как '{word.Translation}'. " +
                $"Твой ответ '{userAnswer}' близок, но правильный вариант: {word.Translation}. " +
                $"💡 Совет: запомни через ассоциацию!",

                $"❌ Неправильно! '{word.Word1}' = {word.Translation}. " +
                $"📖 Пример: {word.ExampleSentence ?? $"Попробуй составить предложение с этим словом"}. " +
                $"🎯 Повтори через час!",

                $"🧠 Ошибка: '{userAnswer}' → правильно '{word.Translation}'. " +
                $"🔊 Произнеси '{word.Word1}' вслух 3 раза - это поможет запомнить!",

                $"💪 Не сдавайся! Правильный перевод: {word.Translation}. " +
                $"✨ Ты обязательно запомнишь это слово!"
            };

            return feedbacks[_random.Next(feedbacks.Length)];
        }

        /// <summary>
        /// Демо-режим для совета дня
        /// </summary>
        private string DemoDailyTip(int wordsLearned, int streakDays)
        {
            if (streakDays > 0 && wordsLearned > 0)
            {
                var progressTips = new[]
                {
                    $"🔥 Серия: {streakDays} дней! Выучено {wordsLearned} слов. Сегодня выучи ещё 5!",
                    $"⭐ Отличный прогресс! {wordsLearned} слов за {streakDays} дней. Продолжай!",
                    $"🎯 Уже {wordsLearned} слов в копилке! Повтори 10 старых слов сегодня."
                };
                return progressTips[_random.Next(progressTips.Length)];
            }

            var beginnerTips = new[]
            {
                "💡 Первые шаги - самые важные! Выучи 5 новых слов сегодня.",
                "🚀 10 минут в день эффективнее, чем час раз в неделю!",
                "📚 Веди словарик с примерами - это ускоряет запоминание.",
                "🎧 Смотри видео на английском с субтитрами.",
                "💪 Не бойся ошибок! Каждая ошибка = шаг к прогрессу."
            };

            return beginnerTips[_random.Next(beginnerTips.Length)];
        }

        /// <summary>
        /// Генерация примера предложения
        /// </summary>
        public async Task<string> GenerateExampleSentence(Word word, User user)
        {
            if (_useRealAI)
            {
                try
                {
                    var prompt = $@"
Составь простое предложение на английском со словом '{word.Word1}'.
Перевод: '{word.Translation}'.
Уровень: beginner.
Формат: 'Предложение. (Перевод)'
";
                    return await CallYandexGPT(prompt);
                }
                catch
                {
                    return DemoExample(word);
                }
            }

            return DemoExample(word);
        }

        private string DemoExample(Word word)
        {
            var examples = new[]
            {
                $"\"This is a {word.Word1}\" — \"Это {word.Translation}\"",
                $"\"I like this {word.Word1}\" — \"Мне нравится этот {word.Translation}\"",
                $"\"My {word.Word1} is good\" — \"Мой {word.Translation} хороший\""
            };
            return examples[_random.Next(examples.Length)];
        }
    }
}