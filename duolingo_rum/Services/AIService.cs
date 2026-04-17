using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using duolingo_rum.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace duolingo_rum.Services
{
    public class AIService
    {
        private readonly HttpClient _httpClient;
        private readonly bool _useRealAI;
        private readonly Random _random = new Random();
        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly string _model;

        public AIService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();

                _apiKey = config["CloudRu:ApiKey"];
                _baseUrl = config["CloudRu:BaseUrl"] ?? "https://foundation-models.api.cloud.ru/v1";
                _model = config["CloudRu:Model"] ?? "GigaChat-Lightning";

                if (!string.IsNullOrEmpty(_apiKey))
                {
                    _useRealAI = true;
                    Debug.WriteLine($"✅ Cloud.ru Service initialized with model: {_model}");
                }
                else
                {
                    _useRealAI = false;
                    Debug.WriteLine("⚠️ Cloud.ru API key not found, using demo mode");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Cloud.ru Service init error: {ex.Message}");
                _useRealAI = false;
                _apiKey = "";
                _baseUrl = "";
                _model = "";
            }
        }

        // --- Основной метод вызова API ---
        private async Task<string?> CallCloudRuAPI(string prompt)
        {
            try
            {
                var requestBody = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7,
                    max_tokens = 1000,
                    stream = false
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
                _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

                var url = $"{_baseUrl}/chat/completions";
                Debug.WriteLine($"Calling Cloud.ru API: {url}");

                var response = await _httpClient.PostAsync(url, content);
                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Cloud.ru API error: {response.StatusCode}");
                    Debug.WriteLine($"Response: {jsonResponse}");
                    return null;
                }

                using var doc = JsonDocument.Parse(jsonResponse);
                var result = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CallCloudRuAPI error: {ex.Message}");
                return null;
            }
        }

        // --- Генерация слов для урока ---
        public async Task<List<GeneratedWord>> GenerateWordsForLesson(string targetLanguage, string nativeLanguage, string difficultyLevel, int count = 10)
        {
            Debug.WriteLine($"GenerateWordsForLesson: lang={targetLanguage}, level={difficultyLevel}, useAI={_useRealAI}");

            if (_useRealAI)
            {
                var difficultyText = difficultyLevel switch
                {
                    "beginner" => "начального уровня",
                    "intermediate" => "среднего уровня",
                    "advanced" => "продвинутого уровня",
                    _ => "начального уровня"
                };

                var prompt = $@"Сгенерируй {count} уникальных и полезных слов для изучения {targetLanguage} языка.
Родной язык студента: {nativeLanguage}. Уровень: {difficultyText}.

Требования:
- Каждое слово должно быть уникальным и практичным для реального общения.
- Используй разные темы (семья, еда, путешествия, работа, природа).
- Используй разные части речи (существительные, глаголы, прилагательные).
- Добавь простое предложение с переводом для каждого слова.

ОТВЕТЬ ТОЛЬКО JSON. БЕЗ ПОЯСНЕНИЙ. Формат:
{{""words"":[
  {{""word"":""пример"",""translation"":""перевод"",""example"":""Пример предложения"",""exampleTranslation"":""Перевод примера""}}
]}}";

                var response = await CallCloudRuAPI(prompt);
                if (!string.IsNullOrEmpty(response))
                {
                    var words = ParseGeneratedWords(response);
                    if (words.Count > 0)
                    {
                        Debug.WriteLine($"✅ Cloud.ru generated {words.Count} words");
                        return words;
                    }
                }
            }

            Debug.WriteLine("Using demo word generation");
            return GenerateDemoWords(targetLanguage, nativeLanguage, difficultyLevel, count);
        }

        // --- Парсинг JSON ответа от AI ---
        private List<GeneratedWord> ParseGeneratedWords(string json)
        {
            var words = new List<GeneratedWord>();
            try
            {
                Debug.WriteLine($"Raw JSON: {json}");
                var cleaned = json.Trim();

                // Убираем markdown обёртки
                if (cleaned.Contains("```json"))
                {
                    int start = cleaned.IndexOf("```json") + 7;
                    int end = cleaned.LastIndexOf("```");
                    if (end > start)
                        cleaned = cleaned.Substring(start, end - start);
                }
                else if (cleaned.Contains("```"))
                {
                    int start = cleaned.IndexOf("```") + 3;
                    int end = cleaned.LastIndexOf("```");
                    if (end > start)
                        cleaned = cleaned.Substring(start, end - start);
                }

                cleaned = cleaned.Trim();

                // Находим JSON объект
                int jsonStart = cleaned.IndexOf('{');
                int jsonEnd = cleaned.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    cleaned = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);
                }

                Debug.WriteLine($"Cleaned JSON: {cleaned}");
                using var doc = JsonDocument.Parse(cleaned);
                var root = doc.RootElement;

                if (root.TryGetProperty("words", out var wordsArray))
                {
                    foreach (var item in wordsArray.EnumerateArray())
                    {
                        var word = new GeneratedWord
                        {
                            Word = item.TryGetProperty("word", out var w) ? w.GetString() ?? "" : "",
                            Translation = item.TryGetProperty("translation", out var t) ? t.GetString() ?? "" : "",
                            ExampleSentence = item.TryGetProperty("example", out var ex) ? ex.GetString() : null,
                            ExampleTranslation = item.TryGetProperty("exampleTranslation", out var et) ? et.GetString() : null,
                        };
                        if (!string.IsNullOrEmpty(word.Word) && !string.IsNullOrEmpty(word.Translation))
                            words.Add(word);
                    }
                }
                Debug.WriteLine($"Parsed {words.Count} words");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ParseGeneratedWords error: {ex.Message}");
                Debug.WriteLine($"Raw JSON was: {json}");
            }
            return words;
        }

        // --- Сохранение сгенерированных слов в БД ---
        public async Task<List<Word>> SaveGeneratedWordsToDatabase(int languageId, List<GeneratedWord> generatedWords)
        {
            var savedWords = new List<Word>();
            try
            {
                using var context = new _43pRumiantsefContext();
                foreach (var genWord in generatedWords)
                {
                    if (string.IsNullOrWhiteSpace(genWord.Word) || string.IsNullOrWhiteSpace(genWord.Translation))
                        continue;

                    var existingWord = await context.Words
                        .FirstOrDefaultAsync(w => w.Word1 == genWord.Word && w.LanguageId == languageId);

                    if (existingWord == null)
                    {
                        var word = new Word
                        {
                            LanguageId = languageId,
                            Word1 = genWord.Word,
                            Translation = genWord.Translation,
                            ExampleSentence = genWord.ExampleSentence,
                            ExampleTranslation = genWord.ExampleTranslation,
                            CreatedAt = DateTime.UtcNow
                        };
                        context.Words.Add(word);
                        savedWords.Add(word);
                    }
                    else
                    {
                        savedWords.Add(existingWord);
                    }
                }
                await context.SaveChangesAsync();
                Debug.WriteLine($"✅ Saved {savedWords.Count} words to database");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveGeneratedWordsToDatabase error: {ex.Message}");
            }
            return savedWords;
        }

        // --- Фидбек при ошибке ---
        public async Task<string> GenerateFeedback(Word word, string userAnswer)
        {
            if (_useRealAI)
            {
                var prompt = $@"Студент ошибся при переводе слова.
Слово: '{word.Word1}'
Правильный перевод: '{word.Translation}'
Ответ студента: '{userAnswer}'

Напиши короткий мотивирующий фидбек на русском языке (максимум 2 предложения):
- мягко укажи на ошибку
- дай конкретный совет как запомнить
- используй 1-2 эмодзи";
                var response = await CallCloudRuAPI(prompt);
                if (!string.IsNullOrEmpty(response)) return response;
            }
            return DemoFeedback(word, userAnswer);
        }

        // --- Похвала при правильном ответе ---
        public async Task<string> GeneratePraise(Word word)
        {
            if (_random.NextDouble() > 0.4) return string.Empty;
            if (_useRealAI)
            {
                var prompt = $@"Студент правильно перевёл слово '{word.Word1}' = '{word.Translation}'.
Напиши очень короткую похвалу на русском языке (1 предложение, 1 эмодзи).";
                var response = await CallCloudRuAPI(prompt);
                if (!string.IsNullOrEmpty(response)) return response;
            }
            return DemoPraise();
        }

        // --- Пример предложения со словом ---
        public async Task<string> GenerateExampleSentence(Word word)
        {
            if (!string.IsNullOrWhiteSpace(word.ExampleSentence))
            {
                var translation = word.ExampleTranslation ?? string.Empty;
                return $"📖 {word.ExampleSentence}\n💬 {translation}";
            }
            if (_useRealAI)
            {
                var prompt = $@"Составь одно простое предложение со словом '{word.Word1}' (перевод: '{word.Translation}').
Формат строго:
EN: <предложение>
RU: <перевод предложения>";
                var response = await CallCloudRuAPI(prompt);
                if (!string.IsNullOrEmpty(response)) return $"📖 {response}";
            }
            return DemoExample(word);
        }

        // --- Ежедневный совет ---
        public async Task<string> GenerateDailyTip(User user, int wordsLearned, int streakDays)
        {
            if (_useRealAI)
            {
                var prompt = $@"Ты — AI-мотиватор приложения для изучения языков.
Статистика: выучено слов: {wordsLearned}, серия: {streakDays} дней, XP: {user.TotalXp ?? 0}.
Напиши персонализированный совет дня на русском языке (30-50 слов, 1-2 эмодзи).";
                var response = await CallCloudRuAPI(prompt);
                if (!string.IsNullOrEmpty(response)) return response;
            }
            return DemoDailyTip(wordsLearned, streakDays);
        }

        // --- AI-анализ слабых мест ---
        public async Task<string> GenerateWeaknessAnalysis(int totalCorrect, int totalWrong, int streak)
        {
            var accuracy = totalCorrect + totalWrong > 0
                ? (int)((double)totalCorrect / (totalCorrect + totalWrong) * 100)
                : 0;
            if (_useRealAI)
            {
                var prompt = $@"Статистика студента:
- Точность: {accuracy}%
- Правильных ответов: {totalCorrect}, ошибок: {totalWrong}
- Серия дней: {streak}

Напиши короткий анализ на русском языке (2-3 предложения, эмодзи): что идёт хорошо и над чем стоит поработать.";
                var response = await CallCloudRuAPI(prompt);
                if (!string.IsNullOrEmpty(response)) return response;
            }
            return DemoWeaknessAnalysis(accuracy);
        }

        // --- Демо-методы (резерв, если AI недоступен) ---
        private List<GeneratedWord> GenerateDemoWords(string targetLanguage, string nativeLanguage, string difficultyLevel, int count)
        {
            var dict = new Dictionary<string, List<GeneratedWord>>();

            // АНГЛИЙСКИЙ (en)
            dict["en"] = new List<GeneratedWord>
    {
        new() { Word = "hello", Translation = "привет", ExampleSentence = "Hello, how are you?", ExampleTranslation = "Привет, как дела?" },
        new() { Word = "goodbye", Translation = "до свидания", ExampleSentence = "Goodbye, see you later", ExampleTranslation = "До свидания, увидимся позже" },
        new() { Word = "thank you", Translation = "спасибо", ExampleSentence = "Thank you for your help", ExampleTranslation = "Спасибо за помощь" },
        new() { Word = "please", Translation = "пожалуйста", ExampleSentence = "Please open the door", ExampleTranslation = "Пожалуйста, открой дверь" },
        new() { Word = "sorry", Translation = "извините", ExampleSentence = "I am sorry", ExampleTranslation = "Извините" },
        new() { Word = "yes", Translation = "да", ExampleSentence = "Yes, I agree", ExampleTranslation = "Да, я согласен" },
        new() { Word = "no", Translation = "нет", ExampleSentence = "No, thank you", ExampleTranslation = "Нет, спасибо" },
        new() { Word = "friend", Translation = "друг", ExampleSentence = "You are my friend", ExampleTranslation = "Ты мой друг" },
        new() { Word = "family", Translation = "семья", ExampleSentence = "My family is big", ExampleTranslation = "Моя семья большая" },
        new() { Word = "house", Translation = "дом", ExampleSentence = "I live in a house", ExampleTranslation = "Я живу в доме" },
        new() { Word = "car", Translation = "машина", ExampleSentence = "My car is red", ExampleTranslation = "Моя машина красная" },
        new() { Word = "dog", Translation = "собака", ExampleSentence = "I have a dog", ExampleTranslation = "У меня есть собака" },
        new() { Word = "cat", Translation = "кот", ExampleSentence = "The cat sleeps", ExampleTranslation = "Кот спит" },
        new() { Word = "work", Translation = "работа", ExampleSentence = "I go to work", ExampleTranslation = "Я иду на работу" },
        new() { Word = "study", Translation = "учиться", ExampleSentence = "I study English", ExampleTranslation = "Я учу английский" },
        new() { Word = "eat", Translation = "есть", ExampleSentence = "I eat an apple", ExampleTranslation = "Я ем яблоко" },
        new() { Word = "drink", Translation = "пить", ExampleSentence = "I drink water", ExampleTranslation = "Я пью воду" },
        new() { Word = "sleep", Translation = "спать", ExampleSentence = "I sleep at night", ExampleTranslation = "Я сплю ночью" },
        new() { Word = "run", Translation = "бежать", ExampleSentence = "I run fast", ExampleTranslation = "Я бегаю быстро" },
        new() { Word = "read", Translation = "читать", ExampleSentence = "I read a book", ExampleTranslation = "Я читаю книгу" },
        new() { Word = "write", Translation = "писать", ExampleSentence = "I write a letter", ExampleTranslation = "Я пишу письмо" },
        new() { Word = "happy", Translation = "счастливый", ExampleSentence = "I am happy", ExampleTranslation = "Я счастлив" },
        new() { Word = "sad", Translation = "грустный", ExampleSentence = "He is sad", ExampleTranslation = "Он грустный" },
        new() { Word = "big", Translation = "большой", ExampleSentence = "The house is big", ExampleTranslation = "Дом большой" },
        new() { Word = "small", Translation = "маленький", ExampleSentence = "The cat is small", ExampleTranslation = "Кот маленький" },
        new() { Word = "new", Translation = "новый", ExampleSentence = "I have a new phone", ExampleTranslation = "У меня новый телефон" },
        new() { Word = "old", Translation = "старый", ExampleSentence = "This car is old", ExampleTranslation = "Эта машина старая" },
        new() { Word = "good", Translation = "хороший", ExampleSentence = "It is good", ExampleTranslation = "Это хорошо" },
        new() { Word = "bad", Translation = "плохой", ExampleSentence = "That is bad", ExampleTranslation = "Это плохо" },
        new() { Word = "beautiful", Translation = "красивый", ExampleSentence = "The flower is beautiful", ExampleTranslation = "Цветок красивый" },
        new() { Word = "ugly", Translation = "уродливый", ExampleSentence = "The monster is ugly", ExampleTranslation = "Монстр уродливый" },
        new() { Word = "cheap", Translation = "дешёвый", ExampleSentence = "This shirt is cheap", ExampleTranslation = "Эта рубашка дешёвая" },
        new() { Word = "expensive", Translation = "дорогой", ExampleSentence = "This watch is expensive", ExampleTranslation = "Эти часы дорогие" },
        new() { Word = "fast", Translation = "быстрый", ExampleSentence = "The train is fast", ExampleTranslation = "Поезд быстрый" },
        new() { Word = "slow", Translation = "медленный", ExampleSentence = "The turtle is slow", ExampleTranslation = "Черепаха медленная" },
        new() { Word = "hot", Translation = "горячий", ExampleSentence = "The soup is hot", ExampleTranslation = "Суп горячий" },
        new() { Word = "cold", Translation = "холодный", ExampleSentence = "The ice cream is cold", ExampleTranslation = "Мороженое холодное" },
        new() { Word = "easy", Translation = "лёгкий", ExampleSentence = "The test is easy", ExampleTranslation = "Тест лёгкий" },
        new() { Word = "difficult", Translation = "трудный", ExampleSentence = "The exam is difficult", ExampleTranslation = "Экзамен трудный" },
        new() { Word = "early", Translation = "рано", ExampleSentence = "I wake up early", ExampleTranslation = "Я встаю рано" },
        new() { Word = "late", Translation = "поздно", ExampleSentence = "I go to bed late", ExampleTranslation = "Я ложусь поздно" },
        new() { Word = "often", Translation = "часто", ExampleSentence = "I often travel", ExampleTranslation = "Я часто путешествую" },
        new() { Word = "never", Translation = "никогда", ExampleSentence = "I never smoke", ExampleTranslation = "Я никогда не курю" },
        new() { Word = "always", Translation = "всегда", ExampleSentence = "I always brush my teeth", ExampleTranslation = "Я всегда чищу зубы" },
        new() { Word = "sometimes", Translation = "иногда", ExampleSentence = "I sometimes watch TV", ExampleTranslation = "Я иногда смотрю телевизор" },
        new() { Word = "today", Translation = "сегодня", ExampleSentence = "Today is Monday", ExampleTranslation = "Сегодня понедельник" },
        new() { Word = "tomorrow", Translation = "завтра", ExampleSentence = "See you tomorrow", ExampleTranslation = "Увидимся завтра" },
        new() { Word = "yesterday", Translation = "вчера", ExampleSentence = "I was at home yesterday", ExampleTranslation = "Вчера я был дома" },
        new() { Word = "morning", Translation = "утро", ExampleSentence = "Good morning", ExampleTranslation = "Доброе утро" },
        new() { Word = "evening", Translation = "вечер", ExampleSentence = "Good evening", ExampleTranslation = "Добрый вечер" },
        new() { Word = "night", Translation = "ночь", ExampleSentence = "Good night", ExampleTranslation = "Спокойной ночи" },
    };

            // ИСПАНСКИЙ (es)
            dict["es"] = new List<GeneratedWord>
    {
        new() { Word = "hola", Translation = "привет", ExampleSentence = "Hola, ¿cómo estás?", ExampleTranslation = "Привет, как дела?" },
        new() { Word = "adiós", Translation = "до свидания", ExampleSentence = "Adiós, hasta luego", ExampleTranslation = "До свидания, увидимся" },
        new() { Word = "gracias", Translation = "спасибо", ExampleSentence = "Muchas gracias", ExampleTranslation = "Большое спасибо" },
        new() { Word = "por favor", Translation = "пожалуйста", ExampleSentence = "Por favor, ayuda", ExampleTranslation = "Пожалуйста, помоги" },
        new() { Word = "lo siento", Translation = "извините", ExampleSentence = "Lo siento, fue mi culpa", ExampleTranslation = "Извините, это была моя вина" },
        new() { Word = "sí", Translation = "да", ExampleSentence = "Sí, quiero", ExampleTranslation = "Да, я хочу" },
        new() { Word = "no", Translation = "нет", ExampleSentence = "No, gracias", ExampleTranslation = "Нет, спасибо" },
        new() { Word = "amigo", Translation = "друг", ExampleSentence = "Eres mi amigo", ExampleTranslation = "Ты мой друг" },
        new() { Word = "familia", Translation = "семья", ExampleSentence = "Mi familia es grande", ExampleTranslation = "Моя семья большая" },
        new() { Word = "casa", Translation = "дом", ExampleSentence = "Voy a casa", ExampleTranslation = "Я иду домой" },
        new() { Word = "coche", Translation = "машина", ExampleSentence = "Mi coche es rojo", ExampleTranslation = "Моя машина красная" },
        new() { Word = "perro", Translation = "собака", ExampleSentence = "Tengo un perro", ExampleTranslation = "У меня есть собака" },
        new() { Word = "gato", Translation = "кот", ExampleSentence = "El gato duerme", ExampleTranslation = "Кот спит" },
        new() { Word = "trabajo", Translation = "работа", ExampleSentence = "Voy al trabajo", ExampleTranslation = "Я иду на работу" },
        new() { Word = "estudiar", Translation = "учиться", ExampleSentence = "Estudio español", ExampleTranslation = "Я учу испанский" },
        new() { Word = "comer", Translation = "есть", ExampleSentence = "Como una manzana", ExampleTranslation = "Я ем яблоко" },
        new() { Word = "beber", Translation = "пить", ExampleSentence = "Bebo agua", ExampleTranslation = "Я пью воду" },
        new() { Word = "dormir", Translation = "спать", ExampleSentence = "Duermo por la noche", ExampleTranslation = "Я сплю ночью" },
        new() { Word = "correr", Translation = "бежать", ExampleSentence = "Corro rápido", ExampleTranslation = "Я бегаю быстро" },
        new() { Word = "leer", Translation = "читать", ExampleSentence = "Leo un libro", ExampleTranslation = "Я читаю книгу" },
        new() { Word = "escribir", Translation = "писать", ExampleSentence = "Escribo una carta", ExampleTranslation = "Я пишу письмо" },
        new() { Word = "feliz", Translation = "счастливый", ExampleSentence = "Estoy feliz", ExampleTranslation = "Я счастлив" },
        new() { Word = "triste", Translation = "грустный", ExampleSentence = "Él está triste", ExampleTranslation = "Он грустный" },
        new() { Word = "grande", Translation = "большой", ExampleSentence = "La casa es grande", ExampleTranslation = "Дом большой" },
        new() { Word = "pequeño", Translation = "маленький", ExampleSentence = "El gato es pequeño", ExampleTranslation = "Кот маленький" },
        new() { Word = "nuevo", Translation = "новый", ExampleSentence = "Tengo un teléfono nuevo", ExampleTranslation = "У меня новый телефон" },
        new() { Word = "viejo", Translation = "старый", ExampleSentence = "Este coche es viejo", ExampleTranslation = "Эта машина старая" },
        new() { Word = "bueno", Translation = "хороший", ExampleSentence = "Está bien", ExampleTranslation = "Это хорошо" },
        new() { Word = "malo", Translation = "плохой", ExampleSentence = "Eso es malo", ExampleTranslation = "Это плохо" },
        new() { Word = "bonito", Translation = "красивый", ExampleSentence = "La flor es bonita", ExampleTranslation = "Цветок красивый" },
        new() { Word = "feo", Translation = "уродливый", ExampleSentence = "El monstruo es feo", ExampleTranslation = "Монстр уродливый" },
        new() { Word = "barato", Translation = "дешёвый", ExampleSentence = "Esta camisa es barata", ExampleTranslation = "Эта рубашка дешёвая" },
        new() { Word = "caro", Translation = "дорогой", ExampleSentence = "Este reloj es caro", ExampleTranslation = "Эти часы дорогие" },
        new() { Word = "rápido", Translation = "быстрый", ExampleSentence = "El tren es rápido", ExampleTranslation = "Поезд быстрый" },
        new() { Word = "lento", Translation = "медленный", ExampleSentence = "La tortuga es lenta", ExampleTranslation = "Черепаха медленная" },
        new() { Word = "caliente", Translation = "горячий", ExampleSentence = "La sopa está caliente", ExampleTranslation = "Суп горячий" },
        new() { Word = "frío", Translation = "холодный", ExampleSentence = "El helado está frío", ExampleTranslation = "Мороженое холодное" },
        new() { Word = "fácil", Translation = "лёгкий", ExampleSentence = "El examen es fácil", ExampleTranslation = "Экзамен лёгкий" },
        new() { Word = "difícil", Translation = "трудный", ExampleSentence = "El examen es difícil", ExampleTranslation = "Экзамен трудный" },
        new() { Word = "temprano", Translation = "рано", ExampleSentence = "Me levanto temprano", ExampleTranslation = "Я встаю рано" },
        new() { Word = "tarde", Translation = "поздно", ExampleSentence = "Me acuesto tarde", ExampleTranslation = "Я ложусь поздно" },
        new() { Word = "a menudo", Translation = "часто", ExampleSentence = "A menudo viajo", ExampleTranslation = "Я часто путешествую" },
        new() { Word = "nunca", Translation = "никогда", ExampleSentence = "Nunca fumo", ExampleTranslation = "Я никогда не курю" },
        new() { Word = "siempre", Translation = "всегда", ExampleSentence = "Siempre me lavo los dientes", ExampleTranslation = "Я всегда чищу зубы" },
        new() { Word = "a veces", Translation = "иногда", ExampleSentence = "A veces veo la tele", ExampleTranslation = "Я иногда смотрю телевизор" },
        new() { Word = "hoy", Translation = "сегодня", ExampleSentence = "Hoy es lunes", ExampleTranslation = "Сегодня понедельник" },
        new() { Word = "mañana", Translation = "завтра", ExampleSentence = "Hasta mañana", ExampleTranslation = "До завтра" },
        new() { Word = "ayer", Translation = "вчера", ExampleSentence = "Ayer estuve en casa", ExampleTranslation = "Вчера я был дома" },
        new() { Word = "mañana", Translation = "утро", ExampleSentence = "Buenos días", ExampleTranslation = "Доброе утро" },
        new() { Word = "tarde", Translation = "вечер", ExampleSentence = "Buenas tardes", ExampleTranslation = "Добрый вечер" },
        new() { Word = "noche", Translation = "ночь", ExampleSentence = "Buenas noches", ExampleTranslation = "Спокойной ночи" },
    };

            // ФРАНЦУЗСКИЙ (fr)
            dict["fr"] = new List<GeneratedWord>
    {
        new() { Word = "bonjour", Translation = "здравствуйте", ExampleSentence = "Bonjour, comment allez-vous?", ExampleTranslation = "Здравствуйте, как поживаете?" },
        new() { Word = "au revoir", Translation = "до свидания", ExampleSentence = "Au revoir, à demain", ExampleTranslation = "До свидания, до завтра" },
        new() { Word = "merci", Translation = "спасибо", ExampleSentence = "Merci beaucoup", ExampleTranslation = "Большое спасибо" },
        new() { Word = "s'il vous plaît", Translation = "пожалуйста", ExampleSentence = "S'il vous plaît, aidez-moi", ExampleTranslation = "Пожалуйста, помогите мне" },
        new() { Word = "désolé", Translation = "извините", ExampleSentence = "Je suis désolé", ExampleTranslation = "Я извиняюсь" },
        new() { Word = "oui", Translation = "да", ExampleSentence = "Oui, je veux", ExampleTranslation = "Да, я хочу" },
        new() { Word = "non", Translation = "нет", ExampleSentence = "Non, merci", ExampleTranslation = "Нет, спасибо" },
        new() { Word = "ami", Translation = "друг", ExampleSentence = "Tu es mon ami", ExampleTranslation = "Ты мой друг" },
        new() { Word = "famille", Translation = "семья", ExampleSentence = "Ma famille est grande", ExampleTranslation = "Моя семья большая" },
        new() { Word = "maison", Translation = "дом", ExampleSentence = "Je vais à la maison", ExampleTranslation = "Я иду домой" },
        new() { Word = "voiture", Translation = "машина", ExampleSentence = "Ma voiture est rouge", ExampleTranslation = "Моя машина красная" },
        new() { Word = "chien", Translation = "собака", ExampleSentence = "J'ai un chien", ExampleTranslation = "У меня есть собака" },
        new() { Word = "chat", Translation = "кот", ExampleSentence = "Le chat dort", ExampleTranslation = "Кот спит" },
        new() { Word = "travail", Translation = "работа", ExampleSentence = "Je vais au travail", ExampleTranslation = "Я иду на работу" },
        new() { Word = "étudier", Translation = "учиться", ExampleSentence = "J'étudie le français", ExampleTranslation = "Я учу французский" },
        new() { Word = "manger", Translation = "есть", ExampleSentence = "Je mange une pomme", ExampleTranslation = "Я ем яблоко" },
        new() { Word = "boire", Translation = "пить", ExampleSentence = "Je bois de l'eau", ExampleTranslation = "Я пью воду" },
        new() { Word = "dormir", Translation = "спать", ExampleSentence = "Je dors la nuit", ExampleTranslation = "Я сплю ночью" },
        new() { Word = "courir", Translation = "бежать", ExampleSentence = "Je cours vite", ExampleTranslation = "Я бегаю быстро" },
        new() { Word = "lire", Translation = "читать", ExampleSentence = "Je lis un livre", ExampleTranslation = "Я читаю книгу" },
        new() { Word = "écrire", Translation = "писать", ExampleSentence = "J'écris une lettre", ExampleTranslation = "Я пишу письмо" },
        new() { Word = "heureux", Translation = "счастливый", ExampleSentence = "Je suis heureux", ExampleTranslation = "Я счастлив" },
        new() { Word = "triste", Translation = "грустный", ExampleSentence = "Il est triste", ExampleTranslation = "Он грустный" },
        new() { Word = "grand", Translation = "большой", ExampleSentence = "La maison est grande", ExampleTranslation = "Дом большой" },
        new() { Word = "petit", Translation = "маленький", ExampleSentence = "Le chat est petit", ExampleTranslation = "Кот маленький" },
        new() { Word = "nouveau", Translation = "новый", ExampleSentence = "J'ai un nouveau téléphone", ExampleTranslation = "У меня новый телефон" },
        new() { Word = "vieux", Translation = "старый", ExampleSentence = "Cette voiture est vieille", ExampleTranslation = "Эта машина старая" },
        new() { Word = "bon", Translation = "хороший", ExampleSentence = "C'est bon", ExampleTranslation = "Это хорошо" },
        new() { Word = "mauvais", Translation = "плохой", ExampleSentence = "C'est mauvais", ExampleTranslation = "Это плохо" },
        new() { Word = "beau", Translation = "красивый", ExampleSentence = "La fleur est belle", ExampleTranslation = "Цветок красивый" },
        new() { Word = "laid", Translation = "уродливый", ExampleSentence = "Le monstre est laid", ExampleTranslation = "Монстр уродливый" },
        new() { Word = "bon marché", Translation = "дешёвый", ExampleSentence = "Cette chemise est bon marché", ExampleTranslation = "Эта рубашка дешёвая" },
        new() { Word = "cher", Translation = "дорогой", ExampleSentence = "Cette montre est chère", ExampleTranslation = "Эти часы дорогие" },
        new() { Word = "rapide", Translation = "быстрый", ExampleSentence = "Le train est rapide", ExampleTranslation = "Поезд быстрый" },
        new() { Word = "lent", Translation = "медленный", ExampleSentence = "La tortue est lente", ExampleTranslation = "Черепаха медленная" },
        new() { Word = "chaud", Translation = "горячий", ExampleSentence = "La soupe est chaude", ExampleTranslation = "Суп горячий" },
        new() { Word = "froid", Translation = "холодный", ExampleSentence = "La glace est froide", ExampleTranslation = "Мороженое холодное" },
        new() { Word = "facile", Translation = "лёгкий", ExampleSentence = "L'examen est facile", ExampleTranslation = "Экзамен лёгкий" },
        new() { Word = "difficile", Translation = "трудный", ExampleSentence = "L'examen est difficile", ExampleTranslation = "Экзамен трудный" },
        new() { Word = "tôt", Translation = "рано", ExampleSentence = "Je me lève tôt", ExampleTranslation = "Я встаю рано" },
        new() { Word = "tard", Translation = "поздно", ExampleSentence = "Je me couche tard", ExampleTranslation = "Я ложусь поздно" },
        new() { Word = "souvent", Translation = "часто", ExampleSentence = "Je voyage souvent", ExampleTranslation = "Я часто путешествую" },
        new() { Word = "jamais", Translation = "никогда", ExampleSentence = "Je ne fume jamais", ExampleTranslation = "Я никогда не курю" },
        new() { Word = "toujours", Translation = "всегда", ExampleSentence = "Je me brosse toujours les dents", ExampleTranslation = "Я всегда чищу зубы" },
        new() { Word = "parfois", Translation = "иногда", ExampleSentence = "Je regarde parfois la télé", ExampleTranslation = "Я иногда смотрю телевизор" },
        new() { Word = "aujourd'hui", Translation = "сегодня", ExampleSentence = "Aujourd'hui c'est lundi", ExampleTranslation = "Сегодня понедельник" },
        new() { Word = "demain", Translation = "завтра", ExampleSentence = "À demain", ExampleTranslation = "До завтра" },
        new() { Word = "hier", Translation = "вчера", ExampleSentence = "Hier j'étais à la maison", ExampleTranslation = "Вчера я был дома" },
        new() { Word = "matin", Translation = "утро", ExampleSentence = "Bonjour", ExampleTranslation = "Доброе утро" },
        new() { Word = "soir", Translation = "вечер", ExampleSentence = "Bonsoir", ExampleTranslation = "Добрый вечер" },
        new() { Word = "nuit", Translation = "ночь", ExampleSentence = "Bonne nuit", ExampleTranslation = "Спокойной ночи" },
    };

            // НЕМЕЦКИЙ (de)
            dict["de"] = new List<GeneratedWord>
    {
        new() { Word = "hallo", Translation = "привет", ExampleSentence = "Hallo, wie geht es dir?", ExampleTranslation = "Привет, как дела?" },
        new() { Word = "auf Wiedersehen", Translation = "до свидания", ExampleSentence = "Auf Wiedersehen, bis morgen", ExampleTranslation = "До свидания, до завтра" },
        new() { Word = "danke", Translation = "спасибо", ExampleSentence = "Danke schön", ExampleTranslation = "Большое спасибо" },
        new() { Word = "bitte", Translation = "пожалуйста", ExampleSentence = "Bitte hilf mir", ExampleTranslation = "Пожалуйста, hilf mir" },
        new() { Word = "Entschuldigung", Translation = "извините", ExampleSentence = "Entschuldigung, das war mein Fehler", ExampleTranslation = "Извините, это была моя ошибка" },
        new() { Word = "ja", Translation = "да", ExampleSentence = "Ja, ich will", ExampleTranslation = "Да, я хочу" },
        new() { Word = "nein", Translation = "нет", ExampleSentence = "Nein, danke", ExampleTranslation = "Нет, спасибо" },
        new() { Word = "Freund", Translation = "друг", ExampleSentence = "Du bist mein Freund", ExampleTranslation = "Ты мой друг" },
        new() { Word = "Familie", Translation = "семья", ExampleSentence = "Meine Familie ist groß", ExampleTranslation = "Моя семья большая" },
        new() { Word = "Haus", Translation = "дом", ExampleSentence = "Ich gehe nach Hause", ExampleTranslation = "Я иду домой" },
        new() { Word = "Auto", Translation = "машина", ExampleSentence = "Mein Auto ist rot", ExampleTranslation = "Моя машина красная" },
        new() { Word = "Hund", Translation = "собака", ExampleSentence = "Ich habe einen Hund", ExampleTranslation = "У меня есть собака" },
        new() { Word = "Katze", Translation = "кот", ExampleSentence = "Die Katze schläft", ExampleTranslation = "Кот спит" },
        new() { Word = "Arbeit", Translation = "работа", ExampleSentence = "Ich gehe zur Arbeit", ExampleTranslation = "Я иду на работу" },
        new() { Word = "lernen", Translation = "учиться", ExampleSentence = "Ich lerne Deutsch", ExampleTranslation = "Я учу немецкий" },
        new() { Word = "essen", Translation = "есть", ExampleSentence = "Ich esse einen Apfel", ExampleTranslation = "Я ем яблоко" },
        new() { Word = "trinken", Translation = "пить", ExampleSentence = "Ich trinke Wasser", ExampleTranslation = "Я пью воду" },
        new() { Word = "schlafen", Translation = "спать", ExampleSentence = "Ich schlafe nachts", ExampleTranslation = "Я сплю ночью" },
        new() { Word = "rennen", Translation = "бежать", ExampleSentence = "Ich renne schnell", ExampleTranslation = "Я бегаю быстро" },
        new() { Word = "lesen", Translation = "читать", ExampleSentence = "Ich lese ein Buch", ExampleTranslation = "Я читаю книгу" },
        new() { Word = "schreiben", Translation = "писать", ExampleSentence = "Ich schreibe einen Brief", ExampleTranslation = "Я пишу письмо" },
        new() { Word = "glücklich", Translation = "счастливый", ExampleSentence = "Ich bin glücklich", ExampleTranslation = "Я счастлив" },
        new() { Word = "traurig", Translation = "грустный", ExampleSentence = "Er ist traurig", ExampleTranslation = "Он грустный" },
        new() { Word = "groß", Translation = "большой", ExampleSentence = "Das Haus ist groß", ExampleTranslation = "Дом большой" },
        new() { Word = "klein", Translation = "маленький", ExampleSentence = "Die Katze ist klein", ExampleTranslation = "Кот маленький" },
        new() { Word = "neu", Translation = "новый", ExampleSentence = "Ich habe ein neues Telefon", ExampleTranslation = "У меня новый телефон" },
        new() { Word = "alt", Translation = "старый", ExampleSentence = "Dieses Auto ist alt", ExampleTranslation = "Эта машина старая" },
        new() { Word = "gut", Translation = "хороший", ExampleSentence = "Das ist gut", ExampleTranslation = "Это хорошо" },
        new() { Word = "schlecht", Translation = "плохой", ExampleSentence = "Das ist schlecht", ExampleTranslation = "Это плохо" },
        new() { Word = "schön", Translation = "красивый", ExampleSentence = "Die Blume ist schön", ExampleTranslation = "Цветок красивый" },
        new() { Word = "hässlich", Translation = "уродливый", ExampleSentence = "Das Monster ist hässlich", ExampleTranslation = "Монстр уродливый" },
        new() { Word = "billig", Translation = "дешёвый", ExampleSentence = "Dieses Hemd ist billig", ExampleTranslation = "Эта рубашка дешёвая" },
        new() { Word = "teuer", Translation = "дорогой", ExampleSentence = "Diese Uhr ist teuer", ExampleTranslation = "Эти часы дорогие" },
        new() { Word = "schnell", Translation = "быстрый", ExampleSentence = "Der Zug ist schnell", ExampleTranslation = "Поезд быстрый" },
        new() { Word = "langsam", Translation = "медленный", ExampleSentence = "Die Schildkröte ist langsam", ExampleTranslation = "Черепаха медленная" },
        new() { Word = "heiß", Translation = "горячий", ExampleSentence = "Die Suppe ist heiß", ExampleTranslation = "Суп горячий" },
        new() { Word = "kalt", Translation = "холодный", ExampleSentence = "Das Eis ist kalt", ExampleTranslation = "Мороженое холодное" },
        new() { Word = "einfach", Translation = "лёгкий", ExampleSentence = "Der Test ist einfach", ExampleTranslation = "Тест лёгкий" },
        new() { Word = "schwierig", Translation = "трудный", ExampleSentence = "Die Prüfung ist schwierig", ExampleTranslation = "Экзамен трудный" },
        new() { Word = "früh", Translation = "рано", ExampleSentence = "Ich stehe früh auf", ExampleTranslation = "Я встаю рано" },
        new() { Word = "spät", Translation = "поздно", ExampleSentence = "Ich gehe spät ins Bett", ExampleTranslation = "Я ложусь поздно" },
        new() { Word = "oft", Translation = "часто", ExampleSentence = "Ich reise oft", ExampleTranslation = "Я часто путешествую" },
        new() { Word = "nie", Translation = "никогда", ExampleSentence = "Ich rauche nie", ExampleTranslation = "Я никогда не курю" },
        new() { Word = "immer", Translation = "всегда", ExampleSentence = "Ich putze mir immer die Zähne", ExampleTranslation = "Я всегда чищу зубы" },
        new() { Word = "manchmal", Translation = "иногда", ExampleSentence = "Ich sehe manchmal fern", ExampleTranslation = "Я иногда смотрю телевизор" },
        new() { Word = "heute", Translation = "сегодня", ExampleSentence = "Heute ist Montag", ExampleTranslation = "Сегодня понедельник" },
        new() { Word = "morgen", Translation = "завтра", ExampleSentence = "Bis morgen", ExampleTranslation = "До завтра" },
        new() { Word = "gestern", Translation = "вчера", ExampleSentence = "Gestern war ich zu Hause", ExampleTranslation = "Вчера я был дома" },
        new() { Word = "Morgen", Translation = "утро", ExampleSentence = "Guten Morgen", ExampleTranslation = "Доброе утро" },
        new() { Word = "Abend", Translation = "вечер", ExampleSentence = "Guten Abend", ExampleTranslation = "Добрый вечер" },
        new() { Word = "Nacht", Translation = "ночь", ExampleSentence = "Gute Nacht", ExampleTranslation = "Спокойной ночи" },
    };

            // ИТАЛЬЯНСКИЙ (it)
            dict["it"] = new List<GeneratedWord>
    {
        new() { Word = "ciao", Translation = "привет", ExampleSentence = "Ciao, come stai?", ExampleTranslation = "Привет, как дела?" },
        new() { Word = "arrivederci", Translation = "до свидания", ExampleSentence = "Arrivederci, a domani", ExampleTranslation = "До свидания, до завтра" },
        new() { Word = "grazie", Translation = "спасибо", ExampleSentence = "Grazie mille", ExampleTranslation = "Большое спасибо" },
        new() { Word = "per favore", Translation = "пожалуйста", ExampleSentence = "Per favore, aiutami", ExampleTranslation = "Пожалуйста, помоги мне" },
        new() { Word = "mi dispiace", Translation = "извините", ExampleSentence = "Mi dispiace, è stato colpa mia", ExampleTranslation = "Извините, это была моя вина" },
        new() { Word = "sì", Translation = "да", ExampleSentence = "Sì, voglio", ExampleTranslation = "Да, я хочу" },
        new() { Word = "no", Translation = "нет", ExampleSentence = "No, grazie", ExampleTranslation = "Нет, спасибо" },
        new() { Word = "amico", Translation = "друг", ExampleSentence = "Tu sei mio amico", ExampleTranslation = "Ты мой друг" },
        new() { Word = "famiglia", Translation = "семья", ExampleSentence = "La mia famiglia è grande", ExampleTranslation = "Моя семья большая" },
        new() { Word = "casa", Translation = "дом", ExampleSentence = "Vado a casa", ExampleTranslation = "Я иду домой" },
        new() { Word = "macchina", Translation = "машина", ExampleSentence = "La mia macchina è rossa", ExampleTranslation = "Моя машина красная" },
        new() { Word = "cane", Translation = "собака", ExampleSentence = "Ho un cane", ExampleTranslation = "У меня есть собака" },
        new() { Word = "gatto", Translation = "кот", ExampleSentence = "Il gatto dorme", ExampleTranslation = "Кот спит" },
        new() { Word = "lavoro", Translation = "работа", ExampleSentence = "Vado al lavoro", ExampleTranslation = "Я иду на работу" },
        new() { Word = "studiare", Translation = "учиться", ExampleSentence = "Studio italiano", ExampleTranslation = "Я учу итальянский" },
        new() { Word = "mangiare", Translation = "есть", ExampleSentence = "Mangio una mela", ExampleTranslation = "Я ем яблоко" },
        new() { Word = "bere", Translation = "пить", ExampleSentence = "Bevo acqua", ExampleTranslation = "Я пью воду" },
        new() { Word = "dormire", Translation = "спать", ExampleSentence = "Dormo di notte", ExampleTranslation = "Я сплю ночью" },
        new() { Word = "correre", Translation = "бежать", ExampleSentence = "Corro veloce", ExampleTranslation = "Я бегаю быстро" },
        new() { Word = "leggere", Translation = "читать", ExampleSentence = "Leggo un libro", ExampleTranslation = "Я читаю книгу" },
        new() { Word = "scrivere", Translation = "писать", ExampleSentence = "Scrivo una lettera", ExampleTranslation = "Я пишу письмо" },
        new() { Word = "felice", Translation = "счастливый", ExampleSentence = "Sono felice", ExampleTranslation = "Я счастлив" },
        new() { Word = "triste", Translation = "грустный", ExampleSentence = "Lui è triste", ExampleTranslation = "Он грустный" },
        new() { Word = "grande", Translation = "большой", ExampleSentence = "La casa è grande", ExampleTranslation = "Дом большой" },
        new() { Word = "piccolo", Translation = "маленький", ExampleSentence = "Il gatto è piccolo", ExampleTranslation = "Кот маленький" },
        new() { Word = "nuovo", Translation = "новый", ExampleSentence = "Ho un nuovo telefono", ExampleTranslation = "У меня новый телефон" },
        new() { Word = "vecchio", Translation = "старый", ExampleSentence = "Questa macchina è vecchia", ExampleTranslation = "Эта машина старая" },
        new() { Word = "buono", Translation = "хороший", ExampleSentence = "Va bene", ExampleTranslation = "Это хорошо" },
        new() { Word = "cattivo", Translation = "плохой", ExampleSentence = "È brutto", ExampleTranslation = "Это плохо" },
        new() { Word = "bello", Translation = "красивый", ExampleSentence = "Il fiore è bello", ExampleTranslation = "Цветок красивый" },
        new() { Word = "brutto", Translation = "уродливый", ExampleSentence = "Il mostro è brutto", ExampleTranslation = "Монстр уродливый" },
        new() { Word = "economico", Translation = "дешёвый", ExampleSentence = "Questa camicia è economica", ExampleTranslation = "Эта рубашка дешёвая" },
        new() { Word = "caro", Translation = "дорогой", ExampleSentence = "Questo orologio è caro", ExampleTranslation = "Эти часы дорогие" },
        new() { Word = "veloce", Translation = "быстрый", ExampleSentence = "Il treno è veloce", ExampleTranslation = "Поезд быстрый" },
        new() { Word = "lento", Translation = "медленный", ExampleSentence = "La tartaruga è lenta", ExampleTranslation = "Черепаха медленная" },
        new() { Word = "caldo", Translation = "горячий", ExampleSentence = "La zuppa è calda", ExampleTranslation = "Суп горячий" },
        new() { Word = "freddo", Translation = "холодный", ExampleSentence = "Il gelato è freddo", ExampleTranslation = "Мороженое холодное" },
        new() { Word = "facile", Translation = "лёгкий", ExampleSentence = "L'esame è facile", ExampleTranslation = "Экзамен лёгкий" },
        new() { Word = "difficile", Translation = "трудный", ExampleSentence = "L'esame è difficile", ExampleTranslation = "Экзамен трудный" },
        new() { Word = "presto", Translation = "рано", ExampleSentence = "Mi alzo presto", ExampleTranslation = "Я встаю рано" },
        new() { Word = "tardi", Translation = "поздно", ExampleSentence = "Vado a letto tardi", ExampleTranslation = "Я ложусь поздно" },
        new() { Word = "spesso", Translation = "часто", ExampleSentence = "Viaggio spesso", ExampleTranslation = "Я часто путешествую" },
        new() { Word = "mai", Translation = "никогда", ExampleSentence = "Non fumo mai", ExampleTranslation = "Я никогда не курю" },
        new() { Word = "sempre", Translation = "всегда", ExampleSentence = "Mi lavo sempre i denti", ExampleTranslation = "Я всегда чищу зубы" },
        new() { Word = "a volte", Translation = "иногда", ExampleSentence = "A volte guardo la TV", ExampleTranslation = "Я иногда смотрю телевизор" },
        new() { Word = "oggi", Translation = "сегодня", ExampleSentence = "Oggi è lunedì", ExampleTranslation = "Сегодня понедельник" },
        new() { Word = "domani", Translation = "завтра", ExampleSentence = "A domani", ExampleTranslation = "До завтра" },
        new() { Word = "ieri", Translation = "вчера", ExampleSentence = "Ieri ero a casa", ExampleTranslation = "Вчера я был дома" },
        new() { Word = "mattina", Translation = "утро", ExampleSentence = "Buongiorno", ExampleTranslation = "Доброе утро" },
        new() { Word = "sera", Translation = "вечер", ExampleSentence = "Buonasera", ExampleTranslation = "Добрый вечер" },
        new() { Word = "notte", Translation = "ночь", ExampleSentence = "Buonanotte", ExampleTranslation = "Спокойной ночи" },
    };

            // Если язык не найден – используем английский
            string lang = targetLanguage?.ToLower() ?? "en";
            if (!dict.ContainsKey(lang)) lang = "en";

            var allWords = dict[lang];
            // Перемешиваем, чтобы каждый урок был разный
            var shuffled = allWords.OrderBy(x => Guid.NewGuid()).ToList();
            return shuffled.Take(count).ToList();
        }

        private string DemoFeedback(Word word, string userAnswer)
        {
            var tips = new[]
            {
                $"❌ Почти! '{word.Word1}' = '{word.Translation}'. 💡 Придумай ассоциацию!",
                $"📚 Ошибка: '{userAnswer}' → правильно '{word.Translation}'. Произнеси вслух 3 раза!",
                $"🧠 '{word.Word1}' переводится как '{word.Translation}'. Повтори через час!",
                $"💪 Не сдавайся! Правильно: {word.Translation}. ✨ У тебя получится!"
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
            return $"📖 I see a {word.Word1?.ToLower() ?? "word"} here.\n💬 Я вижу здесь «{word.Translation?.ToLower() ?? "слово"}».";
        }

        private string DemoDailyTip(int wordsLearned, int streakDays)
        {
            if (streakDays >= 7)
                return $"🔥 Неделя подряд! {wordsLearned} слов — серьёзный результат. Продолжай в том же духе!";
            if (wordsLearned >= 20)
                return $"⭐ {wordsLearned} слов уже в копилке! Повтори 10 старых — интервальное повторение эффективнее.";
            if (streakDays > 0)
                return $"📅 Серия: {streakDays} дней! Маленькие шаги каждый день важнее редких марафонов.";
            return "💡 10 минут в день эффективнее, чем час раз в неделю. Начни прямо сейчас!";
        }

        private string DemoWeaknessAnalysis(int accuracy)
        {
            if (accuracy >= 80)
                return $"🎯 Точность {accuracy}% — отличный результат! Продолжай в том же темпе!";
            if (accuracy >= 60)
                return $"📊 Точность {accuracy}% — хороший прогресс. Уделяй внимание словам, которые повторяются в уроках.";
            return $"💪 Точность {accuracy}% — есть куда расти! Повторяй ошибочные слова каждый день.";
        }
    }
}