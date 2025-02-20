using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

using TR.BidsWebRtc.Api.Model;

namespace TR.BidsWebRtc.Api;

public class SdpExchangeApi(
	HttpClient httpClient,
	ITokenManager tokenManager,
	string baseUrl,
	Guid clientId
)
{
	const string DEFAULT_BASE_URL = "https://bids-rtc.t0r.dev/signaling";
	private readonly HttpClient _httpClient = httpClient;
	private readonly ITokenManager _tokenManager = tokenManager;
	private readonly string _baseUrl = baseUrl.EndsWith("/") ? baseUrl[..^1] : baseUrl;
	public readonly Guid ClientId = clientId;

	public SdpExchangeApi(
		HttpClient httpClient,
		ITokenManager tokenManager
	) : this(httpClient, tokenManager, DEFAULT_BASE_URL, Guid.NewGuid()) { }
	public SdpExchangeApi(
		HttpClient httpClient,
		ITokenManager tokenManager,
		string baseUrl
	) : this(httpClient, tokenManager, baseUrl, Guid.NewGuid()) { }

	private async Task SetHeadersAsync(HttpRequestHeaders headers)
	{
		var token = await _tokenManager.GetTokenAsync();
		headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
		headers.Accept.Add(JSON_MEDIA_TYPE_WITH_QUALITY);
		headers.Add("X-Client-Id", ClientId.ToString());
	}

	private static readonly MediaTypeHeaderValue JSON_MEDIA_TYPE = new("application/json");
	private static readonly MediaTypeWithQualityHeaderValue JSON_MEDIA_TYPE_WITH_QUALITY = new("application/json");
	const string REGISTER_OFFER_ENDPOINT = "/offer";
	const string REGISTER_ANSWER_ENDPOINT = "/offer";

	public async Task<RegisterOfferResult> RegisterOfferAsync(
		RegisterOfferParams param
	)
	{
		byte[] requestBody;
		using (var stream = new MemoryStream())
		{
			param.WriteJson(stream);
			requestBody = stream.ToArray();
		}
		using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{REGISTER_OFFER_ENDPOINT}")
		{
			Content = new ByteArrayContent(requestBody),
		};
		request.Content.Headers.ContentType = JSON_MEDIA_TYPE;
		await SetHeadersAsync(request.Headers);
		using var response = await _httpClient.SendAsync(request);

		response.EnsureSuccessStatusCode();

		using var responseStream = await response.Content.ReadAsStreamAsync();
		return await RegisterOfferResult.FromJsonAsync(responseStream);
	}

	public async Task RegisterAnswerAsync(
		SDPAnswerInfo[] answerInfoArray
	)
	{
		byte[] requestBody;
		using (var stream = new MemoryStream())
		{
			SDPAnswerInfo.WriteArrayJson(stream, answerInfoArray);
			requestBody = stream.ToArray();
		}
		using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{REGISTER_ANSWER_ENDPOINT}")
		{
			Content = new ByteArrayContent(requestBody),
		};
		request.Content.Headers.ContentType = JSON_MEDIA_TYPE;
		await SetHeadersAsync(request.Headers);
		using var response = await _httpClient.SendAsync(request);

		response.EnsureSuccessStatusCode();
	}

	public async Task<SDPAnswerInfo?> GetAnswerAsync(
		Guid sdpId
	)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}{REGISTER_ANSWER_ENDPOINT}/{sdpId}");
		await SetHeadersAsync(request.Headers);
		using var response = await _httpClient.SendAsync(request);
		if (response.StatusCode == HttpStatusCode.NoContent)
		{
			return null;
		}

		response.EnsureSuccessStatusCode();

		using var responseStream = await response.Content.ReadAsStreamAsync();
		return SDPAnswerInfo.FromJson(responseStream);
	}
}
