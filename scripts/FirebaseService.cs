using Godot;
#nullable enable
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class LeaderboardEntry
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public int Score { get; set; }
    public int Stage { get; set; }
    public long Timestamp { get; set; }
}

public partial class FirebaseService : Node
{
    private readonly System.Net.Http.HttpClient httpClient = new();
    private const string FirebaseDatabaseUrl = "https://multifly-25c64-default-rtdb.asia-southeast1.firebasedatabase.app/";
    private const string LeaderboardPath = "leaderboard/scores";

    public void SubmitScore(string playerId, string playerName, int score, int stage)
    {
        _ = SubmitScoreAsync(playerId, playerName, score, stage);
    }

    private async Task SubmitScoreAsync(string playerId, string playerName, int score, int stage)
    {
        try
        {
            var payload = new Dictionary<string, object>
            {
                ["playerId"] = playerId,
                ["playerName"] = playerName,
                ["score"] = score,
                ["stage"] = stage,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string json = JsonSerializer.Serialize(payload);
            // Ensure no double slashes or whitespace in URL
            string baseUrl = FirebaseDatabaseUrl.TrimEnd('/');
            string path = LeaderboardPath.Trim('/');
            string url = $"{baseUrl}/{path}/{playerId}.json";

            GD.Print($"FirebaseService: Submitting to URL: {url}");

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await httpClient.PutAsync(url, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            GD.Print($"FirebaseService: SubmitScore response code={response.StatusCode}");
            GD.Print($"FirebaseService: SubmitScore body={responseBody}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"FirebaseService: SubmitScore failed: {e.Message}");
        }
    }

    public async Task<List<LeaderboardEntry>> FetchLeaderboardAsync()
    {
        try
        {
            string baseUrl = FirebaseDatabaseUrl.TrimEnd('/');
            string path = LeaderboardPath.Trim('/');
            string url = $"{baseUrl}/{path}.json";

            using var response = await httpClient.GetAsync(url);
            string responseBody = await response.Content.ReadAsStringAsync();

            GD.Print($"FirebaseService: FetchLeaderboard response code={response.StatusCode}");
            GD.Print($"FirebaseService: Leaderboard data={responseBody}");

            var entries = new List<LeaderboardEntry>();
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var child in document.RootElement.EnumerateObject())
                {
                    var item = child.Value;
                    entries.Add(new LeaderboardEntry
                    {
                        PlayerId = child.Name,
                        PlayerName = item.TryGetProperty("playerName", out var playerNameProp) ? playerNameProp.GetString() ?? "Unknown" : "Unknown",
                        Score = item.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetInt32(out var scoreValue) ? scoreValue : 0,
                        Stage = item.TryGetProperty("stage", out var stageProp) && stageProp.TryGetInt32(out var stageValue) ? stageValue : 0,
                        Timestamp = item.TryGetProperty("timestamp", out var timestampProp) && timestampProp.TryGetInt64(out var timestampValue) ? timestampValue : 0
                    });
                }
            }

            entries.Sort((a, b) =>
            {
                int compare = b.Score.CompareTo(a.Score);
                if (compare != 0)
                {
                    return compare;
                }
                return a.Timestamp.CompareTo(b.Timestamp);
            });

            return entries;
        }
        catch (Exception e)
        {
            GD.PrintErr($"FirebaseService: FetchLeaderboard failed: {e.Message}");
            return new List<LeaderboardEntry>();
        }
    }

    public async Task<LeaderboardEntry?> FetchLeaderboardEntryAsync(string playerId)
    {
        try
        {
            string baseUrl = FirebaseDatabaseUrl.TrimEnd('/');
            string path = LeaderboardPath.Trim('/');
            string url = $"{baseUrl}/{path}/{playerId}.json";

            GD.Print($"FirebaseService: Fetching leaderboard record from {url}...");
            using var response = await httpClient.GetAsync(url);
            string responseBody = await response.Content.ReadAsStringAsync();

            GD.Print($"FirebaseService: FetchLeaderboardEntry response code={response.StatusCode}");
            GD.Print($"FirebaseService: FetchLeaderboardEntry body={responseBody}");

            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var item = document.RootElement;
            return new LeaderboardEntry
            {
                PlayerId = playerId,
                PlayerName = item.TryGetProperty("playerName", out var playerNameProp) ? playerNameProp.GetString() ?? "Unknown" : "Unknown",
                Score = item.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetInt32(out var scoreValue) ? scoreValue : 0,
                Stage = item.TryGetProperty("stage", out var stageProp) && stageProp.TryGetInt32(out var stageValue) ? stageValue : 0,
                Timestamp = item.TryGetProperty("timestamp", out var timestampProp) && timestampProp.TryGetInt64(out var timestampValue) ? timestampValue : 0
            };
        }
        catch (Exception e)
        {
            GD.PrintErr($"FirebaseService: FetchLeaderboardEntry failed: {e.Message}");
            return null;
        }
    }
}
