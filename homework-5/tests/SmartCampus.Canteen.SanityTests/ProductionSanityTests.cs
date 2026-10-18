using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartCampus.Canteen.Api.Models;

namespace SmartCampus.Canteen.SanityTests
{
    /// <summary>
    /// Production Sanity-тест релиза v1.9.0 (CAMPUS-104).
    /// Выполняется сразу после выката: проводит тестовую оплату обеда на 1 рубль,
    /// проверяет, что сумма корректно сохраняется и читается через новую колонку Amount,
    /// и ОБЯЗАТЕЛЬНО выполняет очистку: возврат (Refund) и удаление технического чека.
    ///
    /// Запуск против боевого окружения:
    ///   set CAMPUS_SANITY_BASE_URL=https://canteen.campus.example
    ///   dotnet test --filter Category=Sanity
    /// Без переменной окружения тест поднимает сервис в памяти (WebApplicationFactory).
    /// </summary>
    [Trait("Category", "Sanity")]
    public class ProductionSanityTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private const string SanityStudentCard = "SANITY-0000";
        private readonly HttpClient _client;

        public ProductionSanityTests(WebApplicationFactory<Program> factory)
        {
            var baseUrl = Environment.GetEnvironmentVariable("CAMPUS_SANITY_BASE_URL");

            _client = string.IsNullOrWhiteSpace(baseUrl)
                ? factory.CreateClient()
                : new HttpClient { BaseAddress = new Uri(baseUrl) };
        }

        [Fact]
        public async Task Payment_OneRuble_IsStoredInAmount_AndCleanedUp()
        {
            Guid? receiptId = null;

            try
            {
                // Arrange + Act: тестовая оплата на 1 рубль
                var request = new CreateReceiptRequest(SanityStudentCard, "Sanity-check v1.9.0 (CAMPUS-104)", 1.00m, IsSanityCheck: true);
                var createResponse = await _client.PostAsJsonAsync("/api/v1/receipts", request);

                Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                var created = await createResponse.Content.ReadFromJsonAsync<ReceiptResponse>();
                Assert.NotNull(created);
                receiptId = created!.Id;

                // Assert: чек читается, сумма приходит из новой колонки Amount
                var fetched = await _client.GetFromJsonAsync<ReceiptResponse>($"/api/v1/receipts/{receiptId}");
                Assert.NotNull(fetched);
                Assert.Equal(1.00m, fetched!.Amount);
                Assert.Equal("RUB", fetched.Currency);
                Assert.Equal("Paid", fetched.Status);
                Assert.True(fetched.IsSanityCheck);
            }
            finally
            {
                // Cleanup: выполняется ВСЕГДА, даже если проверки выше упали
                if (receiptId is not null)
                {
                    var refund = await _client.PostAsync($"/api/v1/receipts/{receiptId}/refund", null);
                    Assert.Equal(HttpStatusCode.OK, refund.StatusCode);

                    var delete = await _client.DeleteAsync($"/api/v1/receipts/{receiptId}");
                    Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
                }
            }

            // Проверка очистки: технический чек не остался в боевой базе
            var afterCleanup = await _client.GetAsync($"/api/v1/receipts/{receiptId}");
            Assert.Equal(HttpStatusCode.NotFound, afterCleanup.StatusCode);
        }

        [Fact]
        public async Task HealthCheck_ReturnsReleaseVersion()
        {
            var response = await _client.GetAsync("/health");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("1.9.0", body);
        }
    }
}
