using System.Threading.Tasks;
using MedicationManagementApp.DTOs;
using MedicationManagementApp.Exceptions;
using MedicationManagementApp.Models;
using MedicationManagementApp.Repositories;
using MedicationManagementApp.Services;
using Moq;
using Shouldly;
using Xunit;

public class MedicationRequestServiceTests
{
    private readonly Mock<IMedicationRequestRepository> _repoMock;
    private readonly Mock<IEmailSender> _emailMock;
    private readonly MedicationRequestService _service;

    public MedicationRequestServiceTests()
    {
        _repoMock = new Mock<IMedicationRequestRepository>();
        _emailMock = new Mock<IEmailSender>();
        _service = new MedicationRequestService(_repoMock.Object, _emailMock.Object);
    }

    private MedicationRequest CreateRequest(
        string medName = "Aspirin",
        int medStock = 10,
        int requestedQty = 5)
    {
        return new MedicationRequest
        {
            Id = 1,
            PatientName = "John Doe",
            PatientEmail = "john@example.com",
            Quantity = requestedQty,
            Medication = new Medication
            {
                Name = medName,
                Quantity = medStock
            }
        };
    }

    [Fact]
    public async Task ProcessMedicationRequest_ShouldThrowNotFound_WhenRequestDoesNotExist()
    {
        // Arrange
        _repoMock.Setup(r => r.GetOne(It.IsAny<int>()))
                 .ReturnsAsync((MedicationRequest)null);

        // Act
        var act = async () => await _service.ProcessMedicationRequest(1);

        // Assert
        await act.ShouldThrowAsync<MedicationRequestNotFoundException>();
    }

    [Fact]
    public async Task ProcessMedicationRequest_ShouldThrowOutOfStock_WhenMedicationQuantityIsZero()
    {
        // Arrange
        var request = CreateRequest(medStock: 0, requestedQty: 5);
        _repoMock.Setup(r => r.GetOne(1)).ReturnsAsync(request);

        // Act
        var act = async () => await _service.ProcessMedicationRequest(1);

        // Assert
        await act.ShouldThrowAsync<OutOfStockException>();
    }

    [Fact]
    public async Task ProcessMedicationRequest_ShouldThrowInsufficientStock_WhenMedicationQuantityIsLessThanRequested()
    {
        // Arrange
        var request = CreateRequest(medStock: 3, requestedQty: 5);
        _repoMock.Setup(r => r.GetOne(1)).ReturnsAsync(request);

        // Act
        var act = async () => await _service.ProcessMedicationRequest(1);

        // Assert
        await act.ShouldThrowAsync<InsufficientStockException>();
    }

    [Fact]
    public async Task ProcessMedicationRequest_ShouldSendEmailAndReturnDto_WhenValid()
    {
        // Arrange
        var request = CreateRequest(medStock: 10, requestedQty: 5);
        _repoMock.Setup(r => r.GetOne(1)).ReturnsAsync(request);

        // Act
        var result = await _service.ProcessMedicationRequest(1);

        // Assert
        result.ShouldNotBeNull();
        result.MedicationName.ShouldBe("Aspirin");
        result.Message.ShouldContain("successfully processed");

        _emailMock.Verify(e => e.SendEmail(
            request.PatientEmail,
            "New Medication Request",
            It.Is<string>(s => s.Contains("Aspirin"))),
            Times.Once);
    }
}
