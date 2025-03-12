using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using TR.BIDSSMemLib;
using TR.BidsWebRtc.Api;
using TR.BidsWebRtc.WebRtc;

namespace TR.BidsWebRtc.Console;

class Program : IDisposable
{
	static readonly HttpClient httpClient = new();
	const string TOKEN_REFRESH_URL = "http://127.0.0.1/signaling/client_token";
	const string SDP_EXCHANGE_API_URL = "http://127.0.0.1/signaling";
	readonly TokenManager tokenManager;
	readonly SdpExchangeApi sdpExchangeApi;
	readonly RtcConnectionManager manager;
	readonly CancellationTokenSource cts = new();

	public static async Task Main(string[] args)
	{
		byte[] refreshToken;
		using (Stream? stream = typeof(Program).Assembly.GetManifestResourceStream("TR.BidsWebRtc.Console.token.txt"))
		{
			if (stream is null)
			{
				throw new FileNotFoundException("Token file not found.");
			}
			using var memStream = new MemoryStream();
			await stream.CopyToAsync(memStream);
			refreshToken = memStream.ToArray();
		}

		using var program = new Program(refreshToken);
		System.Console.CancelKeyPress += (_, _) =>
		{
			program.cts.Cancel();
		};
		await program.RunAsync();
	}

	private Program(byte[] refreshToken)
	{
		tokenManager = new(
			httpClient,
			refreshToken
		);
		if (!string.IsNullOrEmpty(TOKEN_REFRESH_URL))
		{
			tokenManager.SetTokenRefreshUrl(TOKEN_REFRESH_URL);
		}

		sdpExchangeApi = new(
			httpClient,
			tokenManager,
			SDP_EXCHANGE_API_URL
		);

		manager = RtcConnectionManager.Create(
			RtcConnectionManager.Role.Provider,
			sdpExchangeApi
		);
		manager.OnDataGot += (sender, e) =>
		{
			System.Console.WriteLine($"Data got from {e.ClientId}: {Encoding.UTF8.GetString(e.Data)}");
		};
	}

	private async Task RunAsync()
	{
		System.Console.WriteLine("Running...");
		BIDSSharedMemoryData bsmd = new();
		int specSize = Marshal.SizeOf<Spec>();
		int stateSize = Marshal.SizeOf<State>();
		int handleSize = Marshal.SizeOf<Hand>();
		int bsmdSize = 1 + 4 + specSize + stateSize + handleSize + 1;
		int specOffset = 1 + 4;
		int stateOffset = specOffset + specSize;
		int handleOffset = stateOffset + stateSize;
		int doorClosedOffset = handleOffset + handleSize;
		System.Console.WriteLine($"BIDSSharedMemoryData size: {bsmdSize}");
		byte[] data = new byte[bsmdSize + 4];
		data[0] = 0x74;
		data[1] = 0x72;
		data[2] = 0x57;
		data[3] = 0x42;
		IntPtr ptr = Marshal.AllocHGlobal(bsmdSize);
		while (!cts.IsCancellationRequested)
		{
			bsmd.IsDoorClosed = !bsmd.IsDoorClosed;
			bsmd.SpecData.C = Random.Shared.Next() % 32;
			bsmd.StateData.Z = Random.Shared.NextDouble() * Random.Shared.Next();
			bsmd.StateData.V = Random.Shared.NextSingle() * Random.Shared.Next();
			bsmd.StateData.T = (int)(DateTime.Now.TimeOfDay.TotalSeconds * 1000);

			data[4] = bsmd.IsEnabled ? (byte)1 : (byte)0;
			BitConverter.GetBytes(bsmd.VersionNum).CopyTo(data, 4 + 1);

			Marshal.StructureToPtr(bsmd.SpecData, ptr, false);
			Marshal.Copy(ptr, data, 4 + specOffset, specSize);

			Marshal.StructureToPtr(bsmd.StateData, ptr, false);
			Marshal.Copy(ptr, data, 4 + stateOffset, stateSize);

			Marshal.StructureToPtr(bsmd.HandleData, ptr, false);
			Marshal.Copy(ptr, data, 4 + handleOffset, handleSize);

			data[4 + doorClosedOffset] = bsmd.IsDoorClosed ? (byte)1 : (byte)0;

			manager.BroadcastMessage(data);

			await Task.Delay(1000, cts.Token);
		}
		Marshal.FreeHGlobal(ptr);
	}

	public void Dispose()
	{
		cts.Cancel();
		cts.Dispose();
		httpClient.Dispose();
		manager.Dispose();
	}
}
