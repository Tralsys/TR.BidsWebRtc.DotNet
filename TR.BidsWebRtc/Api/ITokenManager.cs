using System.Threading.Tasks;

namespace TR.BidsWebRtc.Api;

public interface ITokenManager
{
	Task<string> GetTokenAsync();
}
