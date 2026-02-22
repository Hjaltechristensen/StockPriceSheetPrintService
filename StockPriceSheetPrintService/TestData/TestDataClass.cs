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
					141.28m, 42.06m, 112.34m, 620.5m, 71.59m, 33.77m, 385m, 881m, 1.22m, 14.004m, 67.87m, 65.98m
				};
			var eurKursSaxo = 7.4523799684268m;
			var eurKursNordnet = 7.4711504651088m;
			var usdKursSaxo = 6.2780107003891m;
			var usdKursNordnet = 6.2939990696231m;

			for (int i = 0; i < prices.Count; i++)
			{
				if (i >= 0 && i <= 5)          // indeks 0-5 = EUR
					prices[i] *= eurKursSaxo;
				else if (i >= 6 && i <= 7)     // indeks 6-7 = DKK
					; 
				else if (i == 8)               // indeks 8 = USD
					prices[i] *= usdKursSaxo;
				else if (i >= 9 && i <= 10)    // indeks 9-10 = EUR
					prices[i] *= eurKursNordnet;
				else if (i == 11)              // indeks 11 = USD
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
					Date = DateTime.Now.AddDays(-1),
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
