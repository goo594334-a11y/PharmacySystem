using System.Collections.Generic;
using System.IO;
using System.Xml;
using Xunit;

namespace PharmacySystem.Tests;

public class ResxSyncTests
{
    [Fact]
    public void VerifyAndSynchronizeRequiredResourceKeys()
    {
        var requiredKeys = new Dictionary<string, (string ar, string en)>
        {
            ["Language_Arabic"] = ("العربية", "Arabic"),
            ["Language_English"] = ("الإنجليزية", "English"),
            ["SaleMustContainAtLeastOneItem"] = ("يجب أن تحتوي الفاتورة على صنف واحد على الأقل", "Sale invoice must contain at least one item"),
            ["PaymentMethod_VodafoneCash"] = ("فودافون كاش", "Vodafone Cash"),
            ["PaymentMethod_Split"] = ("دفع مجزأ", "Split Payment"),
            ["SplitPaymentBreakdown"] = ("توزيع الدفع المجزأ", "Split Payment Breakdown"),
            ["RemainingToCover"] = ("المتبقي للتغطية:", "Remaining to Cover:"),
            ["FullyCovered"] = ("مغطى بالكامل (0.00)", "Fully Covered (0.00)"),
            ["SplitSumMismatchError"] = ("مجموع مبالغ الدفع المجزأ يجب أن يساوي إجمالي الفاتورة بالضبط", "Total split payment amounts must exactly equal invoice total"),
            ["NoSalesYet"] = ("لا توجد مبيعات بعد", "No sales recorded yet"),
            ["NetSalesToday"] = ("صافي المبيعات اليوم", "Today Net Sales"),
            ["NetRevenue"] = ("صافي الإيراد", "Net Revenue"),
            ["GrossSales"] = ("إجمالي المبيعات", "Gross Sales"),
            ["MonthlyNetSales"] = ("صافي مبيعات الشهر", "Monthly Net Sales"),
            ["MonthlyGross"] = ("إجمالي الشهر", "Monthly Gross"),
            ["PaymentBreakdownToday"] = ("طرق الدفع اليوم", "Today's Payment Methods"),
            ["Methods"] = ("الطرق", "Methods"),
            ["SendOtpCode"] = ("إرسال رمز التحقق (OTP)", "Send Verification Code (OTP)"),
            ["VerifyOtpTitle"] = ("التحقق من رمز الأمان (OTP)", "Security Code Verification (OTP)"),
            ["OtpSentNotice"] = ("تم إرسال رمز تحقق مكوّن من 6 أرقام إلى بريدك الإلكتروني", "A 6-digit verification code has been sent to your email"),
            ["TargetEmail"] = ("البريد الإلكتروني المستهدف:", "Target Email:"),
            ["EnterSixDigitOtp"] = ("أدخل رمز الـ 6 أرقام", "Enter 6-Digit Code"),
            ["CodeValidity"] = ("صلاحية الرمز:", "Code Validity:"),
            ["MaxAttemptsNotice"] = ("الحد الأقصى: 5 محاولات", "Maximum: 5 Attempts"),
            ["ConfirmOtpAndProceed"] = ("تأكيد الرمز والمتابعة", "Verify Code & Proceed"),
            ["ResendNewOtp"] = ("إعادة إرسال رمز جديد", "Resend New Code"),
            ["CodeExpired"] = ("انتهت الصلاحية", "Expired"),
            ["AvailableNow"] = ("متاح الآن", "Available Now"),
            ["CodeExpiredRequestNew"] = ("انتهت صلاحية الرمز - يرجى طلب رمز جديد", "Code has expired - please request a new code"),
            ["PackagingUnits"] = ("وحدات التعبئة والتجزئة", "Packaging Units"),
            ["BaseUnitStock"] = ("الوحدة الأساسية (المخزون)", "Base Unit (Stock)"),
            ["BaseUnitDescription"] = ("الوحدة التي يُقاس بها الرصيد المخزني الفعلي بالصيدلية.", "The unit by which the actual physical pharmacy inventory is measured."),
            ["ConversionFactorToDefault"] = ("معامل التحويل للأساس", "Conversion Factor to Base"),
            ["UnitBarcodeOptional"] = ("باركود الوحدة (اختياري)", "Unit Barcode (Optional)"),
            ["AddPackagingUnit"] = ("+ إضافة وحدة بيع (شريط / علبة / كرتونة)", "+ Add Selling Unit (Strip / Box / Carton)"),
            ["BaseUnitStockType"] = ("أساسية (مخزنية)", "Base (Stock)"),
            ["RetailPackagingType"] = ("تجزئة / تعبئة", "Retail / Packaging"),
            ["UnitType"] = ("نوع الوحدة", "Unit Type"),
            ["ExampleBoxStrip"] = ("مثال: Box أو Strip", "e.g. Box or Strip"),
            ["UnitCustomBarcode"] = ("باركود خاص بالوحدة", "Custom unit barcode"),
            ["ContainsPackagingHierarchy"] = ("التسلسل الهرمي (يحتوي على)", "Contains (Hierarchy)"),
            ["PurchaseAllowed"] = ("شراء", "Purchase"),
            ["SaleAllowed"] = ("بيع", "Sale"),
            ["PackagingHierarchyHint"] = ("يتم احتساب معامل التحويل للوحدة الأساسية تلقائياً بناءً على الوحدات الفرعية", "Conversion factor to base unit is auto-calculated based on child units"),
            ["Strip"] = ("شريط / Strip", "Strip"),
            ["Box"] = ("علبة / Box", "Box"),
            ["RegistrationPendingTitle"] = ("طلب التسجيل قيد المراجعة", "Registration Pending Approval"),
            ["RegistrationSubmittedTitle"] = ("تم إرسال طلب التسجيل بنجاح", "Registration Submitted Successfully"),
            ["RegistrationPendingApprovalSubtitle"] = ("صيدليتك قيد الاعتماد من إدارة المنصة", "Your pharmacy registration is pending platform admin approval"),
            ["ReviewInProgress"] = ("الطلب قيد التدقيق والمراجعة", "Review In Progress"),
            ["RegistrationPendingExplanation"] = ("تم تسجيل بيانات الصيدلية والحساب الإداري. لحماية أمان المنصة، يتطلب تشغيل الحساب مراجعة واعتماد المطور / إدارة النظام.", "Your pharmacy information and initial administrator account have been registered. To maintain security, activation requires developer review and approval."),
            ["RegistrationPendingNextSteps"] = ("ستتمكن من تسجيل الدخول فور اعتماد الحساب وتفعيله. شكراً لثقتكم بنظامنا.", "You will be able to log in once your account has been reviewed and activated. Thank you for your patience."),
            ["BackToLogin"] = ("العودة إلى شاشة الدخول", "Back to Login"),
            ["DeveloperAdminDashboard"] = ("لوحة إدارة المطور واعتماد الصيدليات", "Developer Admin & Pharmacy Approvals"),
            ["DeveloperAdminSubtitle"] = ("إدارة طلبات تسجيل الصيدليات والتحكم في تفعيل وإيقاف المنشآت", "Manage pharmacy registration requests and control activation"),
            ["Developer"] = ("إدارة المطور", "Developer"),
            ["PharmacyManagementAndApprovals"] = ("إدارة واعتماد الصيدليات", "Pharmacy Approvals"),
            ["PendingApprovals"] = ("طلبات قيد المراجعة", "Pending Approvals"),
            ["ActivePharmacies"] = ("الصيدليات المفعلة", "Active Pharmacies"),
            ["SuspendedPharmacies"] = ("الصيدليات الموقوفة", "Suspended Pharmacies"),
            ["RejectedPharmacies"] = ("الطلبات المرفوضة", "Rejected Requests"),
            ["PendingApproval"] = ("قيد الاعتماد", "Pending Approval"),
            ["SearchPharmaciesPlaceholder"] = ("بحث باسم الصيدلية أو الهاتف أو المسؤول...", "Search by pharmacy name, phone or admin..."),
            ["ResponsiblePerson"] = ("الشخص المسؤول", "Responsible Person"),
            ["ContactDetails"] = ("بيانات التواصل", "Contact Details"),
            ["RegistrationDate"] = ("تاريخ التسجيل", "Registration Date"),
            ["NoPharmaciesFound"] = ("لا توجد صيدليات مطابقة للبحث", "No pharmacies found matching criteria"),
            ["PharmacyApprovedSuccessfully"] = ("تم اعتماد الصيدلية وتفعيل حساب المسؤول بنجاح", "Pharmacy approved and administrator account activated successfully"),
            ["PharmacyRejectedSuccessfully"] = ("تم رفض طلب تسجيل الصيدلية", "Pharmacy registration request rejected"),
            ["PharmacySuspendedSuccessfully"] = ("تم إيقاف تفعيل الصيدلية بنجاح", "Pharmacy suspended successfully"),
            ["PharmacyActivatedSuccessfully"] = ("تمت إعادة تفعيل الصيدلية بنجاح", "Pharmacy reactivated successfully"),
            ["StockQuantityCannotBeNegative"] = ("كمية المخزون لا يمكن أن تكون سالبة", "Stock quantity cannot be negative"),
            ["StockAdjustmentReasonRequired"] = ("سبب تسوية المخزون مطلوب", "Stock adjustment reason is required"),
            ["StockQuantityUnchanged"] = ("الكمية المدخلة مطابقة للكمية الحالية دون تغيير", "Stock quantity remains unchanged"),
            ["StockAdjustedSuccessfully"] = ("تمت تسوية وتحديث كمية المخزون بنجاح", "Stock adjusted successfully"),
            ["ErrorOccurredDuringStockAdjustment"] = ("حدث خطأ أثناء تسوية المخزون", "An error occurred during stock adjustment"),
            ["PharmacyPendingApproval"] = ("حساب الصيدلية قيد المراجعة والاعتماد من مطور النظام. لا يمكن الدخول حالياً.", "Your pharmacy account is pending developer approval. Login is currently unavailable."),
            ["PharmacyRegistrationRejected"] = ("تم رفض طلب تسجيل هذه الصيدلية. يرجى مراجعة إدارة المنصة.", "Pharmacy registration request was rejected. Please contact support."),
            ["PharmacySuspended"] = ("تم إيقاف نشاط هذه الصيدلية مؤقتاً. يرجى التواصل مع إدارة النظام.", "This pharmacy account has been suspended. Please contact administrator."),
            ["NoPharmacyAssigned"] = ("لا توجد صيدلية مرتبطة بهذا الحساب", "No pharmacy associated with this account"),
            ["SubmitRegistrationRequest"] = ("تقديم طلب تسجيل الصيدلية", "Submit Registration Request"),
            ["PharmacyAddressPlaceholder"] = ("مثال: شارع الجمهورية، القاهرة", "e.g. 15 El-Gomhoureya St, Cairo"),
            ["ProductClassification"] = ("التصنيف والشكل الصيدلاني", "Classification & Pharmaceutical Form"),
            ["DosageForm"] = ("الشكل الصيدلاني", "Dosage Form"),
            ["DosageForm_Tablets"] = ("أقراص", "Tablets"),
            ["DosageForm_Capsules"] = ("كبسولات", "Capsules"),
            ["DosageForm_Syrup"] = ("شراب", "Syrup"),
            ["DosageForm_Injection"] = ("حقن", "Injection"),
            ["DosageForm_Ampoule"] = ("أمبول", "Ampoule"),
            ["DosageForm_Vial"] = ("فيال", "Vial"),
            ["DosageForm_Cream"] = ("كريم", "Cream"),
            ["DosageForm_Ointment"] = ("مرهم", "Ointment"),
            ["DosageForm_Drops"] = ("قطرات", "Drops"),
            ["DosageForm_Suppository"] = ("لبوس", "Suppository"),
            ["DosageForm_Sachet"] = ("أكياس وفوار", "Sachets"),
            ["DosageForm_Spray"] = ("بخاخ", "Spray"),
            ["DosageForm_MedicalSupplies"] = ("مستلزمات طبية", "Medical Supplies"),
            ["DosageForm_Custom"] = ("شكل مخصص", "Custom Form"),
            ["QuickAddCategory"] = ("+ فئة جديدة", "+ New Category"),
            ["CategoryQuickSuggestions"] = ("فئات شائعة ومقترحة", "Predefined Category Suggestions"),
            ["Category_Analgesics"] = ("مسكنات الآلام", "Analgesics (Painkillers)"),
            ["Category_Antibiotics"] = ("مضادات حيوية", "Antibiotics"),
            ["Category_Vitamins"] = ("فيتامينات ومكملات غذائية", "Vitamins & Supplements"),
            ["Category_Digestive"] = ("أدوية الجهاز الهضمي", "Digestive System"),
            ["Category_Cardiovascular"] = ("أدوية القلب والأوعية الدموية", "Cardiovascular"),
            ["Category_Allergy"] = ("أدوية الحساسية", "Allergy"),
            ["Category_Respiratory"] = ("أدوية الجهاز التنفسي", "Respiratory"),
            ["Category_MedicalSupplies"] = ("مستلزمات طبية", "Medical Supplies"),
            ["Category_Other"] = ("أخرى", "Other"),
            ["SellFullPackageOnly"] = ("البيع كعبوة كاملة فقط", "Sell Full Package Only"),
            ["SellFullPackageOnlyHint"] = ("عند التفعيل، يتم إخفاء الوحدات الجزئية (كالشريط أو القرص) من نقطة البيع", "When enabled, partial units (e.g. strips or tablets) are hidden from POS"),
            ["PackagingHierarchyPreview"] = ("المعاينة المباشرة للتسلسل الهرمي", "Live Packaging Preview"),
            ["PackagingPresets"] = ("قوالب تعبئة جاهزة", "Quick Packaging Presets"),
            ["BaseStockUnitNotice"] = ("الوحدة الأساسية هي أصغر وحدة يتم قياس رصيد المخزون الفعلي بها", "The base unit is the smallest unit by which physical stock is tracked"),
            ["CustomerReceipt"] = ("إيصال العميل", "Customer Receipt"),
            ["PrintCustomerReceipt"] = ("طباعة إيصال العميل", "Print Customer Receipt"),
            ["SalesReceipt"] = ("إيصال استلام مبيعات", "Sales Receipt"),
            ["ThermalReceipt"] = ("إيصال حراري (80 مم)", "Thermal Receipt (80mm)"),
            ["StandardReceipt"] = ("طباعة عادية (A4/A5)", "Standard Print (A4/A5)"),
            ["ThankYouVisiting"] = ("شكراً لزيارتكم ونتمنى لكم دوام الصحة والعافية!", "Thank you for visiting us. Wishing you a speedy recovery!"),
            ["SellingUnit"] = ("وحدة البيع", "Selling Unit"),
            ["CanBeSoldInSmallerUnits"] = ("هل يمكن بيع المنتج بوحدات أصغر؟", "Can this product be sold in smaller units?"),
            ["CanBeSoldInSmallerUnitsHint"] = ("حدد نعم إذا كان الدواء يباع بالشريط أو القرص أو الوحدة، أو لا إذا كان يباع فقط بالعبوة الكاملة", "Select Yes if the medicine is sold by strip or tablet, or No if sold only as a full package"),
            ["SoldAsFullPackageOnlyTitle"] = ("يباع هذا الصنف بالعبوة الكاملة فقط", "This product is sold as a full package only"),
            ["SoldAsFullPackageOnlyDescription"] = ("يتم بيع هذا الصنف وشراؤه كعبوة متكاملة، ولن تظهر وحدات تجزئة فرعية في شاشة الكاشير.", "This item is sold and purchased as a complete package. No smaller units will be available at POS."),
            ["MainPackage"] = ("العبوة الرئيسية (الكبرى)", "Main Package"),
            ["MainPackageName"] = ("اسم العبوة الرئيسية", "Main Package Name"),
            ["MainPackagePrice"] = ("سعر بيع العبوة", "Main Package Price"),
            ["StripsPerBox"] = ("عدد الشرائط داخل العلبة", "Strips per Box"),
            ["TabletsPerStrip"] = ("عدد الأقراص داخل الشريط", "Tablets per Strip"),
            ["CapsulesPerStrip"] = ("عدد الكبسولات داخل الشريط", "Capsules per Strip"),
            ["AmpoulesPerBox"] = ("عدد الأمبولات داخل العلبة", "Ampoules per Box"),
            ["VialsPerBox"] = ("عدد الفيال داخل العلبة", "Vials per Box"),
            ["SachetsPerBox"] = ("عدد الأكياس داخل العلبة", "Sachets per Box"),
            ["PiecesPerPack"] = ("عدد القطع داخل العبوة", "Pieces per Pack"),
            ["AllowSellingStrip"] = ("إتاحة بيع الشريط منفرداً في الكاشير", "Allow selling strips individually at POS"),
            ["AllowSellingTablet"] = ("إتاحة بيع القرص منفرداً في الكاشير", "Allow selling tablets individually at POS"),
            ["AllowSellingCapsule"] = ("إتاحة بيع الكبسولة منفرداً في الكاشير", "Allow selling capsules individually at POS"),
            ["AllowSellingAmpoule"] = ("إتاحة بيع الأمبول منفرداً في الكاشير", "Allow selling ampoules individually at POS"),
            ["AllowSellingVial"] = ("إتاحة بيع الفيال منفرداً في الكاشير", "Allow selling vials individually at POS"),
            ["AllowSellingSachet"] = ("إتاحة بيع الكيس منفرداً في الكاشير", "Allow selling sachets individually at POS"),
            ["AllowSellingPiece"] = ("إتاحة بيع القطعة منفردة في الكاشير", "Allow selling pieces individually at POS"),
            ["SuggestedPrice"] = ("سعر مقترح", "Suggested Price"),
            ["FormulaPreview"] = ("معادلة التجزئة والتعبئة المعتمدة", "Packaging & Splitting Formula"),
            ["AddCustomSmallerUnit"] = ("+ إضافة وحدة تجزئة مخصصة", "+ Add Custom Smaller Unit"),
            ["SmallerUnitName"] = ("اسم الوحدة الأصغر (للتجزئة)", "Smaller Unit Name"),
            ["ContainsCount"] = ("العدد داخل العبوة", "Count in Package"),
            ["EGP"] = ("ج.م", "EGP")
        };

        var testDir = AppContext.BaseDirectory;
        // Search upwards for Resources/SharedResource.ar.resx
        var current = new DirectoryInfo(testDir);
        while (current != null && !File.Exists(Path.Combine(current.FullName, "Resources", "SharedResource.ar.resx")))
        {
            current = current.Parent;
        }

        var rootDir = current?.FullName ?? Directory.GetCurrentDirectory();
        var arPath = Path.Combine(rootDir, "Resources", "SharedResource.ar.resx");
        var enPath = Path.Combine(rootDir, "Resources", "SharedResource.en.resx");
        var defPath = Path.Combine(rootDir, "Resources", "SharedResource.resx");

        SyncKeys(arPath, requiredKeys, true);
        SyncKeys(enPath, requiredKeys, false);
        SyncKeys(defPath, requiredKeys, false);

        Assert.True(File.Exists(arPath));
        Assert.True(File.Exists(enPath));
        Assert.True(File.Exists(defPath));
    }

    private static void SyncKeys(string path, Dictionary<string, (string ar, string en)> keys, bool isAr)
    {
        if (!File.Exists(path)) return;

        var doc = new XmlDocument { PreserveWhitespace = true };
        doc.Load(path);
        var root = doc.DocumentElement!;
        bool modified = false;

        foreach (var (k, v) in keys)
        {
            var existing = doc.SelectSingleNode($"//data[@name='{k}']");
            if (existing == null)
            {
                var dataElem = doc.CreateElement("data");
                dataElem.SetAttribute("name", k);
                dataElem.SetAttribute("xml:space", "preserve");

                var valElem = doc.CreateElement("value");
                valElem.InnerText = isAr ? v.ar : v.en;

                dataElem.AppendChild(valElem);
                root.AppendChild(doc.CreateWhitespace("  "));
                root.AppendChild(dataElem);
                root.AppendChild(doc.CreateWhitespace("\n"));
                modified = true;
            }
        }

        if (modified)
        {
            doc.Save(path);
        }
    }
}
