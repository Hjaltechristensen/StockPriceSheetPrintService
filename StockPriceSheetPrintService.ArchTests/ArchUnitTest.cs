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

		[Fact]
		public void Inbound_ShouldNotDependOn_Ports_Outbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Inbound")
				.ShouldNot().HaveDependencyOn(
					"StockPriceSheetPrintService.Service.Ports.Outbound")
				.GetResult();

			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void Outbound_ShouldNotDependOn_Ports_Inbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Outbound")
				.ShouldNot().HaveDependencyOn(
					"StockPriceSheetPrintService.Service.Ports.Inbound")
				.GetResult();
			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void ServiceApplication_ShouldNotDependOn_Dto()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Service.Application")
				.ShouldNot().HaveDependencyOnAny(
					"StockPriceSheetPrintService.Outbound.Dto",
					"StockPriceSheetPrintService.Inbound.Dto")
				.GetResult();
			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void ServiceModels_ShouldNotDependOn_Adapters()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Service.Models")
				.ShouldNot().HaveDependencyOnAny(
					"StockPriceSheetPrintService.Outbound",
					"StockPriceSheetPrintService.Inbound")
				.GetResult();
			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void OutboundDto_ShouldNotDependOn_ServiceOrInbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Outbound.Dto")
				.ShouldNot().HaveDependencyOnAny(
					"StockPriceSheetPrintService.Service",
					"StockPriceSheetPrintService.Inbound")
				.GetResult();
			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void InboundDto_ShouldNotDependOn_ServiceOrOutbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Inbound.Dto")
				.ShouldNot().HaveDependencyOnAny(
					"StockPriceSheetPrintService.Service",
					"StockPriceSheetPrintService.Outbound")
				.GetResult();
			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void InboundMappers_ShouldNotDependOn_Outbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Inbound.Mappers")
				.ShouldNot().HaveDependencyOn("StockPriceSheetPrintService.Outbound")
				.GetResult();
			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void OutboundMappers_ShouldNotDependOn_Inbound()
		{
			var result = Types.InAssembly(assembly)
				.That().ResideInNamespace("StockPriceSheetPrintService.Outbound.Mappers")
				.ShouldNot().HaveDependencyOn("StockPriceSheetPrintService.Inbound")
				.GetResult();
			Assert.True(result.IsSuccessful);
		}

		[Fact]
		public void MapperClasses_ShouldResideInMapperNamespaces()
		{
			var result = Types.InAssembly(assembly)
				.That().HaveNameEndingWith("Mapper")
				.Should().ResideInNamespaceStartingWith("StockPriceSheetPrintService.Inbound.Mappers")
				.Or().ResideInNamespaceStartingWith("StockPriceSheetPrintService.Outbound.Mappers")
				.GetResult();
			Assert.True(result.IsSuccessful);
		}
	}
}
