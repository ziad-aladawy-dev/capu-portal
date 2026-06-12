import os

file_path = r'D:\capu-portal\src\2.Core\CapitalUniversity.Core.Application\StaffManagement\StaffService.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

replacements = {
    'throw new Exception("Email already exists")': 'throw new ValidationException("Email", LocalizedKeys.StaffManagement.EmailInUse)',
    'throw new ValidationException("Email", "Email already exists")': 'throw new ValidationException("Email", LocalizedKeys.StaffManagement.EmailInUse)',
    'throw new Exception("National ID already exists")': 'throw new ValidationException("NationalId", LocalizedKeys.StaffManagement.NationalIdInUse)',
    'throw new ValidationException("NationalId", "National ID already exists")': 'throw new ValidationException("NationalId", LocalizedKeys.StaffManagement.NationalIdInUse)',
    'throw new Exception("Passwords do not match")': 'throw new ValidationException("Password", LocalizedKeys.StaffManagement.PasswordsDoNotMatch)',
    'throw new ValidationException("Password", "Passwords do not match")': 'throw new ValidationException("Password", LocalizedKeys.StaffManagement.PasswordsDoNotMatch)',
    'throw new Exception("Structure node not found")': 'throw new ValidationException("StructureNodeId", LocalizedKeys.StaffManagement.StructureNodeNotFound)',
    'throw new ValidationException("StructureNodeId", "Structure node not found")': 'throw new ValidationException("StructureNodeId", LocalizedKeys.StaffManagement.StructureNodeNotFound)',
    'throw new Exception("Employee code already exists")': 'throw new ValidationException("EmployeeCode", LocalizedKeys.StaffManagement.CodeInUse)',
    'throw new ValidationException("EmployeeCode", "Employee code already exists")': 'throw new ValidationException("EmployeeCode", LocalizedKeys.StaffManagement.CodeInUse)',
    'throw new Exception("Staff not found")': 'throw new NotFoundException(LocalizedKeys.StaffManagement.StaffNotFound)',
    'throw new NotFoundException("Staff not found")': 'throw new NotFoundException(LocalizedKeys.StaffManagement.StaffNotFound)'
}

for old, new in replacements.items():
    content = content.replace(old, new)

# Let's also verify if we need to add 'using CapitalUniversity.SharedKernel.Localization;'
# Assuming 'LocalizedKeys' is part of some namespace like CapitalUniversity.SharedKernel.Constants
# Let's check if the namespace for LocalizedKeys is imported.
# We'll just append it if not present, but wait, usually LocalizedKeys is under CapitalUniversity.SharedKernel.Constants

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)
print('Done.')
