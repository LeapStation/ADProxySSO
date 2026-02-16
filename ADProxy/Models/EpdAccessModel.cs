namespace ADProxy.Models;

public class EpdAccessModel
{
    public EpdActorModel Actor { get; set; }
}

public class EpdActorModel
{
    public string? Ssin { get; set; }
    public string DisplayName { get; set; }
    public string Organization { get; set; }
    public string? Uid { get; set; }
}
