# RevitScheduleEditor DLL Build Log

**Build Date:** 2025-08-28
**Build Time:** 08:43:00 (UTC+7)
**Output Directory:** bin/Release/net8.0-windows/

**DLL Files Generated:**
- RevitScheduleEditor.dll
- RevitScheduleEditor_20250828_084300.dll (nếu có script rename)

**Build Status:** Thành công
**Warning:** 11 cảnh báo nullable, không ảnh hưởng chức năng.

**Hướng dẫn sử dụng DLL:**
- Copy file DLL vào thư mục Addins của Revit 2025.
- Đảm bảo file .addin trỏ đúng tới file DLL mới.
- Nếu cần xuất file với tên kèm ngày giờ, dùng script rename hoặc copy file sau khi build:

```powershell
$dt = Get-Date -Format "yyyyMMdd_HHmmss"
Copy-Item "bin\Release\net8.0-windows\RevitScheduleEditor.dll" "bin\Release\net8.0-windows\RevitScheduleEditor_$dt.dll"
```

**Lưu ý:**
- Nếu cần xuất file DLL với ngày giờ tự động mỗi lần build, hãy thêm script vào post-build event hoặc chạy lệnh PowerShell như trên.
