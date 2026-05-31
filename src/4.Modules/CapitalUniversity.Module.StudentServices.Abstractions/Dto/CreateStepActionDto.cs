namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class CreateStepActionDto
{
    public string ActionKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool TriggersSubmission { get; set; }
}