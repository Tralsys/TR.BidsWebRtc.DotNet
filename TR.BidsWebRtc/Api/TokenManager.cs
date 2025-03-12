using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TR.BidsWebRtc.Api;

public class TokenManager(
	HttpClient httpClient,
	byte[] refreshToken
	) : ITokenManager
{
	private readonly HttpClient _httpClient = httpClient;
	private byte[] _refreshToken = refreshToken;
	private string? _accessToken = null;
	private DateTime _accessTokenExpirationTime = DateTime.MinValue;
	public event EventHandler<byte[]>? RefreshTokenChanged;

	public TokenManager(
		HttpClient httpClient,
		byte[] refreshToken,
		string accessToken
	) : this(httpClient, refreshToken)
	{
		SetNextAccessToken(accessToken);
	}

	private string _TokenRefreshUrl = $"https://bids-rtc.t0r.dev/signaling/client_token";
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
		using var request = new HttpRequestMessage(HttpMethod.Put, _TokenRefreshUrl)
		{
			Content = new ByteArrayContent(_refreshToken),
		};
		request.Headers.Add("Accept", "application/jose");
		request.Content.Headers.ContentType = new("application/jose");
		using var response = await _httpClient.SendAsync(request);

		response.EnsureSuccessStatusCode();

		var responseContent = await response.Content.ReadAsStringAsync();
		string accessToken;
		if (responseContent.StartsWith("{"))
		{
			using var doc = JsonDocument.Parse(responseContent);
			var refreshToken = doc.RootElement.GetProperty("refresh_token").GetString() ?? throw new JsonException("Missing required property 'refresh_token'");
			accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? throw new JsonException("Missing required property 'access_token'");
			byte[] nextRefreshToken = Encoding.ASCII.GetBytes(refreshToken);
			if (!nextRefreshToken.AsSpan().SequenceEqual(_refreshToken))
			{
				_refreshToken = nextRefreshToken;
				RefreshTokenChanged?.Invoke(this, nextRefreshToken);
			}
		}
		else if (response.Content.Headers.ContentType?.MediaType == "application/jose")
		{
			accessToken = responseContent;
		}
		else
		{
			throw new InvalidOperationException($"Unexpected response content: {responseContent}");
		}
		SetNextAccessToken(accessToken);
		return accessToken;
	}
}
