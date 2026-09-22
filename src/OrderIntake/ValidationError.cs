namespace OrderIntake;

public class ValidationError
{
    public string Field { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}