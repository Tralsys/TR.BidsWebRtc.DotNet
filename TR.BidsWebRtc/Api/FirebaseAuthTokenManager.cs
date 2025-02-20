using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TR.BidsWebRtc.Api;

public class FirebaseAuthTokenManager(
	HttpClient httpClient,
	string firebaseApiKey,
	string refreshToken
	) : ITokenManager
{
	private readonly HttpClient _httpClient = httpClient;
	private string _refreshToken = refreshToken;
	private string? _accessToken = null;
	private DateTime _accessTokenExpirationTime = DateTime.MinValue;


	public FirebaseAuthTokenManager(
		HttpClient httpClient,
		string firebaseApiKey,
		string refreshToken,
		string accessToken
	) : this(httpClient, firebaseApiKey, refreshToken)
	{
		SetNextAccessToken(accessToken);
	}

	private const string FIREBASE_REFRESH_TOKEN_GRANT_TYPE = "refresh_token";
	private string _TokenRefreshUrl = $"https://securetoken.googleapis.com/v1/token?key={firebaseApiKey}";
	public void SetTokenRefreshUrl(string url) => _TokenRefreshUrl = url;

	public async Task<string> GetTokenAsync()
	{
		if (_accessToken is not null && !IsTokenRefreshRequired())
		{
			return _accessToken;
		}
		return await RefreshTokenAsync();
	}

	private void SetNextAccessToken(string token)
	{
		var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
		var tokenExp = jwt.Claims.First(claim => claim.Type == "exp").Value;
		var ticks = long.Parse(tokenExp);
		_accessTokenExpirationTime = DateTimeOffset.FromUnixTimeSeconds(ticks).DateTime;
		_accessToken = token;
	}
	private bool IsTokenRefreshRequired() => _accessTokenExpirationTime < DateTime.UtcNow.AddMinutes(3);

	private async Task<string> RefreshTokenAsync()
	{
		using var content = new FormUrlEncodedContent(
		[
			new("grant_type", FIREBASE_REFRESH_TOKEN_GRANT_TYPE),
			new("refresh_token", _refreshToken),
		]);
		using var request = new HttpRequestMessage(HttpMethod.Post, _TokenRefreshUrl)
		{
			Content = content,
		};
		using var response = await _httpClient.SendAsync(request);

		response.EnsureSuccessStatusCode();

		using var responseContent = await response.Content.ReadAsStreamAsync();
		using var doc = await JsonDocument.ParseAsync(responseContent);
		_refreshToken = doc.RootElement.GetProperty("refresh_token").GetString() ?? throw new JsonException("Missing required property 'refresh_token'");
		var token = doc.RootElement.GetProperty("access_token").GetString() ?? throw new JsonException("Missing required property 'access_token'");
		SetNextAccessToken(token);
		return token;
	}
}
