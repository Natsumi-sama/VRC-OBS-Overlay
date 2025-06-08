using System.Net;
using HtmlAgilityPack;
using Serilog;
using ILogger = Serilog.ILogger;

public class WorldInfoData
{
    public string WorldId { get; set; }
    public string WorldName { get; set; }
    public string AuthorName { get; set; }
    public string ImageUrl { get; set; }
}

public class WorldInfo
{
    private static readonly ILogger Logger = Log.Logger.ForContext<WorldInfo>();
    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "VRCOBSOverlay" } }
    };
    private const string WorldInfoUrl = "https://vrchat.com/home/world/{0}";
    
    private static readonly Dictionary<string, string> SymbolMap = new()
    {
        {"@", "＠"},
        {"#", "＃"},
        {"$", "＄"},
        {"%", "％"},
        {"&", "＆"},
        {"=", "＝"},
        {"+", "＋"},
        {"/", "⁄"},
        {"\\", "＼"},
        {";", ";"},
        {":", "˸"},
        {",", "‚"},
        {"?", "？"},
        {"!", "ǃ"},
        {"\"", "＂"},
        {"<", "≺"},
        {">", "≻"},
        {".", "․"},
        {"^", "＾"},
        {"{", "｛"},
        {"}", "｝"},
        {"[", "［"},
        {"]", "］"},
        {"(", "（"},
        {")", "）"},
        {"|", "｜"},
        {"*", "∗"}
    };
    
    private static void ReplaceSymbols(ref string input)
    {
        foreach (var symbol in SymbolMap)
        {
            input = input.Replace(symbol.Value, symbol.Key);
        }
    }
    
    public static async Task<WorldInfoData?> GetWorldInfo(string worldId)
    {
        if (string.IsNullOrEmpty(worldId))
            return null;

        var url = string.Format(WorldInfoUrl, worldId);
        using var response = await HttpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            Logger.Error("Failed to fetch world info for {WorldId}: {ResponseStatusCode}", worldId, response.StatusCode);
            return null;
        }
        var content = await response.Content.ReadAsStringAsync();
        
        var doc = new HtmlDocument();
        doc.LoadHtml(content);
        var docTitle = doc.DocumentNode.Descendants("meta")
            .FirstOrDefault(m => m.GetAttributeValue("name", "") == "og:title")
            ?.GetAttributeValue("content", "").Trim();
        if (string.IsNullOrEmpty(docTitle))
        {
            Logger.Error("World name not found in document title for {WorldId}", worldId);
            return null;
        }
        
        var index = docTitle.IndexOf(" by ", StringComparison.Ordinal);
        if (index < 0)
        {
            Logger.Error("World name format is incorrect for {WorldId}: {DocTitle}", worldId, docTitle);
            return null;
        }
        var worldName = docTitle.Substring(0, index);
        var authorName = docTitle.Substring(index + 1);
        
        // fix html tags
        worldName = WebUtility.HtmlDecode(worldName);
        authorName = WebUtility.HtmlDecode(authorName);
        ReplaceSymbols(ref worldName);
        ReplaceSymbols(ref authorName);
        
        var imageUrl = doc.DocumentNode.Descendants("meta")
            .FirstOrDefault(m => m.GetAttributeValue("name", "") == "og:image")
            ?.GetAttributeValue("content", "").Trim() ?? string.Empty;
        
        Logger.Information("World info for {WorldId}: Name: {WorldName}, Author: {AuthorName}, Image: {ImageUrl}", worldId, worldName, authorName, imageUrl);
        
        return new WorldInfoData
        {
            WorldId = worldId,
            WorldName = worldName,
            AuthorName = authorName,
            ImageUrl = imageUrl
        };
    }
}