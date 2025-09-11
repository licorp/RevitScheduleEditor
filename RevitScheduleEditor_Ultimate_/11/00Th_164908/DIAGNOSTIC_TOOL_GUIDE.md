# REVIT ADD-IN DIAGNOSTIC TOOL
## Công cụ chẩn đoán lỗi Revit Add-in

### Mục đích
Tool này giúp bạn:
- ✅ Kiểm tra môi trường .NET Framework
- ✅ Kiểm tra các phiên bản Revit đã cài đặt  
- ✅ Phân tích file .addin và DLL
- ✅ Tìm lỗi dependencies và cấu hình
- ✅ Tạo report chi tiết để debug

### Cách sử dụng

#### 1. Chạy tool
Double-click vào `RevitAddinDiagnostic.exe`

#### 2. Kiểm tra môi trường (Tự động chạy khi mở)
- Click **"Kiểm tra Môi trường"**
- Xem thông tin .NET Framework, Windows version
- Kiểm tra assemblies đã load

#### 3. Kiểm tra Revit
- Click **"Kiểm tra Revit"**  
- Xem các phiên bản Revit đã cài đặt
- Kiểm tra thư mục Add-ins và các file .addin có sẵn

#### 4. Kiểm tra Add-in của bạn
- Click **"Kiểm tra Add-in"**
- Chọn file `.addin` hoặc `.dll` cần kiểm tra
- Tool sẽ phân tích:
  * ✅ Cú pháp XML của file .addin
  * ✅ Đường dẫn assembly có đúng không
  * ✅ DLL có load được không
  * ✅ Dependencies có đầy đủ không
  * ✅ Class IExternalCommand có tồn tại không

#### 5. Lưu report
- Click **"Lưu Report"** để lưu kết quả ra file text
- Gửi file này khi cần hỗ trợ

### Các lỗi thường gặp và cách fix

#### ❌ "Assembly không tồn tại"
**Nguyên nhân:** Đường dẫn trong file .addin sai
**Cách fix:**
- Kiểm tra đường dẫn trong file .addin
- Đảm bảo file DLL ở đúng vị trí
- Dùng đường dẫn tương đối thay vì tuyệt đối

#### ❌ "Could not load file or assembly"  
**Nguyên nhân:** Thiếu dependencies
**Cách fix:**
- Copy tất cả DLL dependencies vào cùng thư mục
- Kiểm tra version .NET Framework
- Đảm bảo architecture (x86/x64) phù hợp

#### ❌ ".NET Framework version không đủ"
**Nguyên nhân:** Máy thiếu .NET Framework 4.8
**Cách fix:**
- Download và cài đặt .NET Framework 4.8
- Link: https://dotnet.microsoft.com/download/dotnet-framework/net48

#### ❌ "IExternalCommand not found"
**Nguyên nhân:** Class trong DLL không implement đúng interface
**Cách fix:**
- Kiểm tra code implement IExternalCommand
- Đảm bảo namespace và class name đúng với file .addin

#### ❌ "RevitAPI.dll not found"
**Nguyên nhân:** Reference sai version Revit API
**Cách fix:**
- Kiểm tra project reference đến RevitAPI.dll
- Đảm bảo "Copy Local = False" trong project settings

### Output mẫu

```
=== KIỂM TRA ADD-IN: RevitScheduleEditor_External.addin ===

--- Kiểm tra file .addin ---
✅ File đọc được
✅ XML hợp lệ
Số lượng AddIn: 1

AddIn: Schedule Editor
Assembly: RevitScheduleEditor.dll
Class: RevitScheduleEditor.ShowScheduleEditorCommand
📍 Đường dẫn tương đối
✅ Assembly tồn tại: C:\RevitTools\ScheduleEditor\RevitScheduleEditor.dll

--- Kiểm tra DLL: RevitScheduleEditor.dll ---
Kích thước: 141,312 bytes
✅ Assembly load thành công
Target Framework: v4.0.30319

--- Dependencies ---
📦 RevitAPI, Version=20.0.0.0, Culture=neutral, PublicKeyToken=null
📦 RevitAPIUI, Version=20.0.0.0, Culture=neutral, PublicKeyToken=null

--- IExternalCommand Classes ---
🎯 RevitScheduleEditor.ShowScheduleEditorCommand implements IExternalCommand
```

### Khi nào sử dụng tool này?

1. **Trước khi deploy:** Kiểm tra add-in trên máy development
2. **Khi có lỗi:** Chạy tool để tìm nguyên nhân
3. **Máy mới:** Kiểm tra môi trường trước khi cài add-in
4. **Debug:** Tạo report gửi cho developer

### Lưu ý quan trọng

⚠️ **Chạy với quyền Administrator** nếu cần kiểm tra registry
⚠️ **Đóng Revit** trước khi chạy tool để tránh lock file
⚠️ **Backup** add-in trước khi sửa lỗi
⚠️ Tool **không sửa lỗi tự động**, chỉ chẩn đoán và báo cáo

---
*Phát triển bởi LICORP - Tool hỗ trợ debug Revit Add-in*
