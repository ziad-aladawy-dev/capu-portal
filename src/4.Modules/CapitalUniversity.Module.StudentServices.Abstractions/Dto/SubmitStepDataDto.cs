namespace CapitalUniversity.Module.StudentServices.Abstractions.Dto;

public class SubmitStepDataDto
{
    public string StepKey { get; set; } = string.Empty; 
    public object Data { get; set; } = new();
}