# Project22 - Laboratory Order Intake and Input Validation Service

## About the Project

This project implements a small C# service for receiving and validating a
laboratory order provided as a JSON string.

The service checks the order against the required validation rules and returns
an `OrderResult`.

If the order is valid, the result contains an `Accepted` status and the
validated order.

If the order is invalid, the result contains a `Rejected` status and all the
validation errors found in the input.

For malformed JSON or input that does not have the expected order structure,
the service returns a single `MALFORMED_INPUT` error instead of throwing an
unhandled exception.

The implementation is intentionally kept as a class library. There is no
console application, web API, database, UI, or external service because
these are outside the scope of the assessment.

---

## Technology Used

- C#
- .NET 10
- xUnit
- System.Text.Json
- Git / GitHub

---

## Project Structure

```text
Project22
│
├── src
│   └── OrderIntake
│       ├── Order.cs
│       ├── OrderResult.cs
│       ├── OrderIntakeService.cs
│       └── ValidationError.cs
│
├── tests
│   └── OrderIntake.Tests
│       └── OrderIntakeServiceTests.cs
│
├── Project22.sln
└── README.md