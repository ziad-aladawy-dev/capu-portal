//using CapitalUniversity.Core.Domain.UniversityStructure;
//using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
//using CapitalUniversity.Core.Infrastructure.Persistence;
//using Microsoft.EntityFrameworkCore;

//namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

//public static class UniversityStructureSeeder
//{
//    private static readonly SemaphoreSlim _semaphore = new(1, 1);

//    public static async Task SeedAsync(CoreDbContext context)
//    {
//        await _semaphore.WaitAsync();
//        try
//        {
//            if (await context.StructureNodes.AnyAsync())
//                return;

//            var nodes = new List<StructureNode>();

//            var university = CreateNode(nodes, "جامعة العاصمه", StructureNodeType.University, null, 0);

//            #region كلية الاقتصاد المنزلي
//            var homeEconomics = CreateNode(nodes, "كلية الاقتصاد المنزلي", StructureNodeType.Faculty, university, 0);
//            var homeCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", StructureNodeType.System, homeEconomics, 0);
//            var homeSemester = CreateNode(nodes, "نظام الفصول", StructureNodeType.System, homeEconomics, 1);

//            var dietetics = CreateNode(nodes, "برنامج التغذية العلاجية", StructureNodeType.Program, homeCreditHours, 0);
//            CreateLevels(nodes, dietetics, "الأول", "الثاني", "الثالث", "الرابع");

//            var familyManagement = CreateNode(nodes, "إدارة مؤسسات الأسرة والطفولة", StructureNodeType.Program, homeCreditHours, 1);
//            CreateLevels(nodes, familyManagement, "الأول", "الثاني", "الثالث", "الرابع");

//            var elderly = CreateNode(nodes, "إدارة ورعاية المسنين", StructureNodeType.Program, homeCreditHours, 2);
//            CreateLevels(nodes, elderly, "الأول", "الثاني", "الثالث", "الرابع");

//            var educationalHome = CreateNode(nodes, "الاقتصاد المنزلي التربوي", StructureNodeType.Program, homeCreditHours, 3);
//            CreateLevels(nodes, educationalHome, "الأول", "الثاني", "الثالث", "الرابع");

//            var specialNeeds = CreateNode(nodes, "التدريس لذوي الاحتياجات الخاصة", StructureNodeType.Program, homeCreditHours, 4);
//            CreateLevels(nodes, specialNeeds, "الأول", "الثاني", "الثالث", "الرابع");

//            var nutrition = CreateNode(nodes, "التغذية وعلوم الأطعمة", StructureNodeType.Program, homeCreditHours, 5);
//            CreateLevels(nodes, nutrition, "الأول", "الثاني", "الثالث", "الرابع");

//            var generalStream = CreateNode(nodes, "الشعبة العامة", StructureNodeType.Program, homeCreditHours, 6);
//            CreateLevels(nodes, generalStream, "الأول", "الثاني", "الثالث", "الرابع");

//            var leather = CreateNode(nodes, "الصناعات الجلدية", StructureNodeType.Program, homeCreditHours, 7);
//            CreateLevels(nodes, leather, "الأول", "الثاني", "الثالث", "الرابع");

//            var textile = CreateNode(nodes, "الملابس والنسيج", StructureNodeType.Program, homeCreditHours, 8);
//            CreateLevels(nodes, textile, "الأول", "الثاني", "الثالث", "الرابع");

//            var apparel = CreateNode(nodes, "تكنولوجيا تصنيع الملابس", StructureNodeType.Program, homeCreditHours, 9);
//            CreateLevels(nodes, apparel, "الأول", "الثاني", "الثالث", "الرابع");

//            var semesterGeneral = CreateNode(nodes, "البرنامج العام", StructureNodeType.Program, homeSemester, 0);
//            CreateLevels(nodes, semesterGeneral, "الفرقة الأولى");

//            var semesterTextile = CreateNode(nodes, "الملابس والنسيج", StructureNodeType.Program, homeSemester, 1);
//            CreateLevels(nodes, semesterTextile, "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة");

//            var semesterLeather = CreateNode(nodes, "الصناعات الجلدية", StructureNodeType.Program, homeSemester, 2);
//            CreateLevels(nodes, semesterLeather, "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة");
//            #endregion

//            #region كلية الهندسة بالمطرية
//            var matariaEngineering = CreateNode(nodes, "كلية الهندسة بالمطرية", StructureNodeType.Faculty, university, 1);
//            var matariaCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", StructureNodeType.System, matariaEngineering, 0);
//            var matariaSemester = CreateNode(nodes, "نظام الفصول", StructureNodeType.System, matariaEngineering, 1);

//            var construction = CreateNode(nodes, "إدارة المشروعات والتشييد", StructureNodeType.Program, matariaCreditHours, 0);
//            CreateLevels(nodes, construction, "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2");

//            var architectureDigital = CreateNode(nodes, "العمارة بالتكنولوجيا الرقمية", StructureNodeType.Program, matariaCreditHours, 1);
//            CreateLevels(nodes, architectureDigital, "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2");

//            var civil = CreateNode(nodes, "الهندسة المدنية", StructureNodeType.Program, matariaCreditHours, 2);
//            CreateLevels(nodes, civil, "FreshMan", "Sophomore", "Junior", "Senior");

//            var architecture = CreateNode(nodes, "الهندسة المعمارية", StructureNodeType.Program, matariaCreditHours, 3);
//            CreateLevels(nodes, architecture, "FreshMan", "Sophomore", "Junior", "Senior");

//            var structural = CreateNode(nodes, "الهندسة الإنشائية", StructureNodeType.Program, matariaCreditHours, 4);
//            CreateLevels(nodes, structural, "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2");

//            var energy = CreateNode(nodes, "هندسة الطاقة", StructureNodeType.Program, matariaCreditHours, 5);
//            CreateLevels(nodes, energy, "FreshMan", "Sophomore", "Junior", "Senior-1", "Senior-2");

//            var automotive = CreateNode(nodes, "هندسة السيارات والجرارات", StructureNodeType.Program, matariaCreditHours, 6);
//            CreateLevels(nodes, automotive, "FreshMan", "Sophomore", "Junior", "Senior");

//            var mechatronics = CreateNode(nodes, "هندسة الميكاترونيات بالسيارات", StructureNodeType.Program, matariaCreditHours, 7);
//            CreateLevels(nodes, mechatronics, "FreshMan", "Sophomore", "Junior", "Senior");

//            var matariaCivilSemester = CreateNode(nodes, "الهندسة المدنية", StructureNodeType.Program, matariaSemester, 0);
//            CreateLevels(nodes, matariaCivilSemester, "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة");

//            var mechanicalDesign = CreateNode(nodes, "التصميم الميكانيكي", StructureNodeType.Program, matariaSemester, 1);
//            CreateLevels(nodes, mechanicalDesign, "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة");
//            #endregion

//            #region كلية الهندسة بحلوان
//            var helwanEngineering = CreateNode(nodes, "كلية الهندسة بحلوان", StructureNodeType.Faculty, university, 2);
//            var helwanCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", StructureNodeType.System, helwanEngineering, 0);
//            var helwanSemester = CreateNode(nodes, "نظام الفصول", StructureNodeType.System, helwanEngineering, 1);

//            var helwanGeneral = CreateNode(nodes, "البرنامج العام", StructureNodeType.Program, helwanCreditHours, 0);
//            CreateLevels(nodes, helwanGeneral, "FreshMan", "Sophomore", "Junior", "Senior");

//            var biomedical = CreateNode(nodes, "الهندسة الحيوية الطبية", StructureNodeType.Program, helwanCreditHours, 1);
//            CreateLevels(nodes, biomedical, "FreshMan", "Sophomore", "Junior", "Senior");

//            var industrial = CreateNode(nodes, "الهندسة الصناعية", StructureNodeType.Program, helwanCreditHours, 2);
//            CreateLevels(nodes, industrial, "FreshMan", "Sophomore", "Junior", "Senior");

//            var communication = CreateNode(nodes, "هندسة الاتصالات والمعلومات", StructureNodeType.Program, helwanCreditHours, 3);
//            CreateLevels(nodes, communication, "إعدادي", "الأول", "الثاني", "الثالث", "الرابع");

//            var electrical = CreateNode(nodes, "هندسة القوى والوقاية الكهربية", StructureNodeType.Program, helwanCreditHours, 4);
//            CreateLevels(nodes, electrical, "إعدادي", "الأول", "الثاني", "الثالث", "الرابع");

//            var computer = CreateNode(nodes, "هندسة الحاسبات والنظم", StructureNodeType.Program, helwanCreditHours, 5);
//            CreateLevels(nodes, computer, "Sophomore", "Junior", "Senior");

//            var electronics = CreateNode(nodes, "هندسة الإلكترونيات والاتصالات", StructureNodeType.Program, helwanCreditHours, 6);
//            CreateLevels(nodes, electronics, "FreshMan", "Sophomore", "Junior", "Senior");

//            var production = CreateNode(nodes, "هندسة الإنتاج", StructureNodeType.Program, helwanCreditHours, 7);
//            CreateLevels(nodes, production, "FreshMan", "Sophomore", "Junior", "Senior");

//            var power = CreateNode(nodes, "هندسة القوى والآلات الكهربائية", StructureNodeType.Program, helwanCreditHours, 8);
//            CreateLevels(nodes, power, "FreshMan", "Sophomore", "Junior", "Senior");

//            var helwanMechatronics = CreateNode(nodes, "هندسة الميكاترونيات", StructureNodeType.Program, helwanCreditHours, 9);
//            CreateLevels(nodes, helwanMechatronics, "FreshMan", "Sophomore", "Junior", "Senior");

//            var helwanSemesterGeneral = CreateNode(nodes, "البرنامج العام لائحة 2020", StructureNodeType.Program, helwanSemester, 0);
//            CreateLevels(nodes, helwanSemesterGeneral, "إعدادي");

//            var semesterElectronics = CreateNode(nodes, "هندسة الإلكترونيات والاتصالات", StructureNodeType.Program, helwanSemester, 1);
//            CreateLevels(nodes, semesterElectronics, "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة");

//            var semesterPower = CreateNode(nodes, "هندسة القوى والآلات الكهربية", StructureNodeType.Program, helwanSemester, 2);
//            CreateLevels(nodes, semesterPower, "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة");

//            var semesterBiomedical = CreateNode(nodes, "الهندسة الحيوية الطبية", StructureNodeType.Program, helwanSemester, 3);
//            CreateLevels(nodes, semesterBiomedical, "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة");
//            #endregion

//            await context.StructureNodes.AddRangeAsync(nodes);
//            await context.SaveChangesAsync();
//        }
//        finally
//        {
//            _semaphore.Release();
//        }
//    }

//    private static void CreateLevels(List<StructureNode> nodes, StructureNode parent, params string[] levels)
//    {
//        for (int i = 0; i < levels.Length; i++)
//        {
//            CreateNode(nodes, levels[i], StructureNodeType.Level, parent, i);
//        }
//    }

//    private static StructureNode CreateNode(List<StructureNode> nodes, string name, StructureNodeType type, StructureNode? parent, int order)
//    {
//        var node = new StructureNode
//        {
//            Id = Guid.NewGuid(),
//            Name = name,
//            Type = type,
//            ParentId = parent?.Id,
//            Order = order,
//            Depth = parent == null ? 0 : parent.Depth + 1,
//            IsActive = true
//        };

//        node.Path = parent == null ? $"/{node.Id}" : $"{parent.Path}/{node.Id}";
//        nodes.Add(node);
//        return node;
//    }
//}

using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
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

            // ================================
            // 1. UNIVERSITY (ROOT)
            // ================================
            var university = CreateNode(nodes, "جامعة العاصمة", "Capital University", StructureNodeType.University, null, 0);

            // ================================
            // 2. FACULTY OF HOME ECONOMICS
            // ================================
            var homeEconomics = CreateNode(nodes, "كلية الاقتصاد المنزلي", "Faculty of Home Economics", StructureNodeType.Faculty, university, 0);
            var homeCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", "Credit Hours System", StructureNodeType.System, homeEconomics, 0);
            var homeSemester = CreateNode(nodes, "نظام الفصول", "Semester System", StructureNodeType.System, homeEconomics, 1);

            // Programs under Credit Hours System
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

            // Programs under Semester System
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

            // ================================
            // 3. FACULTY OF ENGINEERING – MATARIA
            // ================================
            var matariaEngineering = CreateNode(nodes, "كلية الهندسة بالمطرية", "Faculty of Engineering – Mataria", StructureNodeType.Faculty, university, 1);
            var matariaCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", "Credit Hours System", StructureNodeType.System, matariaEngineering, 0);
            var matariaSemester = CreateNode(nodes, "نظام الفصول", "Semester System", StructureNodeType.System, matariaEngineering, 1);

            // Credit Hours Programs
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

            // Semester Programs (Mataria)
            var matariaCivilSemester = CreateNode(nodes, "الهندسة المدنية", "Civil Engineering", StructureNodeType.Program, matariaSemester, 0);
            CreateLevels(nodes, matariaCivilSemester,
                new[] { "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "First Level", "Second Level", "Third Level", "Fourth Level" });

            var mechanicalDesign = CreateNode(nodes, "التصميم الميكانيكي", "Mechanical Design", StructureNodeType.Program, matariaSemester, 1);
            CreateLevels(nodes, mechanicalDesign,
                new[] { "الفرقة الأولى", "الفرقة الثانية", "الفرقة الثالثة", "الفرقة الرابعة" },
                new[] { "First Level", "Second Level", "Third Level", "Fourth Level" });

            // ================================
            // 4. FACULTY OF ENGINEERING – HELWAN
            // ================================
            var helwanEngineering = CreateNode(nodes, "كلية الهندسة بحلوان", "Faculty of Engineering – Helwan", StructureNodeType.Faculty, university, 2);
            var helwanCreditHours = CreateNode(nodes, "نظام الساعات المعتمدة", "Credit Hours System", StructureNodeType.System, helwanEngineering, 0);
            var helwanSemester = CreateNode(nodes, "نظام الفصول", "Semester System", StructureNodeType.System, helwanEngineering, 1);

            // Credit Hours Programs (Helwan)
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

            // Semester Programs (Helwan)
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

            // ================================
            // SAVE ALL NODES
            // ================================
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