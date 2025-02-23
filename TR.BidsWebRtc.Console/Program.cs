using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using TR.BidsWebRtc.Api;
using TR.BidsWebRtc.WebRtc;

namespace TR.BidsWebRtc.Console;

class Program
{
	static readonly HttpClient httpClient = new();
	const string TOKEN_REFRESH_URL = "http://127.0.0.1:9099/securetoken.googleapis.com/v1/token?key=dummy";
	static readonly FirebaseAuthTokenManager tokenManager = new(
		httpClient,
		"",
		"eyJfQXV0aEVtdWxhdG9yUmVmcmVzaFRva2VuIjoiRE8gTk9UIE1PRElGWSIsImxvY2FsSWQiOiI2V1c1N0J0UGJJcXJ3Y3ljOXRUNEdybGQyY2VEIiwicHJvdmlkZXIiOiJwYXNzd29yZCIsImV4dHJhQ2xhaW1zIjp7fSwicHJvamVjdElkIjoiYmlkcy1ydGMifQ=="
	);
	static readonly SdpExchangeApi sdpExchangeApi = new(
		httpClient,
		tokenManager,
		"http://127.0.0.1/signaling"
	);

	public static async Task Main(string[] args)
	{
		tokenManager.SetTokenRefreshUrl(TOKEN_REFRESH_URL);

		using var manager = RtcConnectionManager.Create(
			RtcConnectionManager.Role.Provider,
			sdpExchangeApi
		);

		System.Console.WriteLine("Running...");
		while (true)
		{
			byte[] message = Encoding.UTF8.GetBytes($"Hello, World! @ {DateTime.Now}");
			manager.BroadcastMessage(message);

			await Task.Delay(1000);
		}
	}
}
