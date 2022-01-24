using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionBuilding;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Fluent.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.Wallets;

namespace WalletWasabi.Fluent.ViewModels.Wallets.Send;

public partial class ChangeAvoidanceSuggestionViewModel : SuggestionViewModel
{
	[AutoNotify] private string _amount;
	[AutoNotify] private string _amountFiat;
	[AutoNotify] private string? _differenceFiat;

	public ChangeAvoidanceSuggestionViewModel(decimal originalAmount,
		BuildTransactionResult transactionResult,
		decimal fiatExchangeRate,
		bool isOriginal)
	{
		TransactionResult = transactionResult;

		decimal total = transactionResult.CalculateDestinationAmount().ToDecimal(MoneyUnit.BTC);

		_amountFiat = total.GenerateFiatText(fiatExchangeRate, "USD");

		if (!isOriginal)
		{
			var fiatTotal = total * fiatExchangeRate;
			var fiatOriginal = originalAmount * fiatExchangeRate;
			var fiatDifference = fiatTotal - fiatOriginal;

			_differenceFiat = (fiatDifference > 0
					? $"{fiatDifference.GenerateFiatText("USD")} More"
					: $"{Math.Abs(fiatDifference).GenerateFiatText("USD")} Less")
				.Replace("(", "").Replace(")", "");
		}

		_amount = $"{total} BTC";
	}

	public BuildTransactionResult TransactionResult { get; }

	private static IEnumerable<ChangeAvoidanceSuggestionViewModel> NormalizeSuggestions(
		IEnumerable<ChangeAvoidanceSuggestionViewModel> suggestions,
		ChangeAvoidanceSuggestionViewModel defaultSuggestion)
	{
		var normalized = suggestions
			.OrderBy(x => x.TransactionResult.CalculateDestinationAmount())
			.ToList();

		if (normalized.Count == 3)
		{
			var index = normalized.IndexOf(defaultSuggestion);

			switch (index)
			{
				case 1:
					break;

				case 0:
					normalized = normalized.Take(2).ToList();
					break;

				case 2:
					normalized = normalized.Skip(1).ToList();
					break;
			}
		}

		return normalized;
	}

	public static async IAsyncEnumerable<ChangeAvoidanceSuggestionViewModel> GenerateSuggestionsAsync(
		TransactionInfo transactionInfo, BitcoinAddress destination, Wallet wallet, BuildTransactionResult requestedTransaction, [EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var intent = new PaymentIntent(
			destination,
			MoneyRequest.CreateAllRemaining(subtractFee: true),
			transactionInfo.UserLabels);

		ChangeAvoidanceSuggestionViewModel? smallerSuggestion = null;

		if (requestedTransaction.SpentCoins.Count() > 1)
		{
			var smallerTransaction = await Task.Run(() => wallet.BuildTransaction(
				wallet.Kitchen.SaltSoup(),
				intent,
				FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
				allowUnconfirmed: true,
				requestedTransaction
					.SpentCoins
					.OrderBy(x => x.Amount)
					.Skip(1)
					.Select(x => x.OutPoint),
				payjoinClient: null,
				tryToSign: false));

			smallerSuggestion = new ChangeAvoidanceSuggestionViewModel(
				transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), smallerTransaction,
				wallet.Synchronizer.UsdExchangeRate, false);
		}

		Task<ChangeAvoidanceSuggestionViewModel?> bnbSuggestionTask = Task.Run(() =>
		{
			List<SmartCoin> availableCoins = wallet.Coins.Available().ToList();

			if (ChangelessTransactionCoinSelector.TryGetCoins(availableCoins, transactionInfo.FeeRate, transactionInfo.Amount, out IEnumerable<SmartCoin>? selection, cancellationToken))
			{
				var transaction = TransactionHelpers.BuildChangelessTransaction(
					wallet,
					destination,
					transactionInfo.UserLabels,
					transactionInfo.FeeRate,
					selection,
					tryToSign: false);

				return new ChangeAvoidanceSuggestionViewModel(
					transactionInfo.Amount.ToDecimal(MoneyUnit.BTC),
					transaction,
					wallet.Synchronizer.UsdExchangeRate,
					isOriginal: false);
			}

			return null;
		});

		var largerTransaction = await Task.Run(() => wallet.BuildTransaction(
			wallet.Kitchen.SaltSoup(),
			intent,
			FeeStrategy.CreateFromFeeRate(transactionInfo.FeeRate),
			true,
			requestedTransaction.SpentCoins.Select(x => x.OutPoint),
			payjoinClient: null,
			tryToSign: false));

		var largerSuggestion = new ChangeAvoidanceSuggestionViewModel(
			transactionInfo.Amount.ToDecimal(MoneyUnit.BTC), largerTransaction,
			wallet.Synchronizer.UsdExchangeRate, false);

		if (smallerSuggestion is not null)
		{
			yield return smallerSuggestion;
		}

		if (largerSuggestion is not null)
		{
			yield return largerSuggestion;
		}

		ChangeAvoidanceSuggestionViewModel? bnbSuggestion = await bnbSuggestionTask;

		if (bnbSuggestion is not null)
		{
			yield return bnbSuggestion;
		}
	}
}
