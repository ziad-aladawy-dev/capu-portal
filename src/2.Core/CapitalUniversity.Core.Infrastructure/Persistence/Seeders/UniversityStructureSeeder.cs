using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class UniversityStructureSeeder
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public static async Task SeedAsync(CoreDbContext context)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (await context.StructureNodes.AnyAsync())
                return;

            var nodes = new List<StructureNode>();

            var university = CreateNode(nodes, "جامعة العاصمة", "Capital University", StructureNodeType.University, null, 0);

            var engineeringFaculty = CreateNode(nodes, "كلية الهندسة", "Faculty of Engineering", StructureNodeType.Faculty, university, 0);

            var businessFaculty = CreateNode(nodes, "كلية التجارة", "Faculty of Business", StructureNodeType.Faculty, university, 1);

            var engCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", "Credit Hours System", StructureNodeType.System, engineeringFaculty, 0);
            var engSemester = CreateNode(nodes, "نظام الفصول", "Semester System", StructureNodeType.System, engineeringFaculty, 1);

            var computerScience = CreateNode(nodes, "علوم الحاسب", "Computer Science", StructureNodeType.Program, engCreditHours, 0);
            var informationSystems = CreateNode(nodes, "نظم المعلومات", "Information Systems", StructureNodeType.Program, engCreditHours, 1);
            var artificialIntelligence = CreateNode(nodes, "الذكاء الاصطناعي", "Artificial Intelligence", StructureNodeType.Program, engCreditHours, 2);

            var civilEngineering = CreateNode(nodes, "الهندسة المدنية", "Civil Engineering", StructureNodeType.Program, engSemester, 0);
            var electricalEngineering = CreateNode(nodes, "الهندسة الكهربائية", "Electrical Engineering", StructureNodeType.Program, engSemester, 1);

            var csLevel1 = CreateNode(nodes, "المستوى الأول", "Level 1", StructureNodeType.Level, computerScience, 0);
            var csLevel2 = CreateNode(nodes, "المستوى الثاني", "Level 2", StructureNodeType.Level, computerScience, 1);
            var csLevel3 = CreateNode(nodes, "المستوى الثالث", "Level 3", StructureNodeType.Level, computerScience, 2);
            var csLevel4 = CreateNode(nodes, "المستوى الرابع", "Level 4", StructureNodeType.Level, computerScience, 3);

            var isLevel1 = CreateNode(nodes, "المستوى الأول", "Level 1", StructureNodeType.Level, informationSystems, 0);
            var isLevel2 = CreateNode(nodes, "المستوى الثاني", "Level 2", StructureNodeType.Level, informationSystems, 1);
            var isLevel3 = CreateNode(nodes, "المستوى الثالث", "Level 3", StructureNodeType.Level, informationSystems, 2);
            var isLevel4 = CreateNode(nodes, "المستوى الرابع", "Level 4", StructureNodeType.Level, informationSystems, 3);

            var aiLevel1 = CreateNode(nodes, "المستوى الأول", "Level 1", StructureNodeType.Level, artificialIntelligence, 0);
            var aiLevel2 = CreateNode(nodes, "المستوى الثاني", "Level 2", StructureNodeType.Level, artificialIntelligence, 1);
            var aiLevel3 = CreateNode(nodes, "المستوى الثالث", "Level 3", StructureNodeType.Level, artificialIntelligence, 2);
            var aiLevel4 = CreateNode(nodes, "المستوى الرابع", "Level 4", StructureNodeType.Level, artificialIntelligence, 3);

            var civilLevel1 = CreateNode(nodes, "الفرقة الأولى", "First Year", StructureNodeType.Level, civilEngineering, 0);
            var civilLevel2 = CreateNode(nodes, "الفرقة الثانية", "Second Year", StructureNodeType.Level, civilEngineering, 1);
            var civilLevel3 = CreateNode(nodes, "الفرقة الثالثة", "Third Year", StructureNodeType.Level, civilEngineering, 2);
            var civilLevel4 = CreateNode(nodes, "الفرقة الرابعة", "Fourth Year", StructureNodeType.Level, civilEngineering, 3);

            var elecLevel1 = CreateNode(nodes, "الفرقة الأولى", "First Year", StructureNodeType.Level, electricalEngineering, 0);
            var elecLevel2 = CreateNode(nodes, "الفرقة الثانية", "Second Year", StructureNodeType.Level, electricalEngineering, 1);
            var elecLevel3 = CreateNode(nodes, "الفرقة الثالثة", "Third Year", StructureNodeType.Level, electricalEngineering, 2);
            var elecLevel4 = CreateNode(nodes, "الفرقة الرابعة", "Fourth Year", StructureNodeType.Level, electricalEngineering, 3);

            var businessCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", "Credit Hours System", StructureNodeType.System, businessFaculty, 0);
            var businessSemester = CreateNode(nodes, "نظام الفصول", "Semester System", StructureNodeType.System, businessFaculty, 1);

            var accounting = CreateNode(nodes, "المحاسبة", "Accounting", StructureNodeType.Program, businessCreditHours, 0);
            var marketing = CreateNode(nodes, "التسويق", "Marketing", StructureNodeType.Program, businessCreditHours, 1);

            var accLevel1 = CreateNode(nodes, "المستوى الأول", "Level 1", StructureNodeType.Level, accounting, 0);
            var accLevel2 = CreateNode(nodes, "المستوى الثاني", "Level 2", StructureNodeType.Level, accounting, 1);
            var accLevel3 = CreateNode(nodes, "المستوى الثالث", "Level 3", StructureNodeType.Level, accounting, 2);
            var accLevel4 = CreateNode(nodes, "المستوى الرابع", "Level 4", StructureNodeType.Level, accounting, 3);

            var mktLevel1 = CreateNode(nodes, "المستوى الأول", "Level 1", StructureNodeType.Level, marketing, 0);
            var mktLevel2 = CreateNode(nodes, "المستوى الثاني", "Level 2", StructureNodeType.Level, marketing, 1);
            var mktLevel3 = CreateNode(nodes, "المستوى الثالث", "Level 3", StructureNodeType.Level, marketing, 2);
            var mktLevel4 = CreateNode(nodes, "المستوى الرابع", "Level 4", StructureNodeType.Level, marketing, 3);

            await context.StructureNodes.AddRangeAsync(nodes);
            await context.SaveChangesAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static StructureNode CreateNode(List<StructureNode> nodes, string nameAr, string nameEn, StructureNodeType type, StructureNode? parent, int order)
    {
        var nameJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "ar", nameAr },
            { "en", nameEn }
        });

        var node = new StructureNode
        {
            Id = Guid.NewGuid(),
            Name = nameJson,
            Type = type,
            ParentId = parent?.Id,
            Order = order,
            Depth = parent == null ? 0 : parent.Depth + 1,
            IsActive = true
        };

        node.Path = parent == null ? $"/{node.Id}" : $"{parent.Path}/{node.Id}";
        nodes.Add(node);
        return node;
    }
}