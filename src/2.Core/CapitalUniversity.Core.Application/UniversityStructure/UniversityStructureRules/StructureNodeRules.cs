using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Application.UniversityStructure.UniversityStructureRules;

public static class StructureNodeRules
{
    public static readonly Dictionary<StructureNodeType, List<StructureNodeType>>
        AllowedChildren = new()
        {
            {
                StructureNodeType.University,
                new List<StructureNodeType>
                {
                    StructureNodeType.Faculty
                }
            },

            {
                StructureNodeType.Faculty,
                new List<StructureNodeType>
                {
                    StructureNodeType.System,
                    StructureNodeType.Program
                }
            },

            {
                StructureNodeType.System,
                new List<StructureNodeType>
                {
                    StructureNodeType.Program
                }
            },

            {
                StructureNodeType.Program,
                new List<StructureNodeType>
                {
                    StructureNodeType.Level,
                    StructureNodeType.Track,
                    StructureNodeType.Specialization
                }
            },

            {
                StructureNodeType.Level,
                new List<StructureNodeType>
                {
                    StructureNodeType.Level,
                    StructureNodeType.Track,
                    StructureNodeType.Specialization
                }
            },

            {
                StructureNodeType.Track,
                new List<StructureNodeType>
                {
                    StructureNodeType.Level,
                    StructureNodeType.Specialization
                }
            },

            {
                StructureNodeType.Specialization,
                new List<StructureNodeType>
                {
                    StructureNodeType.Level
                }
            }
        };
}