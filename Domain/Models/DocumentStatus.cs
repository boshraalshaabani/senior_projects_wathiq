namespace eArchiveSystem.Domain.Models
{
    public enum DocumentStatus
    {
        Draft,        // مسودة - يمكن التعديل من المالك
        Processing,   // تم رفعها وتنتظر انتهاء الـ OCR / المعالجة
        Submitted,    // مقدمة للمراجعة
        UnderReview,  // تحت المراجعة - بدأت المراجعة من Manager
        Approved,     // موافق عليها - جاهزة للنشر
        Rejected,     // مرفوضة - يمكن إعادة التعديل
        Published,    // منشورة - عامة للعرض
        Archived      // مؤرشفة - محفوظة للأرشفة التاريخية
    }
}