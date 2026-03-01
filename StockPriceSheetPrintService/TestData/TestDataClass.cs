using StockPrizeSenderService.Models;

namespace StockPrizeSenderService.TestData
{
	public class TestDataClass
	{
		public EodResponse? Test()
		{
			var random = new Random();
			var response = new EodResponse();
			List<decimal> prices = new()
				{
					14.23m, 68.44m, 66.14m
				};
			var eurKursNordnet = 7.4711504651088m;
			var usdKursNordnet = 6.2939990696231m;

			for (int i = 0; i < prices.Count; i++)
			{
				if (i <= 1)
					prices[i] *= eurKursNordnet;
				else if (i == 2)
					prices[i] *= usdKursNordnet;
			}
			int count = 0;
			if (prices.Count != AllTickers.Symbols.Keys.Count)
			{
				return null;
			}
			foreach (var ticker in AllTickers.Symbols.Keys)
			{
				var basePrice = random.Next(50, 500);

				response.Data.Add(new EodDatum
				{
					Symbol = ticker,
					Exchange = "OSE",
					Date = DateTimeOffset.Now.AddDays(-1),
					Open = basePrice,
					High = basePrice + random.Next(1, 20),
					Low = basePrice - random.Next(1, 20),
					Close = prices[count],
					Volume = random.Next(10000, 500000)
				});
				count++;
			}

			return response;
		}
	}
}
