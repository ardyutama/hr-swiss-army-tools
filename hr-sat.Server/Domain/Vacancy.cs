namespace hr_sat.Server.Domain;

public sealed class Vacancy
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public DateTime CreatedOn { get; set; }
}
