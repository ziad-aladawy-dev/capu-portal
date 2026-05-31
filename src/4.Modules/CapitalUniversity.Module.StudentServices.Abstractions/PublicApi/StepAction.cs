namespace CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;

public class StepAction
{
    public string ActionKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool TriggersSubmission { get; set; }
}