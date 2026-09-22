namespace OrderIntake;

public class OrderResult
{
    public string Status { get; set; } = string.Empty;

    public Order? Order { get; set; }

    public List<ValidationError> Errors { get; set; } = new();
}