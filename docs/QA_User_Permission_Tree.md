# QA: Implementation of User-Specific Permission Tree API

The following questions must be answered to ensure the implementation of the new user-specific permission tree API meets the architectural and functional requirements.

---

### **Q1: Endpoint Specification**
- **Route**: Should the endpoint follow the authorization pattern?
  - *Option A*: `GET api/authorization/users/{userId}/permission-tree`
- **Permission Guard**: Which permission name should guard this endpoint? 
  - *Suggestion*: `PermissionNames.Permissions.EditClose` (Admin audit capability).
  - and if the user has permission higher than edit+colse can access it also

### **Q2: Conflict & Overlap Resolution**
Permissions in this system are aggregated from multiple roles and potential "Deny" overrides. 
- In the tree view (`ModulePermissionTreeDto`), if a permission is granted by one role but denied by an override, the resulting `IsAssigned` will be `false`. 
- **Should we provide "Provenance" metadata?** i.e., Should the API return a simple boolean `IsAssigned`, or should it include information on which Role or Override provided the final result?
Answer: simple bolean
reason: i need to use ef tracking on that getter to update thorugh it so the front end will divide the permissions by modules, resources, and the assigned permissions will have true so its checkbox = true and the denied or not assigned including all permissions will have false 
so the final result is the admin who can change the permissions for another staff member will see all system permissions and choose to give or remove permissions via checkboxes

### **Q3: Contextual Scoping**
The system supports Structural (Faculty/Department) and Temporal (Year/Semester) scoping for permissions.
- **Should the API accept these as optional query parameters?**
  - `Guid? structureNodeId`
  - `Guid? academicYearId`
  - `Guid? semesterId`
- *Note*: If omitted, should the API default to "Global/Always Active" permissions or the user's "Current" context?
answer: the context in that case will be as a filter for user so not needed for permissions cause the permission is not tied to structural filtering but each permission retrived by the api should contain the context the user we're getting his permissions scope like 'faculty head' has view permission on his faculty for this year so you should retrieve it like that the same as the auth response but the main difference is i get it for another user not the use i login with

### **Q4: Implied Permissions**
The system uses an "Implies" graph (e.g., `Edit` implies `View`).
- **Should the tree nodes reflect implied status?** If a user has `Edit` assigned, should the `View` node in the tree also be marked as `IsAssigned = true`?
answer: yes, but not nessecerly used by front + if not a big cost arrange them desc

---

**Please provide your answers/preferences, and I will proceed with the implementation based on your feedback.**


answers you didn't ask for
- the localiztion should be integrated to this service, using the json values stored in database
- the chaching strategy should be considered, while retrieving because the strategy contians storing the full object and using hash tables to get user-specific refernces, so the main point you have to retrive first the chosen user permissions' ids then match them to the cache if not found in chache retreive from the db 
