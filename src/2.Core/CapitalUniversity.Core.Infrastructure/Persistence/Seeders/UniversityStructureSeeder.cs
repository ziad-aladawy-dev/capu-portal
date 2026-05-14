//using CapitalUniversity.Core.Domain.UniversityStructure;
//using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

//namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

//public static class UniversityStructureSeeder
//{
//    public static async Task SeedAsync(
//        CoreDbContext context)
//    {
//        if (context.StructureNodes.Any())
//            return;

//        var university = CreateNode(
//            "Capital University",
//            StructureNodeType.University,
//            null,
//            0);

//        var commerceFaculty = CreateNode(
//            "Faculty of Commerce",
//            StructureNodeType.Faculty,
//            university,
//            0);

//        var csFaculty = CreateNode(
//            "Faculty of Computers and AI",
//            StructureNodeType.Faculty,
//            university,
//            1);

//        var mediaFaculty = CreateNode(
//            "Faculty of Media",
//            StructureNodeType.Faculty,
//            university,
//            2);

//        var commerceCreditHours = CreateNode(
//            "Credit Hours System",
//            StructureNodeType.System,
//            commerceFaculty,
//            0);

//        var commerceRegular = CreateNode(
//            "Regular System",
//            StructureNodeType.System,
//            commerceFaculty,
//            1);

//        var accountingProgram = CreateNode(
//            "Accounting Program",
//            StructureNodeType.Program,
//            commerceCreditHours,
//            0);

//        var businessProgram = CreateNode(
//            "Business Administration Program",
//            StructureNodeType.Program,
//            commerceCreditHours,
//            1);

//        var level1 = CreateNode(
//            "First Level",
//            StructureNodeType.Level,
//            accountingProgram,
//            0);

//        var level2 = CreateNode(
//            "Second Level",
//            StructureNodeType.Level,
//            accountingProgram,
//            1);

//        var accountingSpec = CreateNode(
//            "Accounting Specialization",
//            StructureNodeType.Specialization,
//            level2,
//            0);

//        var financeSpec = CreateNode(
//            "Finance Specialization",
//            StructureNodeType.Specialization,
//            level2,
//            1);

//        CreateNode(
//            "Third Level",
//            StructureNodeType.Level,
//            accountingSpec,
//            0);

//        CreateNode(
//            "Fourth Level",
//            StructureNodeType.Level,
//            accountingSpec,
//            1);

//        var csCreditHours = CreateNode(
//            "Credit Hours System",
//            StructureNodeType.System,
//            csFaculty,
//            0);

//        var aiProgram = CreateNode(
//            "Artificial Intelligence Program",
//            StructureNodeType.Program,
//            csCreditHours,
//            0);

//        var cyberProgram = CreateNode(
//            "Cyber Security Program",
//            StructureNodeType.Program,
//            csCreditHours,
//            1);

//        CreateNode(
//            "First Level",
//            StructureNodeType.Level,
//            aiProgram,
//            0);

//        CreateNode(
//            "Second Level",
//            StructureNodeType.Level,
//            aiProgram,
//            1);

//        CreateNode(
//            "First Level",
//            StructureNodeType.Level,
//            cyberProgram,
//            0);

//        await context.StructureNodes.AddRangeAsync(_nodes);

//        await context.SaveChangesAsync();
//    }

//    private static readonly List<StructureNode> _nodes = new();

//    private static StructureNode CreateNode(
//        string name,
//        StructureNodeType type,
//        StructureNode? parent,
//        int order)
//    {
//        var node = new StructureNode
//        {
//            Id = Guid.NewGuid(),

//            Name = name,

//            Type = type,

//            ParentId = parent?.Id,

//            Parent = parent,

//            Order = order,

//            Depth = parent == null
//                ? 0
//                : parent.Depth + 1,

//            IsActive = true
//        };

//        node.Path = parent == null
//            ? $"/{node.Id}"
//            : $"{parent.Path}/{node.Id}";

//        _nodes.Add(node);

//        return node;
//    }
//}

using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class UniversityStructureSeeder
{
    private static readonly List<StructureNode> _nodes = new();

    public static async Task SeedAsync(
        CoreDbContext context)
    {
        if (context.StructureNodes.Any())
            return;

        var university = CreateNode(
            "جامعة العاصمه",
            StructureNodeType.University,
            null,
            0);

        #region كلية الاقتصاد المنزلي

        var homeEconomics = CreateNode(
            "كلية الاقتصاد المنزلي",
            StructureNodeType.Faculty,
            university,
            0);

        var homeCreditHours = CreateNode(
            "نظام الساعات المعتمدة",
            StructureNodeType.System,
            homeEconomics,
            0);

        var homeSemester = CreateNode(
            "نظام الفصول",
            StructureNodeType.System,
            homeEconomics,
            1);

        // التغذية العلاجية
        var dietetics = CreateNode(
            "برنامج التغذية العلاجية",
            StructureNodeType.Program,
            homeCreditHours,
            0);

        CreateLevels(
            dietetics,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // إدارة مؤسسات الأسرة والطفولة
        var familyManagement = CreateNode(
            "إدارة مؤسسات الأسرة والطفولة",
            StructureNodeType.Program,
            homeCreditHours,
            1);

        CreateLevels(
            familyManagement,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // إدارة ورعاية المسنين
        var elderly = CreateNode(
            "إدارة ورعاية المسنين",
            StructureNodeType.Program,
            homeCreditHours,
            2);

        CreateLevels(
            elderly,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // الاقتصاد المنزلي التربوي
        var educationalHome = CreateNode(
            "الاقتصاد المنزلي التربوي",
            StructureNodeType.Program,
            homeCreditHours,
            3);

        CreateLevels(
            educationalHome,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // التدريس لذوي الاحتياجات الخاصة
        var specialNeeds = CreateNode(
            "التدريس لذوي الاحتياجات الخاصة",
            StructureNodeType.Program,
            homeCreditHours,
            4);

        CreateLevels(
            specialNeeds,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // التغذية وعلوم الأطعمة
        var nutrition = CreateNode(
            "التغذية وعلوم الأطعمة",
            StructureNodeType.Program,
            homeCreditHours,
            5);

        CreateLevels(
            nutrition,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // الشعبة العامة
        var generalStream = CreateNode(
            "الشعبة العامة",
            StructureNodeType.Program,
            homeCreditHours,
            6);

        CreateLevels(
            generalStream,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // الصناعات الجلدية
        var leather = CreateNode(
            "الصناعات الجلدية",
            StructureNodeType.Program,
            homeCreditHours,
            7);

        CreateLevels(
            leather,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // الملابس والنسيج
        var textile = CreateNode(
            "الملابس والنسيج",
            StructureNodeType.Program,
            homeCreditHours,
            8);

        CreateLevels(
            textile,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // تكنولوجيا تصنيع الملابس
        var apparel = CreateNode(
            "تكنولوجيا تصنيع الملابس",
            StructureNodeType.Program,
            homeCreditHours,
            9);

        CreateLevels(
            apparel,
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        // نظام الفصول
        var semesterGeneral = CreateNode(
            "البرنامج العام",
            StructureNodeType.Program,
            homeSemester,
            0);

        CreateLevels(
            semesterGeneral,
            "الفرقة الأولى");

        var semesterTextile = CreateNode(
            "الملابس والنسيج",
            StructureNodeType.Program,
            homeSemester,
            1);

        CreateLevels(
            semesterTextile,
            "الفرقة الثانية",
            "الفرقة الثالثة",
            "الفرقة الرابعة");

        var semesterLeather = CreateNode(
            "الصناعات الجلدية",
            StructureNodeType.Program,
            homeSemester,
            2);

        CreateLevels(
            semesterLeather,
            "الفرقة الثانية",
            "الفرقة الثالثة",
            "الفرقة الرابعة");

        #endregion

        #region كلية الهندسة بالمطرية

        var matariaEngineering = CreateNode(
            "كلية الهندسة بالمطرية",
            StructureNodeType.Faculty,
            university,
            1);

        var matariaCreditHours = CreateNode(
            "نظام الساعات المعتمدة",
            StructureNodeType.System,
            matariaEngineering,
            0);

        var matariaSemester = CreateNode(
            "نظام الفصول",
            StructureNodeType.System,
            matariaEngineering,
            1);

        var construction = CreateNode(
            "إدارة المشروعات والتشييد",
            StructureNodeType.Program,
            matariaCreditHours,
            0);

        CreateLevels(
            construction,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior-1",
            "Senior-2");

        var architectureDigital = CreateNode(
            "العمارة بالتكنولوجيا الرقمية",
            StructureNodeType.Program,
            matariaCreditHours,
            1);

        CreateLevels(
            architectureDigital,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior-1",
            "Senior-2");

        var civil = CreateNode(
            "الهندسة المدنية",
            StructureNodeType.Program,
            matariaCreditHours,
            2);

        CreateLevels(
            civil,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var architecture = CreateNode(
            "الهندسة المعمارية",
            StructureNodeType.Program,
            matariaCreditHours,
            3);

        CreateLevels(
            architecture,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var structural = CreateNode(
            "الهندسة الإنشائية",
            StructureNodeType.Program,
            matariaCreditHours,
            4);

        CreateLevels(
            structural,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior-1",
            "Senior-2");

        var energy = CreateNode(
            "هندسة الطاقة",
            StructureNodeType.Program,
            matariaCreditHours,
            5);

        CreateLevels(
            energy,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior-1",
            "Senior-2");

        var automotive = CreateNode(
            "هندسة السيارات والجرارات",
            StructureNodeType.Program,
            matariaCreditHours,
            6);

        CreateLevels(
            automotive,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var mechatronics = CreateNode(
            "هندسة الميكاترونيات بالسيارات",
            StructureNodeType.Program,
            matariaCreditHours,
            7);

        CreateLevels(
            mechatronics,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        // نظام الفصول
        var matariaCivilSemester = CreateNode(
            "الهندسة المدنية",
            StructureNodeType.Program,
            matariaSemester,
            0);

        CreateLevels(
            matariaCivilSemester,
            "الفرقة الأولى",
            "الفرقة الثانية",
            "الفرقة الثالثة",
            "الفرقة الرابعة");

        var mechanicalDesign = CreateNode(
            "التصميم الميكانيكي",
            StructureNodeType.Program,
            matariaSemester,
            1);

        CreateLevels(
            mechanicalDesign,
            "الفرقة الأولى",
            "الفرقة الثانية",
            "الفرقة الثالثة",
            "الفرقة الرابعة");

        #endregion

        #region كلية الهندسة بحلوان

        var helwanEngineering = CreateNode(
            "كلية الهندسة بحلوان",
            StructureNodeType.Faculty,
            university,
            2);

        var helwanCreditHours = CreateNode(
            "نظام الساعات المعتمدة",
            StructureNodeType.System,
            helwanEngineering,
            0);

        var helwanSemester = CreateNode(
            "نظام الفصول",
            StructureNodeType.System,
            helwanEngineering,
            1);

        var helwanGeneral = CreateNode(
            "البرنامج العام",
            StructureNodeType.Program,
            helwanCreditHours,
            0);

        CreateLevels(
            helwanGeneral,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var biomedical = CreateNode(
            "الهندسة الحيوية الطبية",
            StructureNodeType.Program,
            helwanCreditHours,
            1);

        CreateLevels(
            biomedical,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var industrial = CreateNode(
            "الهندسة الصناعية",
            StructureNodeType.Program,
            helwanCreditHours,
            2);

        CreateLevels(
            industrial,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var communication = CreateNode(
            "هندسة الاتصالات والمعلومات",
            StructureNodeType.Program,
            helwanCreditHours,
            3);

        CreateLevels(
            communication,
            "إعدادي",
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        var electrical = CreateNode(
            "هندسة القوى والوقاية الكهربية",
            StructureNodeType.Program,
            helwanCreditHours,
            4);

        CreateLevels(
            electrical,
            "إعدادي",
            "الأول",
            "الثاني",
            "الثالث",
            "الرابع");

        var computer = CreateNode(
            "هندسة الحاسبات والنظم",
            StructureNodeType.Program,
            helwanCreditHours,
            5);

        CreateLevels(
            computer,
            "Sophomore",
            "Junior",
            "Senior");

        var electronics = CreateNode(
            "هندسة الإلكترونيات والاتصالات",
            StructureNodeType.Program,
            helwanCreditHours,
            6);

        CreateLevels(
            electronics,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var production = CreateNode(
            "هندسة الإنتاج",
            StructureNodeType.Program,
            helwanCreditHours,
            7);

        CreateLevels(
            production,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var power = CreateNode(
            "هندسة القوى والآلات الكهربائية",
            StructureNodeType.Program,
            helwanCreditHours,
            8);

        CreateLevels(
            power,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        var helwanMechatronics = CreateNode(
            "هندسة الميكاترونيات",
            StructureNodeType.Program,
            helwanCreditHours,
            9);

        CreateLevels(
            helwanMechatronics,
            "FreshMan",
            "Sophomore",
            "Junior",
            "Senior");

        // نظام الفصول
        var helwanSemesterGeneral = CreateNode(
            "البرنامج العام لائحة 2020",
            StructureNodeType.Program,
            helwanSemester,
            0);

        CreateLevels(
            helwanSemesterGeneral,
            "إعدادي");

        var semesterElectronics = CreateNode(
            "هندسة الإلكترونيات والاتصالات",
            StructureNodeType.Program,
            helwanSemester,
            1);

        CreateLevels(
            semesterElectronics,
            "الفرقة الأولى",
            "الفرقة الثانية",
            "الفرقة الثالثة",
            "الفرقة الرابعة");

        var semesterPower = CreateNode(
            "هندسة القوى والآلات الكهربية",
            StructureNodeType.Program,
            helwanSemester,
            2);

        CreateLevels(
            semesterPower,
            "الفرقة الأولى",
            "الفرقة الثانية",
            "الفرقة الثالثة",
            "الفرقة الرابعة");

        var semesterBiomedical = CreateNode(
            "الهندسة الحيوية الطبية",
            StructureNodeType.Program,
            helwanSemester,
            3);

        CreateLevels(
            semesterBiomedical,
            "الفرقة الأولى",
            "الفرقة الثانية",
            "الفرقة الثالثة",
            "الفرقة الرابعة");

        #endregion

        await context.StructureNodes.AddRangeAsync(_nodes);

        await context.SaveChangesAsync();
    }

    private static void CreateLevels(
        StructureNode parent,
        params string[] levels)
    {
        for (int i = 0; i < levels.Length; i++)
        {
            CreateNode(
                levels[i],
                StructureNodeType.Level,
                parent,
                i);
        }
    }

    private static StructureNode CreateNode(
        string name,
        StructureNodeType type,
        StructureNode? parent,
        int order)
    {
        var node = new StructureNode
        {
            Id = Guid.NewGuid(),

            Name = name,

            Type = type,

            ParentId = parent?.Id,

            Parent = parent,

            Order = order,

            Depth = parent == null
                ? 0
                : parent.Depth + 1,

            IsActive = true
        };

        node.Path = parent == null
            ? $"/{node.Id}"
            : $"{parent.Path}/{node.Id}";

        _nodes.Add(node);

        return node;
    }
}