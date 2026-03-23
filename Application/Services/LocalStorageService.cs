using eArchiveSystem.Application.Interfaces.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace eArchiveSystem.Application.Services
{
    public class LocalStorageService : IStorageService
    {
        private readonly IWebHostEnvironment _env;

        public LocalStorageService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string folderName)
        {
            // نسحب مسار المشروع الحقيقي
            var root = Directory.GetCurrentDirectory();

            // نحدد المسار الكامل للمجلد المطلوب
            var uploadsPath = Path.Combine(root, folderName);

            // إذا ما كان المجلد موجود → نعمله
            if (!Directory.Exists(uploadsPath))
                Directory.CreateDirectory(uploadsPath);

            // اسم فريد للملف
            string uniqueFileName = Guid.NewGuid() + Path.GetExtension(file.FileName);

            // المسار الكامل للملف
            string filePath = Path.Combine(uploadsPath, uniqueFileName);

            // نسخ الملف
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fs);
            }

            // نرجع المسار النسبي (يعتمد على الحاجة)
            return Path.Combine(folderName, uniqueFileName);
        }

    }
}
