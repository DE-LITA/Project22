using System.Globalization;
using System.Text.Json;

namespace OrderIntake;

public class OrderIntakeService
{
    public OrderResult Process(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return MalformedResult("Input JSON is empty.");
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return MalformedResult("Input is not valid JSON.");
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return MalformedResult("JSON root must be an object.");
            }

            try
            {
                var input = JsonSerializer.Deserialize<OrderInput>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (input == null)
                {
                    return MalformedResult(
                        "JSON could not be read as an order.");
                }

                var errors = Validate(input);

                if (errors.Count > 0)
                {
                    return RejectedResult(errors);
                }

                var order = BuildOrder(input);

                return AcceptedResult(order);
            }
            catch (JsonException)
            {
                return MalformedResult(
                    "JSON contains an incompatible field value.");
            }
        }
    }

    private List<ValidationError> Validate(OrderInput input)
    {
        var errors = new List<ValidationError>();

        ValidateId(input.OrderId, "orderId", errors);
        ValidateId(input.PatientId, "patientId", errors);
        ValidateId(input.SpecimenId, "specimenId", errors);

        ValidateSpecimenType(input.SpecimenType, errors);
        ValidatePriority(input.Priority, errors);
        ValidateCollectionDate(input.CollectionDate, errors);
        ValidateRequestedTests(input.RequestedTests, errors);

        return errors;
    }

    private void ValidateId(
        string? value,
        string field,
        List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Field = field,
                Code = "REQUIRED",
                Message = $"{field} is required."
            });

            return;
        }

        if (value.Length > 20)
        {
            errors.Add(new ValidationError
            {
                Field = field,
                Code = "MAX_LENGTH",
                Message = $"{field} must not exceed 20 characters."
            });
        }
    }

    private void ValidateSpecimenType(
        string? value,
        List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Field = "specimenType",
                Code = "REQUIRED",
                Message = "specimenType is required."
            });

            return;
        }

        var validValues = new[]
        {
            "Blood",
            "Urine",
            "Tissue",
            "Saliva"
        };

        if (!validValues.Any(x =>
        string.Equals(
            x,
            value,
            StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new ValidationError
            {
                Field = "specimenType",
                Code = "INVALID_VALUE",
                Message =
                    "specimenType must be Blood, Urine, Tissue or Saliva."
            });
        }
    }

    private void ValidatePriority(
        string? value,
        List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Field = "priority",
                Code = "REQUIRED",
                Message = "priority is required."
            });

            return;
        }

        var validValues = new[]
        {
            "Routine",
            "Urgent"
        };

        if (!validValues.Any(x =>
        string.Equals(
            x,
            value,
            StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(new ValidationError
            {
                Field = "priority",
                Code = "INVALID_VALUE",
                Message =
                    "priority must be Routine or Urgent."
            });
        }
    }

    private void ValidateCollectionDate(
        string? value,
        List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Field = "collectionDate",
                Code = "REQUIRED",
                Message = "collectionDate is required."
            });

            return;
        }

        if (!DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            errors.Add(new ValidationError
            {
                Field = "collectionDate",
                Code = "INVALID_FORMAT",
                Message =
                    "collectionDate must be a valid date in yyyy-MM-dd format."
            });

            return;
        }

        if (date.Date > DateTime.Today)
        {
            errors.Add(new ValidationError
            {
                Field = "collectionDate",
                Code = "FUTURE_DATE",
                Message =
                    "collectionDate must not be after today."
            });
        }
    }

    private void ValidateRequestedTests(
        List<string>? requestedTests,
        List<ValidationError> errors)
    {
        if (requestedTests == null ||
            requestedTests.Count == 0)
        {
            errors.Add(new ValidationError
            {
                Field = "requestedTests",
                Code = "REQUIRED",
                Message =
                    "At least one requested test is required."
            });

            return;
        }

        if (requestedTests.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add(new ValidationError
            {
                Field = "requestedTests",
                Code = "INVALID_VALUE",
                Message =
                    "Requested test names must not be empty."
            });
        }

        var containsDuplicate = requestedTests
            .Where(test => !string.IsNullOrWhiteSpace(test))
            .GroupBy(
                test => test,
                StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);

        if (containsDuplicate)
        {
            errors.Add(new ValidationError
            {
                Field = "requestedTests",
                Code = "DUPLICATE",
                Message =
                    "Requested tests must not contain duplicates."
            });
        }
    }

    private Order BuildOrder(OrderInput input)
    {
        return new Order
        {
            OrderId = input.OrderId!,
            PatientId = input.PatientId!,
            SpecimenId = input.SpecimenId!,
            SpecimenType = NormalizeSpecimenType(
                input.SpecimenType!),
            Priority = NormalizePriority(
                input.Priority!),
            CollectionDate = DateTime.ParseExact(
                input.CollectionDate!,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            RequestedTests = input.RequestedTests!
        };
    }

    private string NormalizeSpecimenType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "blood" => "Blood",
            "urine" => "Urine",
            "tissue" => "Tissue",
            "saliva" => "Saliva",
            _ => value
        };
    }

    private string NormalizePriority(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "routine" => "Routine",
            "urgent" => "Urgent",
            _ => value
        };
    }

    private OrderResult AcceptedResult(Order order)
    {
        return new OrderResult
        {
            Status = "Accepted",
            Order = order,
            Errors = new List<ValidationError>()
        };
    }

    private OrderResult RejectedResult(
        List<ValidationError> errors)
    {
        return new OrderResult
        {
            Status = "Rejected",
            Order = null,
            Errors = errors
        };
    }

    private OrderResult MalformedResult(string message)
    {
        return new OrderResult
        {
            Status = "Rejected",
            Order = null,
            Errors =
            [
                new ValidationError
                {
                    Field = "$",
                    Code = "MALFORMED_INPUT",
                    Message = message
                }
            ]
        };
    }

    private class OrderInput
    {
        public string? OrderId { get; set; }

        public string? PatientId { get; set; }

        public string? SpecimenId { get; set; }

        public string? SpecimenType { get; set; }

        public string? Priority { get; set; }

        public string? CollectionDate { get; set; }

        public List<string>? RequestedTests { get; set; }
    }
}