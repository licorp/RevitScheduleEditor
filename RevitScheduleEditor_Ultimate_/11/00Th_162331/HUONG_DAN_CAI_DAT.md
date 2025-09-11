# HƯỚNG DẪN CÀI ĐẶT REVIT SCHEDULE EDITOR

## Tổng quan
RevitScheduleEditor là một add-in cho Revit giúp chỉnh sửa schedule một cách nâng cao và tiện lợi.

## Yêu cầu hệ thống
- Revit 2020, 2021, 2022, 2023, 2024, hoặc 2025
- .NET Framework 4.8
- Windows 10/11

## Các phương pháp cài đặt

### PHƯƠNG PHÁP 1: External Tool (Khuyến nghị cho việc test)
Phương pháp này cho phép chạy add-in mà không cần copy vào thư mục Addins của Revit.

**Bước 1:** Copy toàn bộ thư mục này đến một vị trí cố định, ví dụ:
```
C:\RevitTools\ScheduleEditor\
```

**Bước 2:** Mở Revit và vào menu:
```
External Tools > Configure External Tools...
```

**Bước 3:** Click "Add" để thêm tool mới và điền thông tin:
- **Title:** Schedule Editor
- **Command:** C:\RevitTools\ScheduleEditor\RevitScheduleEditor_External.addin
- **Initial Directory:** C:\RevitTools\ScheduleEditor\
- **Prompt for Arguments:** (để trống)

**Bước 4:** Click OK để lưu cấu hình.

**Bước 5:** Tool sẽ xuất hiện trong menu External Tools, click để sử dụng.

### PHƯƠNG PHÁP 2: Add-in thường (Khuyến nghị cho sử dụng hàng ngày)

#### Cài đặt tự động (Dễ nhất)
1. Chạy file `InstallAllVersions.bat` với quyền Administrator
2. Script sẽ tự động cài đặt cho tất cả các phiên bản Revit có trên máy
3. Khởi động lại Revit để thấy add-in

#### Cài đặt thủ công
1. Copy file `RevitScheduleEditor_External.addin` vào thư mục tương ứng:
   
   **Revit 2020:**
   ```
   %APPDATA%\Autodesk\Revit\Addins\2020\
   ```
   
   **Revit 2021:**
   ```
   %APPDATA%\Autodesk\Revit\Addins\2021\
   ```
   
   **Revit 2022:**
   ```
   %APPDATA%\Autodesk\Revit\Addins\2022\
   ```
   
   **Revit 2023:**
   ```
   %APPDATA%\Autodesk\Revit\Addins\2023\
   ```
   
   **Revit 2024:**
   ```
   %APPDATA%\Autodesk\Revit\Addins\2024\
   ```
   
   **Revit 2025:**
   ```
   %APPDATA%\Autodesk\Revit\Addins\2025\
   ```

2. Copy các file DLL vào cùng thư mục:
   - `RevitScheduleEditor.dll`
   - `RevitScheduleEditor.pdb` (optional)

3. Khởi động lại Revit

## Cách sử dụng

1. Mở file Revit project có chứa schedule
2. Vào menu Add-Ins > External Tools hoặc tìm "Schedule Editor" trong ribbon
3. Click để mở Schedule Editor window
4. Chọn schedule cần chỉnh sửa từ danh sách
5. Thực hiện các thao tác chỉnh sửa

## Tính năng chính

- **Chỉnh sửa nhanh:** Chỉnh sửa trực tiếp dữ liệu trong schedule
- **Lọc dữ liệu:** Lọc các row theo điều kiện
- **Export Excel:** Xuất dữ liệu ra Excel
- **Undo/Redo:** Hoàn tác các thay đổi
- **Bulk edit:** Chỉnh sửa hàng loạt

## Gỡ cài đặt

### Để gỡ External Tool:
1. Vào External Tools > Configure External Tools
2. Chọn "Schedule Editor" và click Remove

### Để gỡ Add-in:
1. Xóa file `.addin` trong thư mục Addins tương ứng
2. Xóa các file DLL trong cùng thư mục đó

## Khắc phục sự cố

### Add-in không xuất hiện trong menu:
1. Kiểm tra đường dẫn trong file .addin có đúng không
2. Đảm bảo file DLL ở cùng thư mục với file .addin
3. Kiểm tra Revit có quyền đọc các file không
4. Khởi động lại Revit

### Lỗi khi chạy add-in:
1. Kiểm tra file log của Revit trong:
   ```
   %APPDATA%\Autodesk\Revit\<version>\Journals\
   ```
2. Đảm bảo có .NET Framework 4.8
3. Chạy Revit với quyền Administrator

### Lỗi "Could not load file or assembly":
1. Đảm bảo tất cả file DLL ở cùng thư mục
2. Kiểm tra version .NET Framework
3. Copy lại các file dependency

## Liên hệ hỗ trợ

- **Tác giả:** LICORP
- **Email:** [Thêm email hỗ trợ]
- **Website:** [Thêm website]

## Phiên bản
- **Version:** 1.0
- **Build Date:** 11/09/2025
- **Compatibility:** Revit 2020-2025

---
*Cảm ơn bạn đã sử dụng RevitScheduleEditor!*
