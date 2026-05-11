1. **Remove circular dependency between `Abstractions` and `Domain` projects.**
   - Current state: `Abstractions` depends on `Domain` and `Domain` depends on `Abstractions`.
   - Action: `Abstractions` should be completely independent. We will modify `Abstractions` to no longer reference `Domain` and `Domain` to depend on `Abstractions` (if needed, though standard practice is Abstractions -> nothing, Domain -> Abstractions or SharedKernel). Wait, actually we can move contracts from Domain to Abstractions or create a SharedKernel if they must be shared.
   - We will remove the `Domain` reference from `Abstractions.csproj`.
2. **Move dependencies causing the circular reference.**
   - `Abstractions` has references to `CapitalUniversity.Core.Domain.Common` (`BaseEntity` in `NotificationDto`), `CapitalUniversity.Core.Domain.UniversityStructure.Enums` (`StructureNodeType`), and `CapitalUniversity.Core.Domain` (`NotificationType` and maybe others).
   - We need to move Enums and purely shared models (like `ActionLevel`, `OverrideType`, etc.) to the Abstractions or SharedKernel project.
   - We already created `NotificationType` enum. We should find where `NotificationDto` uses `BaseEntity` and decouple it, or move `BaseEntity` to a lower layer.
3. **Move Enums and shared DTOs/Entities to SharedKernel or fix dependencies.**
   - The task says "create the MINIMUM possible shared contract project". We have `CapitalUniversity.SharedKernel`. We can move Enums there. Or we can just ensure `Abstractions` doesn't reference `Domain`.
4. **Fix Application dependency.**
   - `Application` has handlers. Make sure `API` doesn't reference `Application` implementations directly unless it's just for DI registration.
5. **Run tests to ensure everything is fixed.**
   - Pre-commit checks.
