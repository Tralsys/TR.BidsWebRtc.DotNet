using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TR.BidsWebRtc.Api.Model;

public class RegisterOfferParams(
	string role,
	string rawOffer,
	Guid[] establishedClients
)
{
	public string Role { get; } = role;
	public string RawOffer { get; } = rawOffer;
	public Guid[] EstablishedClients { get; } = establishedClients;

	public void WriteJson(Stream stream)
	{
		using var writer = new Utf8JsonWriter(stream);
		writer.WriteStartObject();
		writer.WriteString("role", Role);
		writer.WriteBase64String("offer", Encoding.UTF8.GetBytes(RawOffer));
		writer.WriteStartArray("established_clients");
		foreach (var client in EstablishedClients)
		{
			writer.WriteStringValue(client);
		}
		writer.WriteEndArray();
		writer.WriteEndObject();
		writer.Flush();
	}
}
