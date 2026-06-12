// Student-services workflow enums — integers, matching the backend
// (CapitalUniversity.Module.StudentServices.Abstractions/PublicApi/Enums.cs;
// no JsonStringEnumConverter is configured, so enums serialize as ints).

export const WORKFLOW_STEP_TYPE = { Form: 1, Review: 2, Payment: 3 };

export const WORKFLOW_STEP_TYPE_NAMES = { 1: "Form", 2: "Review", 3: "Payment" };

export const STEP_FIELD_TYPE = {
  Text: 1, TextArea: 2, Number: 3, Date: 4, Select: 5,
  MultiSelect: 6, File: 7, Checkbox: 8, Radio: 9,
};

export const STEP_FIELD_TYPE_NAMES = {
  1: "Text", 2: "TextArea", 3: "Number", 4: "Date", 5: "Select",
  6: "MultiSelect", 7: "File", 8: "Checkbox", 9: "Radio",
};
