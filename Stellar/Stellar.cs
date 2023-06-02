using stellar_dotnet_sdk;
using stellar_dotnet_sdk.responses;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Stellar
{
	internal class Stellar
	{
		private string secretKey;

        public Stellar(string secretKey)
        {
			this.secretKey = secretKey;    
        }
        public async Task SwapWalletAssetsToXLMAndRemoveTrustline()
        {
			Network.UsePublicNetwork(); // Use Testnet, change to Network.UsePublicNetwork() for Mainnet
			var serverURI = "https://horizon.stellar.org";
			var server = new Server(serverURI);

			var keypair = KeyPair.FromSecretSeed(this.secretKey);
			var sourceAccount = await server.Accounts.Account(keypair.AccountId);

			Console.WriteLine($"Working with wallet {sourceAccount.AccountId}.");

			var transactionResponse = await server.Transactions.ForAccount(keypair.AccountId).Execute();
            var transactionsArray = transactionResponse.Records.ToArray();

			foreach (var assetBalance in sourceAccount.Balances.Where(x => x.AssetCode.ToUpper() != "OPRX"))
				await SwapAssetToXLMAndRemoveTrustline(server, keypair, assetBalance);

			Console.WriteLine($"Completed.");
		}

		private static async Task SwapAssetToXLMAndRemoveTrustline(Server server, KeyPair keypair, Balance assetBalance)
		{
			var sourceAsset = Asset.CreateNonNativeAsset(assetBalance.AssetCode, assetBalance.AssetIssuer);
			var sourceAssetAmount = Decimal.Parse(assetBalance.BalanceString);

			Console.WriteLine($"*** Asset: {assetBalance.AssetCode}:{assetBalance.AssetIssuer}, Balance: {((long)sourceAssetAmount)}.");

			var sourceAccount = await server.Accounts.Account(keypair.AccountId); // Refresh to get the latest sequence number

			if (sourceAssetAmount == 0)
			{
				Console.WriteLine($"Balance is 0. Skipping swap. Removing trustline.");

				try
				{
					await RemoveTrustline(server, sourceAccount, keypair, sourceAsset);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Remove trustline failed. Error: " + ex.Message);
				}
			}
			else
			{
				try
				{
					await SwapAssetToXlmAndRemoveTrustlineAsync(sourceAssetAmount, server, sourceAccount, keypair, sourceAsset);
				}
				catch (Exception ex)
				{
					Console.WriteLine("Asset swap and remove trustline failed. Error: " + ex.Message);
				}
			}
		}

		public static async Task SwapAssetToXlmAndRemoveTrustlineAsync(decimal sourceAssetAmount, Server server, AccountResponse sourceAccount, KeyPair keypair, AssetTypeCreditAlphaNum sourceAsset)
		{
			var destinationPaymentOperation = new PaymentOperation.Builder(keypair, new AssetTypeNative(), sourceAssetAmount.ToString()).Build();

			var pathPaymentOperation = new PathPaymentStrictSendOperation.Builder(sourceAsset, sourceAssetAmount.ToString(), keypair, new AssetTypeNative(), "0.000001").Build();

			// Setting the limit to "0" removes the trustline.
			var removeTrustlineOperation = new ChangeTrustOperation.Builder(stellar_dotnet_sdk.ChangeTrustAsset.Create(sourceAsset), "0").Build();

			// https://stellar.org/developers-blog/transaction-submission-timeouts-and-dynamic-fees-faq
			var transaction = new TransactionBuilder(sourceAccount)
				.AddOperation(destinationPaymentOperation)
				.AddOperation(pathPaymentOperation)
				.AddOperation(removeTrustlineOperation)
				.AddMemo(stellar_dotnet_sdk.Memo.Text("Swap Asset to XLM"))
				.SetFee(100) // Adjust the fee as per your requirements. Unit: stroops. https://developers.stellar.org/docs/encyclopedia/fees-surge-pricing-fee-strategies
				.Build();

			transaction.Sign(keypair);

			var response = await server.SubmitTransaction(transaction);
			
			if (response.IsSuccess())
				Console.WriteLine("Asset swap to XLM and remove trustline successful!");
			else
			{
				Console.WriteLine($"Asset swap to XLM and remove trustline failed!. {response.Result}");

				DisplayOperationResultCodes(response);
			}
		}

		private static async Task RemoveTrustline(Server server, AccountResponse sourceAccount, KeyPair keypair, AssetTypeCreditAlphaNum sourceAsset)
		{
			// Setting the limit to "0" removes the trustline.
			var removeTrustlineOperation = new ChangeTrustOperation.Builder(stellar_dotnet_sdk.ChangeTrustAsset.Create(sourceAsset),"0").Build();

			var transaction = new TransactionBuilder(sourceAccount)
				.AddOperation(removeTrustlineOperation)
				.Build();

			transaction.Sign(keypair);

			var response = await server.SubmitTransaction(transaction);

			if (response.IsSuccess())
				Console.WriteLine("Asset trustline removed");
			else
			{ 
				Console.WriteLine($"Asset trustline remove failed!. {response.Result}");

				DisplayOperationResultCodes(response);
			}
		}
		private static void DisplayOperationResultCodes(SubmitTransactionResponse response)
		{
			var operationResultCodes = response.SubmitTransactionResponseExtras.ExtrasResultCodes.OperationsResultCodes;

			foreach (var operationResultCode in operationResultCodes.Where(x => x != "op_success"))
				Console.WriteLine($"Operation Result code: {operationResultCode}");
		}
	}
}