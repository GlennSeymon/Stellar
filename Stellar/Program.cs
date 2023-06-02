using System.Threading.Tasks;

namespace Stellar
{
	class Program
	{
		static async Task Main(string[] args)
		{
			var stellar = new Stellar("YOUR_SECRET_KEY");

			await stellar.SwapWalletAssetsToXLMAndRemoveTrustline();
        }
	}
}
