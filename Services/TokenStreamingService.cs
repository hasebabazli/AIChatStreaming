using AIChatStreaming.Models;
using System.Runtime.CompilerServices;
using System.Text;

namespace AIChatStreaming.Services;

public interface ITokenStreamingService
{
    IAsyncEnumerable<StreamEvent> StreamResponseAsync(
        string userMessage,
        List<ChatMessage> history,
        double speedMs,
        CancellationToken cancellationToken);
}

public class TokenStreamingService : ITokenStreamingService
{
    private readonly ILogger<TokenStreamingService> _logger;
    private readonly Random _rng = new();


    private static readonly Dictionary<string[], string[]> _responses = new(new KeyArrayComparer())
    {
        {
            new[] { "مرحبا", "اهلا", "هاي", "hello", "hi", "hey" },
            new[]
            {
                "مرحباً بك! 👋 أنا مساعدك الذكي المدعوم بتقنية SSE Streaming. كيف يمكنني مساعدتك اليوم؟",
                "أهلاً وسهلاً! 😊 يسعدني مساعدتك. ما الذي تودّ معرفته؟"
            }
        },
        {
            new[] { "sse", "server-sent events", "streaming", "بث", "stream" },
            new[]
            {
                "Server-Sent Events (SSE) هي تقنية رائعة تتيح للخادم إرسال بيانات بشكل مستمر إلى المتصفح عبر اتصال HTTP واحد مفتوح.\n\nمميزاتها:\n✅ بسيطة ومدعومة في المتصفحات بدون مكتبات\n✅ إعادة الاتصال تلقائية عند الانقطاع\n✅ مثالية للإشعارات الفورية والبيانات الحية\n✅ أقل تعقيداً من WebSocket للاتصال أحادي الاتجاه\n\nفي ASP.NET Core تُنفَّذ عبر Response.ContentType = \"text/event-stream\" مع الكتابة المستمرة على Response.Body."
            }
        },
        {
            new[] { "asp.net", "aspnet", "mvc", "dotnet", ".net", "سي شارب", "c#" },
            new[]
            {
                "ASP.NET Core MVC هو إطار عمل قوي ومفتوح المصدر من Microsoft لبناء تطبيقات ويب.\n\nمكوناته الرئيسية:\n🎯 Model: طبقة البيانات والمنطق\n🎨 View: طبقة العرض (Razor Pages)\n⚙️ Controller: طبقة التحكم والتنسيق\n\nلدعم SSE في ASP.NET Core:\n```csharp\nResponse.Headers[\"Content-Type\"] = \"text/event-stream\";\nResponse.Headers[\"Cache-Control\"] = \"no-cache\";\nawait Response.WriteAsync($\"data: {token}\\n\\n\");\nawait Response.Body.FlushAsync();\n```"
            }
        },
        {
            new[] { "websocket", "ws", "ويب سوكت" },
            new[]
            {
                "WebSocket هو بروتوكول اتصال ثنائي الاتجاه (Full-Duplex) يختلف عن SSE:\n\n📊 مقارنة SSE vs WebSocket:\n\n| الميزة | SSE | WebSocket |\n|--------|-----|----------|\n| الاتجاه | خادم → عميل | ثنائي الاتجاه |\n| البروتوكول | HTTP | WS/WSS |\n| إعادة الاتصال | تلقائية | يدوية |\n| التعقيد | بسيط | متوسط |\n| الاستخدام | إشعارات، بث | دردشة، ألعاب |\n\nللبث أحادي الاتجاه كـAI Chat، SSE هو الخيار الأمثل! ✨"
            }
        },
        {
            new[] { "ذكاء اصطناعي", "ai", "artificial intelligence", "chatgpt", "gpt", "llm" },
            new[]
            {
                "الذكاء الاصطناعي التوليدي يعمل عبر نماذج لغوية ضخمة (LLMs) تُنتج النص token بـtoken.\n\nكيف يعمل بث الـTokens؟\n\n1️⃣ المستخدم يرسل السؤال\n2️⃣ النموذج يبدأ التوليد فوراً\n3️⃣ كل token (كلمة/جزء كلمة) يُرسل عبر SSE فور إنتاجه\n4️⃣ المتصفح يُضيفه للنص المعروض\n5️⃣ النموذج يرسل إشارة [DONE] عند الانتهاء\n\nهذا يجعل المستخدم يرى الرد يتشكّل في الوقت الفعلي بدلاً من انتظار الرد كاملاً! 🚀"
            }
        },
        {
            new[] { "مشروع", "project", "تخرج", "جامعة", "university", "إدلب" },
            new[]
            {
                "مشروع AI Chat Token Streaming رائع للمشاريع الجامعية! 🎓\n\nما يجعله متميزاً:\n\n🔥 تقنية SSE الحديثة لبث الـTokens\n⚡ تجربة مستخدم فورية وسلسة\n🛑 إمكانية إيقاف البث في أي لحظة\n⚙️ تحكم بسرعة البث (throttle)\n📊 إحصائيات الـTokens في الوقت الفعلي\n🔄 إعادة الاتصال التلقائية عند الانقطاع\n\nالفكرة الذكية (Smart Idea):\nتعديل سرعة البث ديناميكياً + إمكانية الإيقاف المتوسط للبث! ✅"
            }
        }
    };

    private static readonly string[] _defaultResponses =
    {
        "سؤال رائع! 🤔 دعني أفكر في ذلك...\n\nبناءً على ما فهمته، يمكنني القول أن هذا الموضوع يستحق الدراسة المعمّقة. تقنية SSE التي نستخدمها هنا تتيح لك رؤية هذا الرد يتشكّل كلمةً بكلمة، تماماً كما تعمل أنظمة الذكاء الاصطناعي الحديثة مثل ChatGPT.\n\nهل تريد معرفة المزيد عن أي جانب محدد؟ 😊",
        "شكراً على سؤالك! 💡\n\nهذا يذكّرني بمفهوم مهم في تطوير الويب الحديث: التفاعلية الفورية. بدلاً من انتظار الخادم حتى ينتهي من معالجة الطلب كاملاً، نستخدم Streaming لإرسال النتائج فور توفرها.\n\nهذا بالضبط ما يجعل تجربة ChatGPT سلسة وممتعة - الرد يظهر مباشرة دون انتظار! ⚡",
        "سؤال ممتاز! 🌟\n\nفي عالم تطوير الويب المتقدم، نهدف دائماً إلى تحسين تجربة المستخدم. تقنية Token Streaming عبر Server-Sent Events هي نموذج مثالي لذلك.\n\nالمستخدم لا يرى شاشة بيضاء وينتظر - بل يرى الإجابة تتكوّن أمامه لحظة بلحظة، مما يعطي إحساساً بالحيوية والاستجابة الفورية. 🎯"
    };

    public TokenStreamingService(ILogger<TokenStreamingService> logger)
    {
        _logger = logger;
    }

    public async IAsyncEnumerable<StreamEvent> StreamResponseAsync(
        string userMessage,
        List<ChatMessage> history,
        double speedMs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var response = GetResponse(userMessage);
        var tokens = Tokenize(response);
        var totalTokens = tokens.Count;

        yield return new StreamEvent
        {
            Type = "metadata",
            Data = $"{{\"totalTokens\":{totalTokens},\"model\":\"AIChatSSE-v1\"}}",
            TotalTokens = totalTokens
        };

        await Task.Delay(150, cancellationToken);

        for (int i = 0; i < tokens.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var token = tokens[i];
            var probability = CalculateProbability(token, i, totalTokens);

            var delay = GetAdaptiveDelay(token, speedMs);

            yield return new StreamEvent
            {
                Type = "token",
                Data = token,
                TokenIndex = i,
                TotalTokens = totalTokens,
                Probability = probability
            };

            await Task.Delay((int)delay, cancellationToken);
        }

        yield return new StreamEvent
        {
            Type = "done",
            Data = $"{{\"totalTokens\":{totalTokens},\"message\":\"Stream completed\"}}",
            TotalTokens = totalTokens
        };
    }

    private string GetResponse(string message)
    {
        var lower = message.ToLowerInvariant();
        foreach (var kv in _responses)
        {
            if (kv.Key.Any(k => lower.Contains(k)))
            {
                var arr = kv.Value;
                return arr[_rng.Next(arr.Length)];
            }
        }
        return _defaultResponses[_rng.Next(_defaultResponses.Length)];
    }

    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();

        foreach (char c in text)
        {
            if (c == ' ' || c == '\n')
            {
                if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Clear(); }
                tokens.Add(c == '\n' ? "\n" : " ");
            }
            else
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    private double GetAdaptiveDelay(string token, double baseSpeed)
    {
        if (token is "." or "!" or "?" or "،" or "؟") return baseSpeed * 4;
        if (token is "," or ":" or "؛") return baseSpeed * 2;
        if (token == "\n") return baseSpeed * 3;
        return baseSpeed + _rng.NextDouble() * (baseSpeed * 0.3);
    }

    private double CalculateProbability(string token, int index, int total)
    {
        var base_ = 0.7 + (_rng.NextDouble() * 0.29);
        if (token.Length <= 2) base_ = Math.Min(base_ + 0.05, 1.0);
        return Math.Round(base_, 3);
    }
}


class KeyArrayComparer : IEqualityComparer<string[]>
{
    public bool Equals(string[]? x, string[]? y) => x != null && y != null && x.SequenceEqual(y);
    public int GetHashCode(string[] obj) => obj.Aggregate(0, (h, s) => h ^ s.GetHashCode());
}
