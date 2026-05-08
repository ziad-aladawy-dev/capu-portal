# Permissions API HTTP Examples

## 1. Get Effective Permissions (Bootstrap)
Returns all active evaluated permissions for the current user based on headers.

```http
GET /api/permissions
Accept: application/json
Authorization: Bearer <token>
X-Faculty-Id: 12345678-1234-1234-1234-123456789012
X-AcademicYear-Id: 22345678-1234-1234-1234-123456789012
X-Semester-Id: 32345678-1234-1234-1234-123456789012
```

## 2. Create Permission Assignment Projection
Creates role assignments and permission overrides under a specific scope boundary.

```http
POST /api/permissions
Content-Type: application/json
Authorization: Bearer <token>

{
  "userId": "11111111-1111-1111-1111-111111111111",
  "roleIds": [
    "22222222-2222-2222-2222-222222222222"
  ],
  "permissionOverrides": [
    {
      "serviceId": "33333333-3333-3333-3333-333333333333",
      "resource": "Profile",
      "level": 3,
      "type": 1
    }
  ],
  "structuralScope": {
    "facultyId": "12345678-1234-1234-1234-123456789012",
    "allFaculties": false
  },
  "temporalScope": {
    "alwaysActive": true
  }
}
```

## 3. Get Scoped Assignment Projection
Retrieves grouped roles and overrides matching the exact scope.

```http
GET /api/permissions/assignment?userId=11111111-1111-1111-1111-111111111111&facultyId=12345678-1234-1234-1234-123456789012&allFaculties=false&alwaysActive=true
Accept: application/json
Authorization: Bearer <token>
```

## 4. Update Scoped Assignment Projection
Modifies the roles/overrides inside the specified boundary.

```http
PUT /api/permissions/assignment
Content-Type: application/json
Authorization: Bearer <token>

{
  "userId": "11111111-1111-1111-1111-111111111111",
  "rolesToAdd": ["44444444-4444-4444-4444-444444444444"],
  "rolesToRemove": [],
  "permissionsToAdd": [],
  "permissionsToRemove": [
    {
      "serviceId": "33333333-3333-3333-3333-333333333333",
      "resource": "Profile",
      "type": 1
    }
  ],
  "structuralScope": {
    "facultyId": "12345678-1234-1234-1234-123456789012",
    "allFaculties": false
  },
  "temporalScope": {
    "alwaysActive": true
  }
}
```
