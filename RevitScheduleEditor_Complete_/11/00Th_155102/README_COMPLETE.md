# 🎯 REVIT SCHEDULE EDITOR - COMPLETE DIAGNOSTIC PACKAGE

## 📋 Tổng quan
Package hoàn chỉnh bao gồm:
- ✅ **RevitScheduleEditor Add-in** - Tool chỉnh sửa schedule chính
- ✅ **Diagnostic Tool** - Công cụ chẩn đoán và debug lỗi  
- ✅ **Installation Scripts** - Scripts cài đặt tự động
- ✅ **Documentation** - Hướng dẫn chi tiết

## 🚀 Quick Start Guide

### Bước 1: Kiểm tra môi trường (QUAN TRỌNG!)
```
RevitAddinDiagnostic.exe
```
- Click **"Kiểm tra Môi trường"** - xem .NET Framework, Windows info
- Click **"Kiểm tra Revit"** - xem các version Revit đã cài

### Bước 2: Cài đặt Add-in
**Cách 1: Tự động (Khuyến nghị)**
```
InstallAllVersions.bat (chạy with Administrator)
```

**Cách 2: External Tool (Cho testing)**  
```
Install.bat
```

### Bước 3: Nếu có lỗi 🔧
1. Chạy `RevitAddinDiagnostic.exe`
2. Click **"Kiểm tra Add-in"**  
3. Chọn file `.addin` để phân tích chi tiết
4. Click **"Lưu Report"** và gửi cho support nếu cần

## 📁 Files trong package

| File | Mô tả |
|------|-------|
| `RevitScheduleEditor.dll` | Main add-in assembly |
| `RevitScheduleEditor_External.addin` | Add-in manifest (relative paths) |
| `RevitScheduleEditor.pdb` | Debug symbols |
| `RevitAddinDiagnostic.exe` | 🔍 **Diagnostic tool chính** |
| `InstallAllVersions.bat` | Cài đặt cho tất cả Revit versions |
| `Install.bat` | Cài đặt như External Tool |
| `DIAGNOSTIC_TOOL_GUIDE.md` | 📖 Hướng dẫn chi tiết diagnostic tool |
| `HUONG_DAN_CAI_DAT.md` | 📖 Hướng dẫn cài đặt chi tiết |

## 🔧 Diagnostic Tool Features

### ✅ Kiểm tra Môi trường
- .NET Framework version
- Windows version & architecture  
- User permissions
- Loaded assemblies

### ✅ Kiểm tra Revit  
- Detected Revit installations
- Add-ins directories
- Existing .addin files

### ✅ Phân tích Add-in
- XML syntax validation
- Assembly path checking  
- DLL loading test
- Dependencies verification
- IExternalCommand detection
- Detailed error reporting

## 🚨 Troubleshooting Common Issues

### ❌ Add-in không xuất hiện trong Revit
1. **Chạy Diagnostic Tool** → "Kiểm tra Add-in"
2. Kiểm tra đường dẫn assembly trong file .addin  
3. Đảm bảo DLL và .addin ở cùng thư mục

### ❌ "Could not load file or assembly" 
1. **Nguyên nhân:** Thiếu dependencies hoặc sai .NET version
2. **Fix:** Diagnostic Tool sẽ liệt kê missing DLLs
3. Copy tất cả dependencies vào cùng thư mục

### ❌ .NET Framework issues
1. **Diagnostic Tool** sẽ hiện version hiện tại
2. Cần .NET Framework 4.8 minimum
3. Download từ: https://dotnet.microsoft.com/download/dotnet-framework/net48

### ❌ Permission denied
1. Chạy với quyền Administrator
2. Kiểm tra antivirus blocking
3. Unblock files nếu cần: Properties → Unblock

## 📊 Sample Diagnostic Output

```
=== KIỂM TRA ADD-IN ===
✅ File XML hợp lệ
✅ Assembly tồn tại: RevitScheduleEditor.dll  
✅ Assembly load thành công
🎯 IExternalCommand found: ShowScheduleEditorCommand
📦 Dependencies: RevitAPI ✅, RevitAPIUI ✅
```

## 🎯 Workflow khi có lỗi

1. **🔍 Chạy Diagnostic** → Xác định vấn đề
2. **📋 Lưu Report** → Document lỗi  
3. **🔧 Fix theo hướng dẫn** → Áp dụng solution
4. **✅ Test lại** → Verify fix
5. **📤 Gửi report** → Nếu cần support

## 💡 Pro Tips

- ⚡ **Luôn chạy Diagnostic Tool trước** khi cài đặt
- 🔄 **Backup** add-ins trước khi sửa
- 🎯 **Dùng External Tool** để test trước khi install permanent  
- 📝 **Lưu reports** để track issues
- 🔧 **Update documentation** này khi tìm ra fixes mới

## 📞 Support

- **Tool tự chẩn đoán:** Chạy RevitAddinDiagnostic.exe
- **Documentation:** Đọc DIAGNOSTIC_TOOL_GUIDE.md  
- **Installation:** Đọc HUONG_DAN_CAI_DAT.md
- **Reports:** Lưu và gửi diagnostic reports

---
*Package được tạo bởi LICORP - Bao gồm Diagnostic Tool để tự debug issues!*
