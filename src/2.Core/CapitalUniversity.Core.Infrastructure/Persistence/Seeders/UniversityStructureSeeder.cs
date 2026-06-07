using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class UniversityStructureSeeder
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static async Task SeedAsync(CoreDbContext context)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (await context.StructureNodes.AnyAsync())
                return;

            var nodes = new List<StructureNode>();

            var university = CreateNode(nodes, "جامعة العاصمة", "Capital University", StructureNodeType.University, null, 0);

            var homeEconomics = CreateNode(nodes, "كلية الاقتصاد المنزلي", "Faculty of Home Economics", StructureNodeType.Faculty, university, 0);
            var homeCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", "Credit Hours System", StructureNodeType.System, homeEconomics, 0);
            var homeSemester = CreateNode(nodes, "نظام الفصول", "Semester System", StructureNodeType.System, homeEconomics, 1);

            var dietetics = CreateNode(nodes, "برنامج التغذية العلاجية", "Clinical Nutrition Program", StructureNodeType.Program, homeCreditHours, 0);
            CreateLevels(nodes, dietetics,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var familyManagement = CreateNode(nodes, "إدارة مؤسسات الأسرة والطفولة", "Family & Childhood Institution Management", StructureNodeType.Program, homeCreditHours, 1);
            CreateLevels(nodes, familyManagement,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var elderly = CreateNode(nodes, "إدارة ورعاية المسنين", "Elderly Management & Care", StructureNodeType.Program, homeCreditHours, 2);
            CreateLevels(nodes, elderly,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var educationalHome = CreateNode(nodes, "الاقتصاد المنزلي التربوي", "Educational Home Economics", StructureNodeType.Program, homeCreditHours, 3);
            CreateLevels(nodes, educationalHome,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var specialNeeds = CreateNode(nodes, "التدريس لذوي الاحتياجات الخاصة", "Teaching for Special Needs", StructureNodeType.Program, homeCreditHours, 4);
            CreateLevels(nodes, specialNeeds,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var nutrition = CreateNode(nodes, "التغذية وعلوم الأطعمة", "Nutrition & Food Science", StructureNodeType.Program, homeCreditHours, 5);
            CreateLevels(nodes, nutrition,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var generalStream = CreateNode(nodes, "الشعبة العامة", "General Stream", StructureNodeType.Program, homeCreditHours, 6);
            CreateLevels(nodes, generalStream,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var leather = CreateNode(nodes, "الصناعات الجلدية", "Leather Industries", StructureNodeType.Program, homeCreditHours, 7);
            CreateLevels(nodes, leather,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var textile = CreateNode(nodes, "الملابس والنسيج", "Textile & Clothing", StructureNodeType.Program, homeCreditHours, 8);
            CreateLevels(nodes, textile,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var apparel = CreateNode(nodes, "تكنولوجيا تصنيع الملابس", "Apparel Manufacturing Technology", StructureNodeType.Program, homeCreditHours, 9);
            CreateLevels(nodes, apparel,
                new[] { "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "First", "Second", "Third", "Fourth" });

            var semesterGeneral = CreateNode(nodes, "البرنامج العام", "General Program", StructureNodeType.Program, homeSemester, 0);
            CreateLevels(nodes, semesterGeneral,
                new[] { "الفرقة الأولى" },
                new[] { "First Level" });

            var semesterTextile = CreateNode(nodes, "الملابس والنسيج", "Textile & Clothing", StructureNodeType.Program, homeSemester, 1);
            CreateLevels(nodes, semesterTextile,
                new[] { "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "Second Level", "Third Level", "Fourth Level" });

            var semesterLeather = CreateNode(nodes, "الصناعات الجلدية", "Leather Industries", StructureNodeType.Program, homeSemester, 2);
            CreateLevels(nodes, semesterLeather,
                new[] { "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "Second Level", "Third Level", "Fourth Level" });

            var matariaEngineering = CreateNode(nodes, "كلية الهندسة بالمطرية", "Faculty of Engineering – Mataria", StructureNodeType.Faculty, university, 1);
            var matariaCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", "Credit Hours System", StructureNodeType.System, matariaEngineering, 0);
            var matariaSemester = CreateNode(nodes, "نظام الفصول", "Semester System", StructureNodeType.System, matariaEngineering, 1);

            var construction = CreateNode(nodes, "إدارة المشروعات والتشييد", "Construction Project Management", StructureNodeType.Program, matariaCreditHours, 0);
            CreateLevels(nodes, construction,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2" });

            var architectureDigital = CreateNode(nodes, "العمارة بالتكنولوجيا الرقمية", "Digital Architecture", StructureNodeType.Program, matariaCreditHours, 1);
            CreateLevels(nodes, architectureDigital,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2" });

            var civil = CreateNode(nodes, "الهندسة المدنية", "Civil Engineering", StructureNodeType.Program, matariaCreditHours, 2);
            CreateLevels(nodes, civil,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var architecture = CreateNode(nodes, "الهندسة المعمارية", "Architectural Engineering", StructureNodeType.Program, matariaCreditHours, 3);
            CreateLevels(nodes, architecture,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var structural = CreateNode(nodes, "الهندسة الإنشائية", "Structural Engineering", StructureNodeType.Program, matariaCreditHours, 4);
            CreateLevels(nodes, structural,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2" });

            var energy = CreateNode(nodes, "هندسة الطاقة", "Energy Engineering", StructureNodeType.Program, matariaCreditHours, 5);
            CreateLevels(nodes, energy,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2" });

            var automotive = CreateNode(nodes, "هندسة السيارات والجرارات", "Automotive & Tractors Engineering", StructureNodeType.Program, matariaCreditHours, 6);
            CreateLevels(nodes, automotive,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var mechatronics = CreateNode(nodes, "هندسة الميكاترونيات بالسيارات", "Automotive Mechatronics", StructureNodeType.Program, matariaCreditHours, 7);
            CreateLevels(nodes, mechatronics,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var matariaCivilSemester = CreateNode(nodes, "الهندسة المدنية", "Civil Engineering", StructureNodeType.Program, matariaSemester, 0);
            CreateLevels(nodes, matariaCivilSemester,
                new[] { "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "First Level", "Second Level", "Third Level", "Fourth Level" });

            var mechanicalDesign = CreateNode(nodes, "التصميم الميكانيكي", "Mechanical Design", StructureNodeType.Program, matariaSemester, 1);
            CreateLevels(nodes, mechanicalDesign,
                new[] { "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "First Level", "Second Level", "Third Level", "Fourth Level" });

            var helwanEngineering = CreateNode(nodes, "كلية الهندسة بحلوان", "Faculty of Engineering – Helwan", StructureNodeType.Faculty, university, 2);
            var helwanCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", "Credit Hours System", StructureNodeType.System, helwanEngineering, 0);
            var helwanSemester = CreateNode(nodes, "نظام الفصول", "Semester System", StructureNodeType.System, helwanEngineering, 1);

            var helwanGeneral = CreateNode(nodes, "البرنامج العام", "General Program", StructureNodeType.Program, helwanCreditHours, 0);
            CreateLevels(nodes, helwanGeneral,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var biomedical = CreateNode(nodes, "الهندسة الحيوية الطبية", "Biomedical Engineering", StructureNodeType.Program, helwanCreditHours, 1);
            CreateLevels(nodes, biomedical,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var industrial = CreateNode(nodes, "الهندسة الصناعية", "Industrial Engineering", StructureNodeType.Program, helwanCreditHours, 2);
            CreateLevels(nodes, industrial,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var communication = CreateNode(nodes, "هندسة الاتصالات والمعلومات", "Communications & Information Engineering", StructureNodeType.Program, helwanCreditHours, 3);
            CreateLevels(nodes, communication,
                new[] { "إعدادي", "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "Preparatory", "First", "Second", "Third", "Fourth" });

            var electrical = CreateNode(nodes, "هندسة القوى والوقاية الكهربية", "Electrical Power Engineering", StructureNodeType.Program, helwanCreditHours, 4);
            CreateLevels(nodes, electrical,
                new[] { "إعدادي", "الأول", "الثاني", "الثالث", "الرابع" },
                new[] { "Preparatory", "First", "Second", "Third", "Fourth" });

            var computer = CreateNode(nodes, "هندسة الحاسبات والنظم", "Computer & Systems Engineering", StructureNodeType.Program, helwanCreditHours, 5);
            CreateLevels(nodes, computer,
                new[] { "Sophomore", "Junior", "Senior" },
                new[] { "Sophomore", "Junior", "Senior" });

            var electronics = CreateNode(nodes, "هندسة الإلكترونيات والاتصالات", "Electronics & Communications Engineering", StructureNodeType.Program, helwanCreditHours, 6);
            CreateLevels(nodes, electronics,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var production = CreateNode(nodes, "هندسة الإنتاج", "Production Engineering", StructureNodeType.Program, helwanCreditHours, 7);
            CreateLevels(nodes, production,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var power = CreateNode(nodes, "هندسة القوى والآلات الكهربائية", "Power & Electrical Machines Engineering", StructureNodeType.Program, helwanCreditHours, 8);
            CreateLevels(nodes, power,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var helwanMechatronics = CreateNode(nodes, "هندسة الميكاترونيات", "Mechatronics Engineering", StructureNodeType.Program, helwanCreditHours, 9);
            CreateLevels(nodes, helwanMechatronics,
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" },
                new[] { "FreshMan", "Sophomore", "Junior", "Senior" });

            var helwanSemesterGeneral = CreateNode(nodes, "البرنامج العام لائحة 2020", "General Program 2020 Regulations", StructureNodeType.Program, helwanSemester, 0);
            CreateLevels(nodes, helwanSemesterGeneral,
                new[] { "إعدادي" },
                new[] { "Preparatory" });

            var semesterElectronics = CreateNode(nodes, "هندسة الإلكترونيات والاتصالات", "Electronics & Communications Engineering", StructureNodeType.Program, helwanSemester, 1);
            CreateLevels(nodes, semesterElectronics,
                new[] { "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "First Level", "Second Level", "Third Level", "Fourth Level" });

            var semesterPower = CreateNode(nodes, "هندسة القوى والآلات الكهربية", "Power & Electrical Machines Engineering", StructureNodeType.Program, helwanSemester, 2);
            CreateLevels(nodes, semesterPower,
                new[] { "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "First Level", "Second Level", "Third Level", "Fourth Level" });

            var semesterBiomedical = CreateNode(nodes, "الهندسة الحيوية الطبية", "Biomedical Engineering", StructureNodeType.Program, helwanSemester, 3);
            CreateLevels(nodes, semesterBiomedical,
                new[] { "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "First Level", "Second Level", "Third Level", "Fourth Level" });

            await context.StructureNodes.AddRangeAsync(nodes);
            await context.SaveChangesAsync();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static void CreateLevels(List<StructureNode> nodes, StructureNode parent, string[] namesAr, string[] namesEn)
    {
        for (int i = 0; i < namesAr.Length; i++)
        {
            CreateNode(nodes, namesAr[i], namesEn[i], StructureNodeType.Level, parent, i);
        }
    }

    private static StructureNode CreateNode(List<StructureNode> nodes, string nameAr, string nameEn, StructureNodeType type, StructureNode? parent, int order)
    {
        var nameJson = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            { "ar", nameAr },
            { "en", nameEn }
        }, _jsonOptions);

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