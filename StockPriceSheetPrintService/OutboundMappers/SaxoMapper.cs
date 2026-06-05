using StockPriceSheetPrintService.OutboundDto.Saxo;
using StockPriceSheetPrintService.OutboundDto.Saxo.InstrumentDetails;
using StockPriceSheetPrintService.OutboundDto.Saxo.Transactions;
using StockPriceSheetPrintService.Service.Models;

namespace StockPriceSheetPrintService.OutboundMappers
{
	public static class SaxoMapper
	{
		public static OAuthTokens ToOAuthTokens(SaxoTokenResult dto) =>
			new(dto.AccessToken, dto.RefreshToken);

		public static AccountBalance ToAccountBalance(SaxoBalanceResponse dto) =>
			new(dto.TotalValue, dto.CashBalance, dto.Currency, dto.CalculationAssetValue);

		public static Transfer ToTransfer(SaxoTransaction dto) =>
			new(dto.BookingId, dto.Amount);

		public static List<Transfer> ToTransfers(SaxoTransactionsResponse dto) =>
			dto.Data.Select(ToTransfer).ToList();

		public static Instrument ToInstrument(SaxoInstrument dto) => new()
		{
			Uic            = dto.Uic,
			Symbol         = dto.Symbol,
			Description    = dto.Description,
			AssetType      = dto.AssetType,
			Currency       = dto.CurrencyCode,
			ExchangeId      = dto.Exchange?.ExchangeId ?? string.Empty,
			ExchangeCountry = dto.Exchange?.CountryCode ?? string.Empty,
			ExchangeName    = dto.Exchange?.Name ?? string.Empty,
		};

		public static SaxoInstrument ToDto(Instrument domain) => new()
		{
			Uic          = domain.Uic,
			Symbol       = domain.Symbol,
			Description  = domain.Description,
			AssetType    = domain.AssetType,
			CurrencyCode = domain.Currency,
			Exchange     = new ExchangeDto
			{
				ExchangeId  = domain.ExchangeId,
				CountryCode = domain.ExchangeCountry,
				Name        = domain.ExchangeName,
			},
		};
	}
}
