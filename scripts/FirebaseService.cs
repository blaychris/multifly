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
    private const string FirebaseApiKey = "AIzaSyBrFdewHRaXYMhVDLnVdVbihvqpF7sMfX8"; // Replace with your Firebase Web API key
    private const string FirebaseDatabaseUrl = "https://multifly-25c64-default-rtdb.asia-southeast1.firebasedatabase.app/";
    private const string LeaderboardPath = "leaderboard/scores";

    private string _idToken = string.Empty;
    private string _refreshToken = string.Empty;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_idToken) && _tokenExpiry > DateTimeOffset.UtcNow;

    public bool LastFetchFailed { get; private set; }
    public string LastFetchErrorMessage { get; private set; } = string.Empty;
    public string LastFetchDebugInfo { get; private set; } = string.Empty;

    private string BuildDatabaseUrl(string path)
    {
        var baseUrl = FirebaseDatabaseUrl.TrimEnd('/');
        path = path.Trim('/');
        var url = $"{baseUrl}/{path}.json";

        if (IsAuthenticated)
        {
            url += url.Contains('?') ? $"&auth={_idToken}" : $"?auth={_idToken}";
        }

        return url;
    }

    public async Task<bool> SignInAsync(string email, string password)
    {
        try
        {
            // Use anonymous auth path only; credentials are ignored.
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={FirebaseApiKey}";

            var payload = new
            {
                returnSecureToken = true
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                GD.PrintErr($"FirebaseService: SignIn failed: {responseBody}");
                return false;
            }

            using var document = JsonDocument.Parse(responseBody);
            _idToken = document.RootElement.GetProperty("idToken").GetString() ?? string.Empty;
            _refreshToken = document.RootElement.GetProperty("refreshToken").GetString() ?? string.Empty;
            var expiresIn = document.RootElement.GetProperty("expiresIn").GetString() ?? "0";
            if (long.TryParse(expiresIn, out var expiresSeconds))
            {
                _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(expiresSeconds - 30);
            }

            GD.Print("FirebaseService: Successfully signed in and acquired idToken.");
            return true;
        }
        catch (Exception e)
        {
            GD.PrintErr($"FirebaseService: SignInAsync exception: {e.Message}");
            return false;
        }
    }

    private string ToDatabaseUrl(string path)
    {
        return BuildDatabaseUrl(path);
    }

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
            string url = ToDatabaseUrl($"{LeaderboardPath}/{playerId}");

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
        LastFetchFailed = false;
        LastFetchErrorMessage = string.Empty;
        LastFetchDebugInfo = string.Empty;

        try
        {
            string url = ToDatabaseUrl(LeaderboardPath);

            using var response = await httpClient.GetAsync(url);
            string responseBody = await response.Content.ReadAsStringAsync();

            GD.Print($"FirebaseService: FetchLeaderboard response code={response.StatusCode}");
            GD.Print($"FirebaseService: Leaderboard data={responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                LastFetchFailed = true;
                LastFetchErrorMessage = $"FirebaseService: FetchLeaderboard failed with status {response.StatusCode}: {responseBody}";
                LastFetchDebugInfo = GetFetchDebugInfo(url);
                return new List<LeaderboardEntry>();
            }

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
            LastFetchFailed = true;
            LastFetchErrorMessage = $"FirebaseService: FetchLeaderboard failed: {e.Message}";
            LastFetchDebugInfo = GetFetchDebugInfo(LeaderboardPath);
            GD.PrintErr(LastFetchErrorMessage);
            return new List<LeaderboardEntry>();
        }
    }

    private string GetFetchDebugInfo(string url)
    {
        return "FirebaseService Debug Info:\n" +
               $"ApiKey={FirebaseApiKey}\n" +
               $"DatabaseUrl={FirebaseDatabaseUrl}\n" +
               $"LeaderboardPath={LeaderboardPath}\n" +
               $"RequestUrl={url}\n" +
               $"IsAuthenticated={IsAuthenticated}\n" +
               $"IdToken={(string.IsNullOrWhiteSpace(_idToken) ? "<empty>" : _idToken)}\n" +
               $"RefreshToken={(string.IsNullOrWhiteSpace(_refreshToken) ? "<empty>" : _refreshToken)}\n" +
               $"TokenExpiry={_tokenExpiry:O}";
    }

    public async Task<LeaderboardEntry?> FetchLeaderboardEntryAsync(string playerId)
    {
        try
        {
            string url = ToDatabaseUrl($"{LeaderboardPath}/{playerId}");

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
