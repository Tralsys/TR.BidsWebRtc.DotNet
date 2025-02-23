using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using SIPSorcery.Net;
using SIPSorcery.SIP.App;

using TR.BidsWebRtc.Api;
using TR.BidsWebRtc.Api.Model;

namespace TR.BidsWebRtc.WebRtc;

public class RtcConnectionManager : IDisposable
{
	public enum Role
	{
		Provider,
		Subscriber,
	}
	private class RTCConnectionInfo()
	{
		public RTCPeerConnection PeerConnection { get; } = new();

		public Guid? SdpId { get; set; } = null;
		public Guid? ClientId { get; set; } = null;

		public Dictionary<string, RTCDataChannel> DataChannelDict { get; } = [];
		public RTCPeerConnectionState ConnectionState => PeerConnection.connectionState;
	}

	public class OnDataGotEventArgs(
		Guid clientId,
		RTCDataChannel dataChannel,
		byte[] data
	) : EventArgs
	{
		public Guid ClientId { get; } = clientId;
		public RTCDataChannel DataChannel { get; } = dataChannel;
		public byte[] Data { get; } = data;
	}

	private readonly Dictionary<Guid, RTCConnectionInfo> _establishedConnectionDict = [];
	public IReadOnlyDictionary<string, RTCDataChannel> GetDataChannelDict(Guid clientId) => _establishedConnectionDict[clientId].DataChannelDict;

	public event EventHandler<OnDataGotEventArgs>? OnDataGot;

	const string DEFAULT_DATA_CHANNEL_LABEL = "bids-rtc-data-main";
	const int ANSWER_CHECK_INTERVAL_MS = 1000;

	readonly CancellationTokenSource _cts = new();
	readonly Role _role;
	readonly SdpExchangeApi _sdpExchangeApi;

	private RtcConnectionManager(
		Role role,
		SdpExchangeApi sdpExchangeApi
	)
	{
		_role = role;
		_sdpExchangeApi = sdpExchangeApi;
	}

	public static RtcConnectionManager Create(
		Role role,
		SdpExchangeApi sdpExchangeApi
	)
	{
		var manager = new RtcConnectionManager(role, sdpExchangeApi);
		Task.Run(manager._registerOfferAsync);
		return manager;
	}

	public void Dispose()
	{
		_cts.Cancel();
		foreach (var connectionInfo in _establishedConnectionDict.Values)
		{
			connectionInfo.PeerConnection.close();
		}
		_establishedConnectionDict.Clear();
	}

	private RTCConnectionInfo _createRTCPeerConnection(Guid? sdpId = null)
	{
		RTCConnectionInfo connectionInfo = new()
		{
			SdpId = sdpId,
		};
		RTCPeerConnection peerConnection = connectionInfo.PeerConnection;

		var registration = _cts.Token.Register(peerConnection.close);

		peerConnection.onconnectionstatechange += (state) =>
		{
			switch (state)
			{
				case RTCPeerConnectionState.connected:
					registration.Dispose();
					if (connectionInfo.SdpId is not null)
					{
						var sdpId = connectionInfo.SdpId.Value;
						_establishedConnectionDict[sdpId] = connectionInfo;
					}
					break;
				case RTCPeerConnectionState.disconnected:
#if DEBUG
					Console.WriteLine($"Disconnected: {connectionInfo.SdpId}");
#endif
					if (connectionInfo.SdpId is not null)
					{
						var sdpId = connectionInfo.SdpId.Value;
						_establishedConnectionDict.Remove(sdpId);
					}
					break;
				case RTCPeerConnectionState.failed:
#if DEBUG
					Console.WriteLine($"Failed: {connectionInfo.SdpId}");
#endif
					peerConnection.close();
					if (connectionInfo.SdpId is not null)
					{
						var sdpId = connectionInfo.SdpId.Value;
						_establishedConnectionDict.Remove(sdpId);
					}
					break;
				case RTCPeerConnectionState.closed:
#if DEBUG
					Console.WriteLine($"Closed: {connectionInfo.SdpId}");
#endif
					if (connectionInfo.SdpId is not null)
					{
						var sdpId = connectionInfo.SdpId.Value;
						_establishedConnectionDict.Remove(sdpId);
					}
					break;
			}
		};

		peerConnection.ondatachannel += (e) =>
		{
#if DEBUG
			Console.WriteLine($"DataChannel received: {e.label}[{_role}]@{connectionInfo.SdpId}");
#endif
			_setupDataChannel(connectionInfo, e);
			if (e.IsOpened)
			{
				connectionInfo.DataChannelDict[e.label] = e;
			}
		};

		return connectionInfo;
	}

	private async Task _registerOfferAsync()
	{
		try
		{
			RTCConnectionInfo connectionInfo = _createRTCPeerConnection();
			RTCPeerConnection peerConnection = connectionInfo.PeerConnection;
			RTCDataChannel dataChannel = await peerConnection.createDataChannel(DEFAULT_DATA_CHANNEL_LABEL);
			_setupDataChannel(connectionInfo, dataChannel);

			void onConnectionStateChange(RTCPeerConnectionState state)
			{
				if (state != RTCPeerConnectionState.connected)
				{
					return;
				}
				peerConnection.onconnectionstatechange -= onConnectionStateChange;
				Task.Run(_registerOfferAsync);
			}
			peerConnection.onconnectionstatechange += onConnectionStateChange;

			RTCSessionDescriptionInit offer = peerConnection.createOffer();
			await peerConnection.setLocalDescription(offer);

			var offerRegisterResult = await _sdpExchangeApi.RegisterOfferAsync(new RegisterOfferParams(
				role: RoleToString(_role),
				rawOffer: offer.sdp,
				establishedClients: [.. _establishedConnectionDict.Values.Where(x => x.ClientId is not null).Select(x => x.ClientId!.Value)]
			));
			connectionInfo.SdpId = offerRegisterResult.RegisteredOffer.SdpId;
#if DEBUG
			Console.WriteLine($"Offer registered: {connectionInfo.SdpId}");
#endif
			if (offerRegisterResult.ReceivedOfferArray is not null)
			{
				SDPAnswerInfo[] answerList = await Task.WhenAll(offerRegisterResult.ReceivedOfferArray.Select(_onOfferReceivedAsync)) ?? [];
				await _sdpExchangeApi.RegisterAnswerAsync(answerList);
			}
			while (!_cts.Token.IsCancellationRequested)
			{
				var answerRes = await _sdpExchangeApi.GetAnswerAsync(connectionInfo.SdpId.Value);
				if (_cts.Token.IsCancellationRequested)
				{
					dataChannel.close();
					peerConnection.Dispose();
					return;
				}
				if (answerRes is null)
				{
					await Task.Delay(ANSWER_CHECK_INTERVAL_MS);
					continue;
				}
				peerConnection.SetRemoteDescription(SdpType.answer, SDP.ParseSDPDescription(answerRes.RawAnswer));
				_establishedConnectionDict[connectionInfo.SdpId.Value] = connectionInfo;
				break;
			}
		}
		catch (Exception e)
		{
			// TODO: 良い感じにエラー処理する
#if DEBUG
			Console.WriteLine($"Error: {e}");
#endif
		}
	}

	private void _setupDataChannel(RTCConnectionInfo connectionInfo, RTCDataChannel dataChannel)
	{
		connectionInfo.DataChannelDict[dataChannel.label] = dataChannel;
		dataChannel.onerror += (e) => _onDataChannelError(connectionInfo, dataChannel, e);
		dataChannel.onopen += () =>
		{
#if DEBUG
			Console.WriteLine($"DataChannel opened for {connectionInfo.SdpId}[{_role}]@{dataChannel.label}");
			dataChannel.send($"Hello, world! from {connectionInfo.SdpId}[{_role}]@{dataChannel.label}");
#endif
			connectionInfo.DataChannelDict[dataChannel.label] = dataChannel;
		};
		dataChannel.onmessage += (dc, protocol, data) =>
		{
#if DEBUG
			Console.WriteLine($"DataChannel message from {connectionInfo.SdpId}[{_role}]@{dataChannel.label}: {data} (protocol: {protocol}, length: {data.Length})");
#endif
			OnDataGot?.Invoke(this, new OnDataGotEventArgs(connectionInfo.ClientId!.Value, dataChannel, data));
		};
		dataChannel.onclose += () =>
		{
#if DEBUG
			Console.WriteLine($"DataChannel closed for {connectionInfo.SdpId}[{_role}]@{dataChannel.label}");
#endif
			connectionInfo.DataChannelDict.Remove(dataChannel.label);
		};
	}

	private void _onDataChannelError(RTCConnectionInfo connectionInfo, RTCDataChannel dc, string e)
	{
		string dcLabel = dc.label;
#if DEBUG
		Console.WriteLine($"DataChannel[{dcLabel}@{connectionInfo.SdpId}] error: {e}");
#endif
		connectionInfo.DataChannelDict.Remove(dcLabel);
		dc.close();
	}

	private async Task<SDPAnswerInfo> _onOfferReceivedAsync(SDPOfferInfo offerInfo)
	{
		var connectionInfo = _createRTCPeerConnection(offerInfo.SdpId);
		RTCPeerConnection peerConnection = connectionInfo.PeerConnection;

		peerConnection.SetRemoteDescription(SdpType.offer, SDP.ParseSDPDescription(offerInfo.RawOffer));
		var answer = peerConnection.createAnswer();
		await peerConnection.setLocalDescription(answer);
		return new(offerInfo.SdpId, _sdpExchangeApi.ClientId, answer.sdp);
	}

	public void BroadcastMessage(byte[] message)
	{
		foreach (var connectionInfo in _establishedConnectionDict.Values)
		{
			foreach (var dataChannel in connectionInfo.DataChannelDict.Values)
			{
				dataChannel.send(message);
			}
		}
	}

	private static Role GetRoleFromString(string role)
		=> role.ToLower() switch
		{
			"provider" => Role.Provider,
			"subscriber" => Role.Subscriber,
			_ => throw new ArgumentException($"Invalid role: {role}"),
		};
	private static string RoleToString(Role role)
		=> role switch
		{
			Role.Provider => "provider",
			Role.Subscriber => "subscriber",
			_ => throw new ArgumentException($"Invalid role: {role}"),
		};

}
