using System.Text.Json;
using HelpdeskOps.Models.Enums;
using HelpdeskOps.Services.Suggestions;

namespace HelpdeskOps.Tests;

public class SuggestionsOptionsTests
{
    [Theory]
    [InlineData("", "", false, true)]
    [InlineData("key", "", false, true)]
    [InlineData("key", "https://proxy.sirket.test", false, true)]
    [InlineData("key", "http://proxy.sirket.test", true, false)]
    [InlineData("key", "http://localhost:9001", true, true)]
    [InlineData("key", "http://localhost:9001", false, false)]
    [InlineData("key", "not an address", false, false)]
    [InlineData("", "http://proxy.sirket.test", false, true)]
    public void Validate(string apiKey, string baseUrl, bool isDevelopment, bool isValid)
    {
        var error = new SuggestionsOptions { ApiKey = apiKey, BaseUrl = baseUrl }.Validate(isDevelopment);

        Assert.Equal(isValid, error == null);
    }

    [Fact]
    public void IsOffUntilAKeyIsSet()
    {
        Assert.False(new SuggestionsOptions().IsEnabled);
        Assert.True(new SuggestionsOptions { ApiKey = "key" }.IsEnabled);
    }
}

public class PersonalDataMaskTests
{
    [Theory]
    [InlineData("Bana ayse.yilmaz@sirket.com.tr adresinden yazın", "[e-posta]", "ayse.yilmaz")]
    [InlineData("Telefonum 0532 123 45 67", "[telefon]", "123 45")]
    [InlineData("Ara: +90 (532) 123-45-67 lütfen", "[telefon]", "123-45")]
    [InlineData("Dahili 5321234567", "[telefon]", "5321234567")]
    [InlineData("T.C. 12345678901 ile giriş", "[tckn]", "12345678901")]
    [InlineData("IBAN TR33 0006 1005 1978 6457 8413 26", "[iban]", "0006")]
    [InlineData("Sunucu 192.168.1.20 yanıt vermiyor", "[ip]", "192.168")]
    public void HidesWhatItCanRecognize(string text, string expected, string hidden)
    {
        var masked = PersonalDataMask.Apply(text);

        Assert.Contains(expected, masked);
        Assert.DoesNotContain(hidden, masked);
    }

    // Model and error numbers must survive, or the suggestion loses what it needs
    [Theory]
    [InlineData("HP LaserJet M404 yazıcı 0x80070005 hatası veriyor")]
    [InlineData("Outlook 2021 açılmıyor, 3 kişi etkilendi")]
    [InlineData("Windows 11 güncellemesi 25H2 yarıda kaldı")]
    public void LeavesOrdinaryTextAlone(string text)
    {
        Assert.Equal(text, PersonalDataMask.Apply(text));
    }
}

public class TicketSuggestionPromptTests
{
    private static readonly List<SuggestionArticle> Articles =
    [
        new(7, "Yazıcı kuyruğunu temizleme", TicketCategory.Printer, "Yazıcı yazdırmıyor"),
        new(9, "VPN'e bağlanma", TicketCategory.Network, null)
    ];

    private static string Answer(string category = "Printer", string subcategory = "NotPrinting", string priority = "High",
        string articleIds = "[7]") =>
        $$"""{"category":"{{category}}","subcategory":"{{subcategory}}","priority":"{{priority}}","reason":"Yazıcı sorunu.","articleIds":{{articleIds}}}""";

    [Fact]
    public void AValidAnswerBecomesASuggestion()
    {
        var suggestion = TicketSuggestionPrompt.ParseAnswer(Answer(), Articles);

        Assert.NotNull(suggestion);
        Assert.Equal(TicketCategory.Printer, suggestion.Category);
        Assert.Equal(TicketSubcategory.NotPrinting, suggestion.Subcategory);
        Assert.Equal(TicketPriority.High, suggestion.Priority);
        Assert.Equal("Yazıcı sorunu.", suggestion.Reason);
        Assert.Equal(7, Assert.Single(suggestion.Articles).Id);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"category":"Printer"}""")]
    public void AMalformedAnswerIsDropped(string answer)
    {
        Assert.Null(TicketSuggestionPrompt.ParseAnswer(answer, Articles));
    }

    [Fact]
    public void AnUnknownCategoryIsDropped()
    {
        Assert.Null(TicketSuggestionPrompt.ParseAnswer(Answer(category: "Finance"), Articles));
    }

    [Fact]
    public void ANumberInPlaceOfANameIsDropped()
    {
        Assert.Null(TicketSuggestionPrompt.ParseAnswer(Answer(priority: "99"), Articles));
    }

    [Fact]
    public void ASubcategoryFromAnotherCategoryIsDropped()
    {
        Assert.Null(TicketSuggestionPrompt.ParseAnswer(Answer(subcategory: "Vpn"), Articles));
    }

    [Fact]
    public void ArticlesTheModelMadeUpAreRemoved()
    {
        var suggestion = TicketSuggestionPrompt.ParseAnswer(Answer(articleIds: "[999, 7, 7]"), Articles);

        Assert.Equal(7, Assert.Single(suggestion!.Articles).Id);
    }

    [Fact]
    public void AtMostThreeArticlesAreKept()
    {
        var many = Enumerable.Range(1, 5).Select(id => new SuggestionArticle(id, $"Makale {id}", TicketCategory.Other, null)).ToList();

        var suggestion = TicketSuggestionPrompt.ParseAnswer(Answer(articleIds: "[1,2,3,4,5]"), many);

        Assert.Equal(TicketSuggestionPrompt.MaxArticles, suggestion!.Articles.Count);
    }

    [Fact]
    public void TheMessageSentOutIsMasked()
    {
        var message = TicketSuggestionPrompt.BuildUserMessage("Yazıcı bozuk", "Ulaşın: ali@sirket.com.tr, 0532 123 45 67", Articles);

        Assert.DoesNotContain("ali@sirket.com.tr", message);
        Assert.DoesNotContain("0532", message);
        Assert.Contains("Yazıcı bozuk", message);
        Assert.Contains("7. [Printer] Yazıcı kuyruğunu temizleme", message);
    }

    [Fact]
    public void TheSystemPromptOffersEveryCategoryAndTheSchemaIsClosed()
    {
        foreach (var category in Enum.GetNames<TicketCategory>())
        {
            Assert.Contains(category, TicketSuggestionPrompt.SystemPrompt);
        }

        var schema = TicketSuggestionPrompt.OutputSchema();
        Assert.Equal(JsonValueKind.False, schema["additionalProperties"].ValueKind);
    }
}
