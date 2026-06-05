using NetArchTest.Rules;
using System.Reflection;

namespace StockPriceSheetPrintService.ArchTests
{
	public class ArchUnitTest
	{
		private readonly Assembly assembly = Assembly.Load("StockPriceSheetPrintService");

		[Fact]
		public void Inbound_ShouldNotDependOn_ServiceApplication_Or_Outbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Inbound")
				.ShouldNot().HaveDependencyOnAny(
					"StockPriceSheetPrintService.Service.Application",
					"StockPriceSheetPrintService.Outbound")
				.GetResult();

			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void Service_ShouldNotDependOn_Outbound_And_Inbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Service")
				.ShouldNot().HaveDependencyOnAny(
					"StockPriceSheetPrintService.Outbound",
					"StockPriceSheetPrintService.Inbound")
				.GetResult();

			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void Outbound_ShouldNotDependOn_ServiceApplication_Or_Inbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Outbound")
				.ShouldNot().HaveDependencyOnAny(
					"StockPriceSheetPrintService.Service.Application",
					"StockPriceSheetPrintService.Inbound")
				.GetResult();

			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void ImplClasses_ShouldResideInApplication_Or_Outbound()
		{
			var result = Types.InAssembly(assembly)
				.That().HaveNameEndingWith("Impl")
				.Should().ResideInNamespaceStartingWith("StockPriceSheetPrintService.Service.Application")
				.Or().ResideInNamespaceStartingWith("StockPriceSheetPrintService.Outbound")
				.GetResult();

			Assert.True(result.IsSuccessful);
		}
	}
}
