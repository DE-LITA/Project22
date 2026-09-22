using OrderIntake;

namespace OrderIntake.Tests;

public class OrderIntakeServiceTests
{
    private readonly OrderIntakeService _service = new();

    // 1. Accepted order:
    // - Mixed-case specimenType and priority are normalized
    // - Unknown fields are ignored
    // - Accepted order contains correct values
    [Fact]
    public void AcceptedOrder_NormalizesValues_AndIgnoresUnknownFields()
    {
        var json = """
        {
            "orderId": "ORD-1005",
            "patientId": "PAT-505",
            "specimenId": "SP-9005",
            "specimenType": "bLoOd",
            "priority": "uRgEnT",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose", "CompleteBloodCount"],
            "senderNote": "ignore me"
        }
        """;

        var result = _service.Process(json);

        Assert.Equal("Accepted", result.Status);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Order);

        Assert.Equal("ORD-1005", result.Order!.OrderId);
        Assert.Equal("PAT-505", result.Order.PatientId);
        Assert.Equal("SP-9005", result.Order.SpecimenId);
        Assert.Equal("Blood", result.Order.SpecimenType);
        Assert.Equal("Urgent", result.Order.Priority);
        Assert.Equal(new DateTime(2026, 9, 18), result.Order.CollectionDate);

        Assert.Equal(
            new[] { "Glucose", "CompleteBloodCount" },
            result.Order.RequestedTests);
    }

    // 2. All validation errors should be returned together.
    [Fact]
    public void InvalidOrder_ReturnsAllValidationErrors()
    {
        var json = """
        {
            "orderId": "   ",
            "patientId": "PAT-505",
            "specimenId": "SP-9005",
            "specimenType": "Plasma",
            "priority": "Emergency",
            "collectionDate": "2026-02-30",
            "requestedTests": []
        }
        """;

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);

        var actualErrors = result.Errors
            .Select(error => $"{error.Field}:{error.Code}")
            .ToHashSet();

        var expectedErrors = new HashSet<string>
        {
            "orderId:REQUIRED",
            "specimenType:INVALID_VALUE",
            "priority:INVALID_VALUE",
            "collectionDate:INVALID_FORMAT",
            "requestedTests:REQUIRED"
        };

        Assert.Equal(expectedErrors, actualErrors);
    }

    // 3. Exactly 20 characters is valid.
    [Fact]
    public void OrderId_WithExactly20Characters_IsAccepted()
    {
        var orderId = new string('A', 20);

        var json = CreateValidOrderJson(
            orderId: orderId);

        var result = _service.Process(json);

        Assert.Equal("Accepted", result.Status);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Order);
        Assert.Equal(orderId, result.Order!.OrderId);
    }

    // 4. 21 characters should be rejected.
    [Fact]
    public void OrderId_With21Characters_ReturnsMaxLengthError()
    {
        var orderId = new string('A', 21);

        var json = CreateValidOrderJson(
            orderId: orderId);

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);

        Assert.Contains(
            result.Errors,
            error => error.Field == "orderId" &&
                     error.Code == "MAX_LENGTH");
    }

    // 5. Invalid calendar date and invalid format.
    [Fact]
    public void InvalidCollectionDate_IsRejected()
    {
        var invalidDateJson = CreateValidOrderJson(
            collectionDate: "2026-02-30");

        var invalidFormatJson = CreateValidOrderJson(
            collectionDate: "20-09-2026");

        var invalidDateResult = _service.Process(invalidDateJson);
        var invalidFormatResult = _service.Process(invalidFormatJson);

        Assert.Contains(
            invalidDateResult.Errors,
            error => error.Field == "collectionDate" &&
                     error.Code == "INVALID_FORMAT");

        Assert.Contains(
            invalidFormatResult.Errors,
            error => error.Field == "collectionDate" &&
                     error.Code == "INVALID_FORMAT");
    }

    // 6. A future date should be rejected.
    [Fact]
    public void FutureCollectionDate_IsRejected()
    {
        var futureDate = DateTime.Today
            .AddDays(1)
            .ToString("yyyy-MM-dd");

        var json = CreateValidOrderJson(
            collectionDate: futureDate);

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);

        Assert.Contains(
            result.Errors,
            error => error.Field == "collectionDate" &&
                     error.Code == "FUTURE_DATE");
    }

    // 7. Empty requestedTests list is invalid.
    [Fact]
    public void EmptyRequestedTests_IsRejected()
    {
        var json = CreateValidOrderJson(
            requestedTests: Array.Empty<string>());

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);

        Assert.Contains(
            result.Errors,
            error => error.Field == "requestedTests" &&
                     error.Code == "REQUIRED");
    }

    // 8. Test names differing only by case are duplicates.
    [Fact]
    public void RequestedTests_DifferingOnlyByCase_AreDuplicates()
    {
        var json = CreateValidOrderJson(
            requestedTests: new[] { "Glucose", "glucose" });

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);

        Assert.Contains(
            result.Errors,
            error => error.Field == "requestedTests" &&
                     error.Code == "DUPLICATE");
    }

    // 9. Broken JSON should return exactly one MALFORMED_INPUT error.
    [Fact]
    public void MalformedJson_ReturnsSingleMalformedInputError()
    {
        var json = """{"orderId": """;

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    // 10. Empty JSON object is valid JSON,
    // so it should return field-level validation errors.
    [Fact]
    public void EmptyObject_ReturnsFieldValidationErrors()
    {
        var result = _service.Process("{}");

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);

        Assert.Contains(
            result.Errors,
            error => error.Field == "orderId" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "patientId" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "specimenId" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "specimenType" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "priority" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "collectionDate" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "requestedTests" &&
                     error.Code == "REQUIRED");
    }

    // 11. Null input should not throw an exception.
    [Fact]
    public void NullInput_ReturnsSingleMalformedInputError()
    {
        var result = _service.Process(null);

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    // 12. Whitespace input should not throw an exception.
    [Fact]
    public void WhitespaceInput_ReturnsSingleMalformedInputError()
    {
        var result = _service.Process("   ");

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    // 13. Top-level JSON array is malformed input.
    [Fact]
    public void TopLevelArray_ReturnsSingleMalformedInputError()
    {
        var result = _service.Process("[]");

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    // 14. Top-level JSON string is malformed input.
    [Fact]
    public void TopLevelString_ReturnsSingleMalformedInputError()
    {
        var result = _service.Process("\"hello\"");

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    // 15. Recognized field with incompatible JSON type is malformed input.
    [Fact]
    public void IncompatibleRecognizedFieldType_ReturnsSingleMalformedInputError()
    {
        var json = """
        {
            "orderId": 123
        }
        """;

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    // 16. Empty requested test item should produce INVALID_VALUE.
    [Fact]
    public void EmptyRequestedTestItem_ReturnsInvalidValue()
    {
        var json = CreateValidOrderJson(
            requestedTests: new[] { "Glucose", "" });

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);

        Assert.Contains(
            result.Errors,
            error => error.Field == "requestedTests" &&
                     error.Code == "INVALID_VALUE");
    }

    // 17. Explicit null values should be treated as REQUIRED.
    [Fact]
    public void ExplicitNullValues_ReturnRequiredErrors()
    {
        var json = """
        {
            "orderId": null,
            "patientId": null,
            "specimenId": null,
            "specimenType": null,
            "priority": null,
            "collectionDate": null,
            "requestedTests": null
        }
        """;

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);
        Assert.Null(result.Order);

        Assert.Contains(
            result.Errors,
            error => error.Field == "orderId" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "patientId" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "specimenId" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "specimenType" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "priority" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "collectionDate" &&
                     error.Code == "REQUIRED");

        Assert.Contains(
            result.Errors,
            error => error.Field == "requestedTests" &&
                     error.Code == "REQUIRED");
    }

    // 18. Today is a valid collection date.
    [Fact]
    public void TodayCollectionDate_IsAccepted()
    {
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var json = CreateValidOrderJson(
            collectionDate: today);

        var result = _service.Process(json);

        Assert.Equal("Accepted", result.Status);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Order);
        Assert.Equal(DateTime.Today, result.Order!.CollectionDate);
    }

    // 19. Null item inside requestedTests should be invalid.
    [Fact]
    public void NullRequestedTestItem_ReturnsInvalidValue()
    {
        var json = """
        {
            "orderId": "ORD-1005",
            "patientId": "PAT-505",
            "specimenId": "SP-9005",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose", null]
        }
        """;

        var result = _service.Process(json);

        Assert.Equal("Rejected", result.Status);

        Assert.Contains(
            result.Errors,
            error => error.Field == "requestedTests" &&
                     error.Code == "INVALID_VALUE");
    }

    // 20. Requested test names are case-sensitive for storage.
    // Only duplicate detection is case-insensitive.
    [Fact]
    public void RequestedTests_PreserveOriginalCasing()
    {
        var json = CreateValidOrderJson(
            requestedTests: new[] { "glucose", "CompleteBloodCount" });

        var result = _service.Process(json);

        Assert.Equal("Accepted", result.Status);
        Assert.NotNull(result.Order);

        Assert.Equal(
            new[] { "glucose", "CompleteBloodCount" },
            result.Order!.RequestedTests);
    }

    // Helper method used to create valid orders for individual tests.
    private static string CreateValidOrderJson(
        string orderId = "ORD-1005",
        string patientId = "PAT-505",
        string specimenId = "SP-9005",
        string specimenType = "Blood",
        string priority = "Routine",
        string collectionDate = "2026-09-18",
        IEnumerable<string>? requestedTests = null)
    {
        requestedTests ??= new[] { "Glucose" };

        return $$"""
        {
            "orderId": "{{orderId}}",
            "patientId": "{{patientId}}",
            "specimenId": "{{specimenId}}",
            "specimenType": "{{specimenType}}",
            "priority": "{{priority}}",
            "collectionDate": "{{collectionDate}}",
            "requestedTests": [{{string.Join(", ", requestedTests.Select(test => test == null ? "null" : $"\"{test}\""))}}]
        }
        """;
    }
}