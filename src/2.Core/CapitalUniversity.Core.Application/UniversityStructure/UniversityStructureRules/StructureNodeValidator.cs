using CapitalUniversity.Core.Abstractions.UniversityStructure.Enums;
namespace CapitalUniversity.Core.Application.UniversityStructure.UniversityStructureRules;

public static class StructureNodeValidator
{
    public static bool IsValidChild(StructureNodeType parentType, StructureNodeType childType)
    {
        if (!StructureNodeRules.AllowedChildren
            .ContainsKey(parentType))
        {
            return false;
        }

        return StructureNodeRules
            .AllowedChildren[parentType]
            .Contains(childType);
    }

    public static bool CanBeRoot(StructureNodeType type)
    {
        return type == StructureNodeType.University;
    }
}