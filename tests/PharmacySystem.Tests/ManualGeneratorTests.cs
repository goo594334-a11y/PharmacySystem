using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PharmacySystem.Tests;

public class ManualGeneratorTests
{
    [Fact]
    public void GenerateArabicUserManual_CreatesProfessionalWordDocument()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "PharmacySystem.sln")) && !File.Exists(Path.Combine(dir.FullName, "PharmacySystem.csproj")))
        {
            dir = dir.Parent;
        }
        var targetDir = dir?.FullName ?? @"D:\Project\system\Pharmacy System";
        var absolutePath = Path.Combine(targetDir, "Pharmacy_System_User_Manual_AR.docx");

        CreateDocx(absolutePath);

        Assert.True(File.Exists(absolutePath), "User manual file must exist.");
        var fileInfo = new FileInfo(absolutePath);
        Assert.True(fileInfo.Length > 8000, "User manual should be a comprehensive document over 8KB zipped.");
    }

    private static void CreateDocx(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        using var zip = ZipFile.Open(filePath, ZipArchiveMode.Create);

        // 1. [Content_Types].xml
        var contentTypes = zip.CreateEntry("[Content_Types].xml");
        using (var writer = new StreamWriter(contentTypes.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
  <Override PartName=""/word/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml""/>
</Types>");
        }

        // 2. _rels/.rels
        var rootRels = zip.CreateEntry("_rels/.rels");
        using (var writer = new StreamWriter(rootRels.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
</Relationships>");
        }

        // 3. word/_rels/document.xml.rels
        var docRels = zip.CreateEntry("word/_rels/document.xml.rels");
        using (var writer = new StreamWriter(docRels.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>
</Relationships>");
        }

        // 4. word/styles.xml
        var styles = zip.CreateEntry("word/styles.xml");
        using (var writer = new StreamWriter(styles.Open(), Encoding.UTF8))
        {
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:styles xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
  <w:docDefaults>
    <w:rPrDefault>
      <w:rPr>
        <w:rFonts w:ascii=""Segoe UI"" w:hAnsi=""Segoe UI"" w:cs=""Arial""/>
        <w:sz w:val=""22""/>
        <w:szCs w:val=""22""/>
        <w:lang w:val=""en-US"" w:bidi=""ar-EG""/>
      </w:rPr>
    </w:rPrDefault>
  </w:docDefaults>
</w:styles>");
        }

        // 5. word/document.xml
        var doc = zip.CreateEntry("word/document.xml");
        using (var writer = new StreamWriter(doc.Open(), Encoding.UTF8))
        {
            writer.Write(BuildManualDocumentXml());
        }
    }

    private static string BuildManualDocumentXml()
    {
        var sb = new StringBuilder();
        sb.Append(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<w:document xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main"">
<w:body>");

        // Document Cover / Header
        sb.Append(MakeTitle("دليل التشغيل والاستخدام الشامل لنظام إدارة الصيدليات الذكي"));
        sb.Append(MakeSubtitle("Pharmacy Management System — Complete User & Operational Manual"));
        sb.Append(MakeParagraph("الإصدار: النهائي للإنتاج (Release 2.0 - .NET 10) | تاريخ الاعتماد: أكتوبر 2026 | إعداد: الفريق الهندسي لنظم الصيدليات", true, "64748B", 18));
        sb.Append(MakeDivider());

        // Callout Box
        sb.Append(MakeCallout("ملاحظة هامة للمديرين والمستخدمين:", "تم تصميم هذا الدليل ليكون مرجعاً عملياً وتشغيلياً يومياً لكافة أدوار العمل داخل الصيدلية (مدير النظام، الصيدلي، الكاشير، ومدير الحسابات). يرجى الالتزام بالتعليمات الواردة لضمان دقة الرصيد المخزني وسلامة العمليات المالية وفق المعايير المعتمدة.", "0284C7", "F0F9FF"));

        // Table of Contents
        sb.Append(MakeHeading1("فهرس المحتويات"));
        sb.Append(MakeBulletItem("1. مقدمة عن النظام والواجهة التفاعلية الحديثة"));
        sb.Append(MakeBulletItem("2. تسجيل الدخول، أمان الحسابات، واستعادة كلمة المرور (OTP)"));
        sb.Append(MakeBulletItem("3. المظهر وتخصيص بيئة العمل (الوضع الليلي والشريط الجانبي القابل للطي)"));
        sb.Append(MakeBulletItem("4. نظام إدارة الأدوية ووحدات التعبئة والتجزئة المتقدم"));
        sb.Append(MakeBulletItem("5. دورة المشتريات، إدارة الموردين، وإدخال التشغيلات"));
        sb.Append(MakeBulletItem("6. نقطة البيع السريعة (POS) وسياسة الصرف (FEFO) واختصارات الكيبورد"));
        sb.Append(MakeBulletItem("7. نظام المرتجعات والتسويات المخزنية وإعدام التوالف"));
        sb.Append(MakeBulletItem("8. إدارة الصلاحيات والعزل التام لبيانات الصيدليات (Multi-Tenant Isolation)"));
        sb.Append(MakeBulletItem("9. بوابة المطور والموافقة على الصيدليات الجديدة (Developer Portal)"));
        sb.Append(MakeBulletItem("10. التقارير المالية وحركات الصندوق والتدقيق الشامل"));
        sb.Append(MakeDivider());

        // Section 1
        sb.Append(MakeHeading1("1. مقدمة عن النظام والواجهة التفاعلية الحديثة"));
        sb.Append(MakeParagraph("نظام إدارة الصيدليات هو منصة سحابية وهجينة متطورة مبنية بأحدث تقنيات دوت نت (.NET 10) وASP.NET Core MVC وEF Core وSQL Server. يهدف النظام إلى تحويل الصيدلية إلى بيئة عمل رقمية ذكية خالية من الأخطاء الورقية والحسابية، مع دعم كامل للغتين العربية (RTL) والإنجليزية (LTR)."));
        sb.Append(MakeParagraph("أبرز المزايا التنافسية للنظام:"));
        sb.Append(MakeBulletItem("مرونة فائقة في شجرة تعبئة الأدوية (علبة، شريط، قرص، أمبولة، فيال، زجاجة، ملليلتر، أنبوبة، جرام)."));
        sb.Append(MakeBulletItem("التطبيق الآلي واللحظي لقاعدة ما ينتهي أولاً يخرج أولاً (FEFO) لمنع ركود وتلف الأدوية."));
        sb.Append(MakeBulletItem("نقطة بيع فورية (POS) مزودة بأزرار تبديل الوحدات السريعة، تقسيم المدفوعات، ودعم الباركود."));
        sb.Append(MakeBulletItem("عزل كامل ومتعدد المستأجرين (Multi-Tenant Isolation) لحماية خصوصية وسرية كل صيدلية."));
        sb.Append(MakeBulletItem("بوابة متقدمة للمطور (Developer Portal) للموافقة على اشتراكات الصيدليات ومراقبة النظام."));

        // Section 2
        sb.Append(MakeHeading1("2. تسجيل الدخول وأمان الحسابات واستعادة كلمة المرور"));
        sb.Append(MakeParagraph("يوفر النظام حماية متعددة الطبقات لحسابات الموظفين تمنع الوصول غير المصرح به:"));
        sb.Append(MakeBulletItem("تسجيل الدخول الآمن: يتم عبر البريد الإلكتروني وكلمة المرور المشفرة بتقنية PBKDF2/Identity V3."));
        sb.Append(MakeBulletItem("الحماية من هجمات التخمين (Brute-Force): عند تكرار إدخال كلمة المرور بشكل خاطئ 5 مرات متتالية، يتم قفل الحساب تلقائياً لمدة 15 دقيقة، وتسجيل محاولة الاختراق في سجل المراجعة (Audit Log)."));
        sb.Append(MakeBulletItem("استعادة كلمة المرور عبر الرمز لمرة واحدة (OTP): يمكن للمستخدم النقر على 'نسيت كلمة المرور'، حيث يرسل النظام رمز تحقق مكون من 6 أرقام صالح لمدة 15 دقيقة فقط، مع قيود صارمة تمنع إعادة الإرسال المتكرر."));
        sb.Append(MakeBulletItem("إلزامية تغيير كلمة المرور: يمكن للمدير تحديد خيار إلزام الموظف الجديد بتغيير كلمة المرور المؤقتة فور أول دخول."));

        // Section 3
        sb.Append(MakeHeading1("3. المظهر والوضع الليلي والشريط الجانبي القابل للطي"));
        sb.Append(MakeParagraph("حرصاً على راحة أعين الصيادلة أثناء فترات العمل الطويلة والمناوبات الليلية، تم تزويد النظام بنظام سمات عالمي دقيق:"));
        sb.Append(MakeBulletItem("الوضع الليلي والنهاري (Dark/Light Mode): يتم التبديل بنقرة واحدة من أيقونة القمر/الشمس في الشريط العلوي. يغير النظام ألوان كافة الجداول، النوافذ المنبثقة، القوائم المنسدلة، والبطاقات إلى تباين مريح للعين بدون أي وهج أبيض غير مرغوب فيه."));
        sb.Append(MakeBulletItem("تذكر التفضيلات: يتم حفظ اختيار الوضع الليلي في المتصفح تلقائياً عبر LocalStorage ويتم تطبيقه فور فتح أي صفحة."));
        sb.Append(MakeBulletItem("الشريط الجانبي القابل للطي (Collapsible Sidebar Rail): يمكن طي القائمة الجانبية لتوفير مساحة شاشة قصوى على شاشات الكاشير ونقاط البيع، مع ظهور تلميحات (Tooltips) للأيقونات عند التمرير."));

        // Section 4
        sb.Append(MakeHeading1("4. نظام إدارة الأدوية ووحدات التعبئة والتجزئة المتقدم"));
        sb.Append(MakeParagraph("تختلف الأدوية في الصيدلية جذرياً في طبيعة تغليفها وتجزئتها. يرفض النظام فرض قالب تعبئة موحد، بل يوفر هيكلية ديناميكية لكل منتج وفق تصنيفه:"));

        // Packaging Table
        sb.Append(MakeTable(
            new[] { "التصنيف الصيدلاني", "الوحدة الأساسية (Base Unit)", "وحدات التعبئة الأعلى", "مثال عملي وطريقة التجزئة" },
            new[]
            {
                new[] { "الأقراص والكبسولات (Tablets)", "قرص / كبسولة (Tablet)", "شريط (Strip) / علبة (Box)", "علبة بنادول = 3 شرائط = 30 قرص (الوحدة الأساسية: قرص)" },
                new[] { "الحقن والأمبولات (Injections)", "أمبولة / فيال (Ampoule/Vial)", "علبة (Box) / باكت (Pack)", "علبة فولتارين = 5 أمبولات (الوحدة الأساسية: أمبولة)" },
                new[] { "الأشربة والسوائل (Syrups)", "زجاجة (Bottle)", "كرتونة (Carton) / باكت", "كرتونة كحة = 12 زجاجة (الوحدة الأساسية: زجاجة)" },
                new[] { "المراهم والكريمات (Creams)", "أنبوبة (Tube)", "كرتونة (Carton) / علبة", "كرتونة مرهم مضاد = 24 أنبوبة (الوحدة الأساسية: أنبوبة)" },
                new[] { "الأكياس والفوارات (Sachets)", "كيس (Sachet)", "علبة (Box)", "علبة فوار = 10 أكياس (الوحدة الأساسية: كيس)" },
                new[] { "المستلزمات الطبية (Supplies)", "قطعة (Piece)", "باكت (Pack) / كرتونة", "كرتونة سرنجات = 20 باكت = 200 سرنجة" }
            }
        ));

        sb.Append(MakeCallout("قاعدة جوهرية في احتساب المخزون (The Golden Inventory Rule):", "يتم دائماً تخزين كميات المخزون والتشغيلات بالوحدة الأساسية (Base Unit). عندما يشتري الصيدلي 100 علبة بنادول (تحتوي كل علبة على 30 قرص)، يُنشئ النظام رصيداً مقداره 3,000 قرص في التشغيلة، ويحسب تكلفة الشراء للقرص الواحد بتقسيم سعر العلبة على 30 تلقائياً وبأعلى دقة حسابية.", "059669", "F0FDF4"));

        // Section 5
        sb.Append(MakeHeading1("5. دورة المشتريات وإدارة الموردين والتشغيلات"));
        sb.Append(MakeParagraph("تعتبر فاتورة الشراء هي المنفذ الأساسي لدخول البضائع والتشغيلات إلى الصيدلية:"));
        sb.Append(MakeBulletItem("إدخال فاتورة الشراء: اختر المورد، رقم فاتورة الشراء، والمخزن أو الرف."));
        sb.Append(MakeBulletItem("الشراء بالوحدة الكبرى: يدخل الصيدلي الكمية المشتراة بوحدة 'علبة' وسعر شراء العلبة، مع تحديد رقم التشغيلة (Batch Number) وتاريخ الصلاحية (Expiry Date)."));
        sb.Append(MakeBulletItem("التحويل التلقائي: يحسب النظام تلقائياً الكمية المكافئة بالوحدات الصغرى وسعر التكلفة لكل وحدة صغرى."));
        sb.Append(MakeBulletItem("تأكيد الفاتورة (Confirm): يتم قفل الفاتورة لمنع التلاعب، وتوليد التشغيلات فوراً وزيادة رصيد المستودع، مع تحديث حساب المورد والمبالغ الآجلة أو المدفوعة."));

        // Section 6
        sb.Append(MakeHeading1("6. نقطة البيع السريعة (POS) وقاعدة (FEFO) واختصارات لوحة المفاتيح"));
        sb.Append(MakeParagraph("شاشة نقطة البيع مصممة هندسياً للعمل فائق السرعة أثناء أوقات الذروة وازدحام العملاء:"));
        sb.Append(MakeBulletItem("البحث والمسح الضوئي: البحث الفوري باسم الدواء، المادة الفعالة، أو مسح الباركود بالماسح الضوئي."));
        sb.Append(MakeBulletItem("أزرار التبديل السريع للوحدات: بمجرد اختيار الدواء، تظهر أزرار سريعة لاختيار (علبة / شريط / قرص) مع السعر المحدد لكل وحدة."));
        sb.Append(MakeBulletItem("تطبيق FEFO الإجباري: لا يقوم الصيدلي باختيار رقم التشغيلة يدوياً؛ يفرز النظام آلياً التشغيلات الأقرب انتهاءً ويصرف منها أولاً، ويحظر تماماً بيع أي تشغيلة منتهية الصلاحية."));
        sb.Append(MakeBulletItem("منع البيع بالسالب: يتحقق النظام على الخادم (Server-Side) من توفر الرصيد الفعلي قبل خصم أي وحدة، ويمنع بيع أي صنف ينفد رصيده."));
        sb.Append(MakeBulletItem("تقسيم المدفوعات (Split Payment): دعم سداد جزء نقداً والباقي ببطاقة الائتمان أو رصيد آجل لعميل مسجل."));

        // Keyboard Shortcuts Table
        sb.Append(MakeHeading2("جدول اختصارات لوحة المفاتيح لكاشير الصيدلية"));
        sb.Append(MakeTable(
            new[] { "المفتاح (Shortcut)", "الوظيفة التشغيلية", "الهدف من الاختصار" },
            new[]
            {
                new[] { "F1 أو Ctrl + K", "التركيز على حقل البحث عن الأدوية", "البدء الفوري في كتابة اسم الدواء أو مسح الباركود" },
                new[] { "F2", "فتح نافذة الدفع وإتمام الفاتورة", "الانتقال السريع لإنهاء المعاملة وطباعة الإيصال" },
                new[] { "F4", "تطبيق خصم عام على الفاتورة", "إدخال نسبة أو قيمة الخصم المعتمدة للعميل" },
                new[] { "Esc", "إلغاء المعاملة الحالية أو إغلاق النوافذ", "تنظيف شاشة الكاشير لبدء عملية جديدة" },
                new[] { "+ / -", "زيادة أو إنقاص كمية الصنف المحدد", "تعديل الكمية المباعة بسرعة بدون استخدام الفأرة" }
            }
        ));

        // Section 7
        sb.Append(MakeHeading1("7. نظام المرتجعات والتسويات المخزنية وإعدام التوالف"));
        sb.Append(MakeParagraph("يوفر النظام آليات محكمة لضبط حركة المخزون المعكوسة ومنع أي عجز أو تلاعب:"));
        sb.Append(MakeBulletItem("مرتجع المبيعات (Sales Return): يتم بإدخال رقم الفاتورة الأصلية. يعرض النظام الأصناف والكميات المباعة بوحداتها الأصلية والتشغيلات التي خُصمت منها، ويسمح بالمرتجع الكلي أو الجزئي مع إعادة البضاعة السليمة للتشغيلة الأصلية واسترداد المبلغ للعميل."));
        sb.Append(MakeBulletItem("مرتجع المشتريات (Purchase Return): لإرجاع بضاعة معيبة أو قريبة الانتهاء إلى المورد، مع خصم رصيدها من المخزن وحسابه المالي."));
        sb.Append(MakeBulletItem("تسوية المخزون والتوالف والكسور (Stock Adjustments): ميزة مخصصة لتصحيح فروق الجرد الدوري، أو إثبات زجاجة دواء كُسرت أثناء النقل، مع تحديد سبب التسوية والمستخدم المسؤول وتسجيلها في سجل المراجعة."));
        sb.Append(MakeBulletItem("إعدام الأدوية منتهية الصلاحية (Expired Disposal): تصفية التشغيلات المنتهية رسمياً وإخراجها من التقييم المخزني وتحميل قيمتها لحساب التوالف."));

        // Section 8
        sb.Append(MakeHeading1("8. إدارة الصلاحيات والعزل التام لبيانات الصيدليات (Multi-Tenant)"));
        sb.Append(MakeParagraph("يعتمد النظام مبدأ الحماية الفاشلة بالإغلاق (Fail-Closed Multi-Tenancy):"));
        sb.Append(MakeBulletItem("كل صيدلية تمثل كياناً مستقلاً تماماً (Tenant). لا يمكن لأي مستخدم، مديراً كان أو صيدلياً، رؤية أو استعلام أي منتج، مبيعات، عملاء، أو موردين يخصون صيدلية أخرى."));
        sb.Append(MakeBulletItem("تطبق مرشحات الاستعلام العامة (Global Query Filters) على مستوى قاعدة البيانات وتمنع تسريب السجلات برمجياً حتى في حال الخطأ البشري."));
        sb.Append(MakeBulletItem("فصل أدوار الموظفين:"));
        sb.Append(MakeBulletItem("  - مدير الصيدلية (Admin): إدارة الموظفين، إعدادات المنتجات، فواتير الشراء، والتقارير المالية."));
        sb.Append(MakeBulletItem("  - الصيدلي / الكاشير (Pharmacist): عمليات البيع، البحث عن الأدوية، وإصدار مرتجعات البيع المصرح بها."));

        // Section 9
        sb.Append(MakeHeading1("9. بوابة المطور والموافقة على الصيدليات الجديدة (Developer Portal)"));
        sb.Append(MakeParagraph("عند تسجيل صيدلية جديدة من خلال صفحة التسجيل العامة، لا تدخل الصيدلية مباشرة في حيز التشغيل حفاظاً على المعايير المؤسسية:"));
        sb.Append(MakeBulletItem("تدخل الصيدلية فور التسجيل في حالة 'قيد المراجعة والموافقة' (Pending Approval) ويتم حجب تسجيل دخول مسؤوليها مؤقتاً."));
        sb.Append(MakeBulletItem("يتلقى المطور إشعاراً في لوحة التحكم المركزية (/Developer) لمراجعة بيانات الترخيص والصيدلية."));
        sb.Append(MakeBulletItem("إجراءات المطور:"));
        sb.Append(MakeBulletItem("  1. الموافقة (Approve): تفعيل الصيدلية وتوليد كود رسمي لها وتنشيط حساب مديرها، وإرسال بريد ترحيبي."));
        sb.Append(MakeBulletItem("  2. الرفض (Reject): رفض الطلب غير المستوفي للشروط مع ذكر السبب."));
        sb.Append(MakeBulletItem("  3. التعليق (Suspend): إيقاف نشاط أي صيدلية متعثرة أو مخالفة للشروط فوراً مع منع دخول كافة مستخدميها."));
        sb.Append(MakeBulletItem("  4. إعادة التنشيط (Activate): إعادة تشغيل الصيدليات المعلقة بعد زوال سبب التعليق."));

        // Section 10
        sb.Append(MakeHeading1("10. التقارير المالية وحركات الصندوق والتدقيق الشامل"));
        sb.Append(MakeParagraph("لوحة تقارير تفاعلية تمنح إدارة الصيدلية رؤية شاملة ولحظية:"));
        sb.Append(MakeBulletItem("تقرير الأرباح والخسائر: حساب صافي الأرباح على مستوى كل صنف وكل فاتورة بمقارنة سعر البيع الفعلي مع سعر تكلفة التشغيلة المصروفة."));
        sb.Append(MakeBulletItem("تقرير حركة الخزينة والصندوق (Daily Cash Flow): مطابقة المبيعات النقدية والإلكترونية وحساب العجز أو الزيادة في نهاية كل وردية."));
        sb.Append(MakeBulletItem("تقرير النواقص وحد الطلب (Low Stock Alert): تنبيه استباقي بالأدوية التي وصل رصيدها إلى حد الأمان لتوليد أمر شراء للموردين."));
        sb.Append(MakeBulletItem("تقرير تواريخ الانتهاء الوشيكة (Near-Expiry Alert): حصر الأدوية التي ستنتهي صلاحيتها خلال (30، 60، أو 90 يوماً) لاتخاذ الإجراء المناسب."));
        sb.Append(MakeBulletItem("سجل المراجعة والتدقيق (Audit Trail): رصد دقيق لكافة العمليات الحساسة (تعديل أسعار، تسوية كميات، حذف فواتير، تسجيل الدخول) متضمناً اسم الموظف، التوقيت بالثانية، وعنوان الـ IP."));

        // Footer / Signature
        sb.Append(MakeDivider());
        sb.Append(MakeParagraph("نهاية دليل التشغيل — مع أطيب تمنياتنا بإدارة صيدلانية متكاملة وناجحة.", true, "0F766E", 22));

        sb.Append(@"</w:body></w:document>");
        return sb.ToString();
    }

    private static string MakeTitle(string text)
    {
        return $@"<w:p><w:pPr><w:bidi/><w:jc w:val=""center""/><w:spacing w:before=""240"" w:after=""120""/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val=""44""/><w:szCs w:val=""44""/><w:color w:val=""0F766E""/><w:rtl/></w:rPr><w:t>{Escape(text)}</w:t></w:r></w:p>";
    }

    private static string MakeSubtitle(string text)
    {
        return $@"<w:p><w:pPr><w:bidi/><w:jc w:val=""center""/><w:spacing w:before=""60"" w:after=""180""/></w:pPr><w:r><w:rPr><w:i/><w:sz w:val=""26""/><w:szCs w:val=""26""/><w:color w:val=""0284C7""/><w:rtl/></w:rPr><w:t>{Escape(text)}</w:t></w:r></w:p>";
    }

    private static string MakeHeading1(string text)
    {
        return $@"<w:p><w:pPr><w:bidi/><w:jc w:val=""right""/><w:spacing w:before=""360"" w:after=""140""/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val=""32""/><w:szCs w:val=""32""/><w:color w:val=""0F766E""/><w:rtl/></w:rPr><w:t>{Escape(text)}</w:t></w:r></w:p>";
    }

    private static string MakeHeading2(string text)
    {
        return $@"<w:p><w:pPr><w:bidi/><w:jc w:val=""right""/><w:spacing w:before=""240"" w:after=""100""/></w:pPr><w:r><w:rPr><w:b/><w:sz w:val=""26""/><w:szCs w:val=""26""/><w:color w:val=""0369A1""/><w:rtl/></w:rPr><w:t>{Escape(text)}</w:t></w:r></w:p>";
    }

    private static string MakeParagraph(string text, bool bold = false, string color = "1E293B", int size = 22)
    {
        var bTag = bold ? "<w:b/>" : "";
        return $@"<w:p><w:pPr><w:bidi/><w:jc w:val=""right""/><w:spacing w:before=""60"" w:after=""100""/><w:ind w:right=""100""/></w:pPr><w:r><w:rPr>{bTag}<w:sz w:val=""{size}""/><w:szCs w:val=""{size}""/><w:color w:val=""{color}""/><w:rtl/></w:rPr><w:t>{Escape(text)}</w:t></w:r></w:p>";
    }

    private static string MakeBulletItem(string text)
    {
        return $@"<w:p><w:pPr><w:bidi/><w:jc w:val=""right""/><w:spacing w:before=""40"" w:after=""60""/><w:ind w:right=""360""/></w:pPr><w:r><w:rPr><w:sz w:val=""22""/><w:szCs w:val=""22""/><w:color w:val=""334155""/><w:rtl/></w:rPr><w:t>• {Escape(text)}</w:t></w:r></w:p>";
    }

    private static string MakeDivider()
    {
        return @"<w:p><w:pPr><w:bidi/><w:pBdr><w:bottom w:val=""single"" w:sz=""12"" w:space=""6"" w:color=""CBD5E1""/></w:pPr><w:spacing w:before=""180"" w:after=""180""/></w:pPr></w:p>";
    }

    private static string MakeCallout(string title, string body, string borderColor, string bgColor)
    {
        return $@"<w:tbl>
  <w:tblPr>
    <w:tblBidi/>
    <w:tblW w:w=""5000"" w:type=""pct""/>
    <w:tblBorders>
      <w:top w:val=""none""/>
      <w:left w:val=""none""/>
      <w:bottom w:val=""none""/>
      <w:right w:val=""single"" w:sz=""36"" w:color=""{borderColor}""/>
      <w:insideH w:val=""none""/>
      <w:insideV w:val=""none""/>
    </w:tblBorders>
  </w:tblPr>
  <w:tr>
    <w:tc>
      <w:tcPr>
        <w:tcW w:w=""5000"" w:type=""pct""/>
        <w:shd w:val=""clear"" w:color=""auto"" w:fill=""{bgColor}""/>
        <w:tcMar>
          <w:top w:w=""160""/>
          <w:bottom w:w=""160""/>
          <w:right w:w=""240""/>
          <w:left w:w=""240""/>
        </w:tcMar>
      </w:tcPr>
      <w:p>
        <w:pPr><w:bidi/><w:jc w:val=""right""/><w:spacing w:after=""60""/></w:pPr>
        <w:r><w:rPr><w:b/><w:sz w:val=""22""/><w:szCs w:val=""22""/><w:color w:val=""{borderColor}""/><w:rtl/></w:rPr><w:t>{Escape(title)}</w:t></w:r>
      </w:p>
      <w:p>
        <w:pPr><w:bidi/><w:jc w:val=""right""/><w:spacing w:after=""40""/></w:pPr>
        <w:r><w:rPr><w:sz w:val=""20""/><w:szCs w:val=""20""/><w:color w:val=""1E293B""/><w:rtl/></w:rPr><w:t>{Escape(body)}</w:t></w:r>
      </w:p>
    </w:tc>
  </w:tr>
</w:tbl>
<w:p><w:pPr><w:spacing w:after=""120""/></w:pPr></w:p>";
    }

    private static string MakeTable(string[] headers, string[][] rows)
    {
        var sb = new StringBuilder();
        sb.Append(@"<w:tbl>
  <w:tblPr>
    <w:tblBidi/>
    <w:tblW w:w=""5000"" w:type=""pct""/>
    <w:tblBorders>
      <w:top w:val=""single"" w:sz=""6"" w:color=""CBD5E1""/>
      <w:left w:val=""single"" w:sz=""6"" w:color=""CBD5E1""/>
      <w:bottom w:val=""single"" w:sz=""6"" w:color=""CBD5E1""/>
      <w:right w:val=""single"" w:sz=""6"" w:color=""CBD5E1""/>
      <w:insideH w:val=""single"" w:sz=""4"" w:color=""E2E8F0""/>
      <w:insideV w:val=""single"" w:sz=""4"" w:color=""E2E8F0""/>
    </w:tblBorders>
  </w:tblPr>");

        // Header Row
        sb.Append("<w:tr><w:trPr><w:tblHeader/></w:trPr>");
        foreach (var h in headers)
        {
            sb.Append($@"<w:tc>
  <w:tcPr>
    <w:shd w:val=""clear"" w:color=""auto"" w:fill=""0F766E""/>
    <w:tcMar><w:top w:w=""120""/><w:bottom w:w=""120""/><w:right w:w=""140""/><w:left w:w=""140""/></w:tcMar>
  </w:tcPr>
  <w:p><w:pPr><w:bidi/><w:jc w:val=""center""/></w:pPr><w:r><w:rPr><w:b/><w:color w:val=""FFFFFF""/><w:sz w:val=""20""/><w:szCs w:val=""20""/><w:rtl/></w:rPr><w:t>{Escape(h)}</w:t></w:r></w:p>
</w:tc>");
        }
        sb.Append("</w:tr>");

        // Data Rows
        var isAlt = false;
        foreach (var row in rows)
        {
            var bg = isAlt ? "F8FAFC" : "FFFFFF";
            isAlt = !isAlt;
            sb.Append("<w:tr>");
            foreach (var cell in row)
            {
                sb.Append($@"<w:tc>
  <w:tcPr>
    <w:shd w:val=""clear"" w:color=""auto"" w:fill=""{bg}""/>
    <w:tcMar><w:top w:w=""100""/><w:bottom w:w=""100""/><w:right w:w=""120""/><w:left w:w=""120""/></w:tcMar>
  </w:tcPr>
  <w:p><w:pPr><w:bidi/><w:jc w:val=""right""/></w:pPr><w:r><w:rPr><w:color w:val=""1E293B""/><w:sz w:val=""19""/><w:szCs w:val=""19""/><w:rtl/></w:rPr><w:t>{Escape(cell)}</w:t></w:r></w:p>
</w:tc>");
            }
            sb.Append("</w:tr>");
        }

        sb.Append("</w:tbl><w:p><w:pPr><w:spacing w:after=\"140\"/></w:pPr></w:p>");
        return sb.ToString();
    }

    private static string Escape(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
