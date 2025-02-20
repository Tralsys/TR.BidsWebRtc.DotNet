using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TR.BidsWebRtc.Api.Model;

public class SDPOfferInfo(
	Guid SdpId,
	Guid OfferClientId,
	string OfferClientRole,
	DateTime CreatedAt,
	string RawOffer
)
{
	public Guid SdpId { get; } = SdpId;
	public Guid OfferClientId { get; } = OfferClientId;
	public string OfferClientRole { get; } = OfferClientRole;
	public DateTime CreatedAt { get; } = CreatedAt;
	public string RawOffer { get; } = RawOffer;

	public static SDPOfferInfo FromJson(Stream stream)
	{
		using var doc = JsonDocument.Parse(stream);
		return FromJson(doc.RootElement);
	}
	public static async Task<SDPOfferInfo> FromJsonAsync(Stream stream)
	{
		using var doc = await JsonDocument.ParseAsync(stream);
		return FromJson(doc.RootElement);
	}
	public static SDPOfferInfo FromJson(JsonElement root)
	{
		var offerBytes = root.GetProperty("offer").GetBytesFromBase64() ?? throw new JsonException("Missing required property 'answer'");

		return new SDPOfferInfo(
			SdpId: root.GetProperty("sdp_id").GetGuid(),
			OfferClientId: root.GetProperty("offer_client_id").GetGuid(),
			OfferClientRole: root.GetProperty("offer_client_role").GetString() ?? throw new JsonException("Missing required property 'offer_client_role'"),
			CreatedAt: root.GetProperty("created_at").GetDateTime(),
			RawOffer: Encoding.UTF8.GetString(offerBytes)
		);
	}
}
