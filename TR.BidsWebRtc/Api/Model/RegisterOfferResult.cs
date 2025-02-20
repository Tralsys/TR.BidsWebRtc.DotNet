using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace TR.BidsWebRtc.Api.Model;

public class RegisterOfferResult(
	SDPOfferInfo registeredOffer,
	SDPOfferInfo[] receivedOfferArray
)
{
	public SDPOfferInfo RegisteredOffer { get; } = registeredOffer;
	public SDPOfferInfo[] ReceivedOfferArray { get; } = receivedOfferArray;

	public static RegisterOfferResult FromJson(Stream stream)
	{
		using var doc = JsonDocument.Parse(stream);
		return FromJson(doc.RootElement);
	}
	public static async Task<RegisterOfferResult> FromJsonAsync(Stream stream)
	{
		using var doc = await JsonDocument.ParseAsync(stream);
		return FromJson(doc.RootElement);
	}
	public static RegisterOfferResult FromJson(JsonElement root)
	{
		var registeredOffer = SDPOfferInfo.FromJson(root.GetProperty("registered_offer"));
		var receivedOfferArray = root.GetProperty("received_offer_array").EnumerateArray().Select(SDPOfferInfo.FromJson).ToArray();

		return new RegisterOfferResult(
			registeredOffer: registeredOffer,
			receivedOfferArray: receivedOfferArray
		);
	}
}
