using TR.BidsWebRtc.Api;

namespace TR.BidsWebRtc.Tests.Api;

public class FirebaseAuthTokenManagerTests
{
	const string TOKEN_REFRESH_URL = "http://127.0.0.1:9099/securetoken.googleapis.com/v1/token?key=dummy";
	const string REFRESH_TOKEN = "eyJfQXV0aEVtdWxhdG9yUmVmcmVzaFRva2VuIjoiRE8gTk9UIE1PRElGWSIsImxvY2FsSWQiOiI2V1c1N0J0UGJJcXJ3Y3ljOXRUNEdybGQyY2VEIiwicHJvdmlkZXIiOiJwYXNzd29yZCIsImV4dHJhQ2xhaW1zIjp7fSwicHJvamVjdElkIjoiYmlkcy1ydGMifQ==";
	const string ACCESS_TOKEN = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJuYW1lIjoiVGVzdFVzZXIiLCJwaWN0dXJlIjoiIiwiZW1haWwiOiJhQGEudGVzdCIsImVtYWlsX3ZlcmlmaWVkIjpmYWxzZSwiYXV0aF90aW1lIjoxNzQwMDYzMzk0LCJ1c2VyX2lkIjoiNldXNTdCdFBiSXFyd2N5Yzl0VDRHcmxkMmNlRCIsImZpcmViYXNlIjp7ImlkZW50aXRpZXMiOnsiZW1haWwiOlsiYUBhLnRlc3QiXX0sInNpZ25faW5fcHJvdmlkZXIiOiJwYXNzd29yZCJ9LCJpYXQiOjE3NDAwNjMzOTQsImV4cCI6MTc0MDA2Njk5NCwiYXVkIjoiYmlkcy1ydGMiLCJpc3MiOiJodHRwczovL3NlY3VyZXRva2VuLmdvb2dsZS5jb20vYmlkcy1ydGMiLCJzdWIiOiI2V1c1N0J0UGJJcXJ3Y3ljOXRUNEdybGQyY2VEIn0.";

	[Test]
	public async Task TestWithoutAccessTokenAsync()
	{
		var manager = new FirebaseAuthTokenManager(
			new HttpClient(),
			"",
			REFRESH_TOKEN
		);
		manager.SetTokenRefreshUrl(TOKEN_REFRESH_URL);
		var token = await manager.GetTokenAsync();
		Assert.That(token, Is.Not.EqualTo(ACCESS_TOKEN));

		var token2 = await manager.GetTokenAsync();
		Assert.That(token2, Is.EqualTo(token));
	}

	[Test]
	public async Task TestWithAccessTokenAsync()
	{
		var manager = new FirebaseAuthTokenManager(
			new HttpClient(),
			"",
			REFRESH_TOKEN,
			ACCESS_TOKEN
		);
		manager.SetTokenRefreshUrl(TOKEN_REFRESH_URL);
		var token = await manager.GetTokenAsync();
		Assert.That(token, Is.Not.EqualTo(ACCESS_TOKEN));

		var token2 = await manager.GetTokenAsync();
		Assert.That(token2, Is.EqualTo(token));
	}
}
