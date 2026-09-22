namespace OrderIntake;

public class Order
{
    public string OrderId { get; set; } = string.Empty;

    public string PatientId { get; set; } = string.Empty;

    public string SpecimenId { get; set; } = string.Empty;

    public string SpecimenType { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public DateTime CollectionDate { get; set; }

    public List<string> RequestedTests { get; set; } = new();
}