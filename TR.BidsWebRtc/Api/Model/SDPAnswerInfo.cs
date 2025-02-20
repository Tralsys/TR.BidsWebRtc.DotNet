using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TR.BidsWebRtc.Api.Model;

public class SDPAnswerInfo(
	Guid SdpId,
	Guid AnswerClientId,
	string RawAnswer
)
{
	public Guid SdpId { get; } = SdpId;
	public Guid AnswerClientId { get; } = AnswerClientId;
	public string RawAnswer { get; } = RawAnswer;

	public void WriteJson(Stream stream)
	{
		using var writer = new Utf8JsonWriter(stream);
		WriteJson(writer);
		writer.Flush();
	}
	public void WriteJson(Utf8JsonWriter writer)
	{
		writer.WriteStartObject();
		writer.WriteString("sdp_id", SdpId);
		// readonly
		// writer.WriteString("answer_client_id", AnswerClientId);
		writer.WriteString("answer", RawAnswer);
		writer.WriteEndObject();
	}

	public static void WriteArrayJson(Stream stream, SDPAnswerInfo[] answerInfoArray)
	{
		using var writer = new Utf8JsonWriter(stream);
		writer.WriteStartArray();
		foreach (var answerInfo in answerInfoArray)
		{
			answerInfo.WriteJson(writer);
		}
		writer.WriteEndArray();
		writer.Flush();
	}

	public static SDPAnswerInfo FromJson(Stream stream)
	{
		using var doc = JsonDocument.Parse(stream);
		var root = doc.RootElement;
		var answerBytes = root.GetProperty("answer").GetBytesFromBase64() ?? throw new JsonException("Missing required property 'answer'");

		return new SDPAnswerInfo(
			SdpId: root.GetProperty("sdp_id").GetGuid(),
			AnswerClientId: root.GetProperty("answer_client_id").GetGuid(),
			RawAnswer: Encoding.UTF8.GetString(answerBytes)
		);
	}
}
