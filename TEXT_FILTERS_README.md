# Text Filters Feature

## Mô tả
Text Filters là một tính năng mới được thêm vào RevitScheduleEditor, cho phép lọc dữ liệu trong Schedule một cách nâng cao, tương tự như Excel.

## Chức năng

### TextFiltersWindow
- **Giao diện tương tự Excel**: Dialog có giao diện giống như Text Filters trong Excel
- **Search Box**: Cho phép tìm kiếm nhanh trong danh sách các giá trị
- **Select All**: Checkbox để chọn/bỏ chọn tất cả items
- **Danh sách values**: Hiển thị tất cả giá trị unique trong cột với checkbox
- **OK/Cancel**: Buttons để áp dụng hoặc hủy filter

### Tính năng chính
1. **Filter theo cột**: Click vào filter button trên header của bất kỳ cột nào
2. **Multi-select**: Có thể chọn nhiều giá trị để hiển thị
3. **Search**: Tìm kiếm nhanh trong danh sách values
4. **Visual indicator**: Header sẽ được highlight khi có filter active
5. **Persistent filters**: Có thể áp dụng filter cho nhiều cột cùng lúc

## Cách sử dụng

1. **Mở Schedule Editor** trong Revit
2. **Load data**: Click "Preview/Edit" để load schedule data
3. **Click vào filter button** (biểu tượng ▼) trên header của cột muốn filter
4. **Chọn các giá trị** muốn hiển thị:
   - 💡 **Quan trọng**: UNCHECK items để hide chúng. Chỉ checked items sẽ visible
   - Dùng Search box để tìm nhanh
   - Dùng "Select All" để chọn/bỏ chọn tất cả
   - Status text cho biết số items được chọn và effect của filter
5. **Click OK** để áp dụng filter
6. **Repeat** cho các cột khác nếu cần

### 🎯 Tip sử dụng:
- **All items checked** = Không filter (hiển thị tất cả)
- **Some items unchecked** = Filter active (hide unchecked items)
- **No items checked** = Hide tất cả (empty results)

### 🧪 Test Buttons:
- **"Test Filter"** (tím): Test dialog với sample data có pre-selection
- **"Test Real"** (hồng): Test filter trên actual data
- **"Demo"** (cam): Demo filter với 30% items pre-selected

## Ví dụ sử dụng
- Filter các Element ID cụ thể: `420.05.010d`, `420.05.013a`, `420.05.111`
- Lọc theo loại material, family type, parameter values, etc.
- Kết hợp nhiều filters để tìm chính xác những elements cần thiết

## Code Structure

### Files được thêm mới:
- `TextFiltersWindow.xaml` - XAML layout cho filter dialog
- `TextFiltersWindow.xaml.cs` - Code-behind logic cho filter functionality

### Files được cập nhật:
- `RevitScheduleEditor.csproj` - Thêm references cho XAML files mới
- `ScheduleEditorWindow.xaml.cs` - Cập nhật logic để sử dụng TextFiltersWindow

### Classes mới:
- `FilterItem` - Represents mỗi item trong filter list
- `TextFiltersWindow` - Main dialog window cho text filtering

## Technical Details

### FilterItem Class
```csharp
public class FilterItem : INotifyPropertyChanged
{
    public string Value { get; set; }    // Giá trị hiển thị
    public bool IsSelected { get; set; }  // Trạng thái được chọn
}
```

### Key Methods
- `FilterItems()` - Lọc items theo search text
- `UpdateSelectAllCheckbox()` - Cập nhật trạng thái Select All checkbox
- `ApplyFilters()` - Áp dụng tất cả filters lên DataGrid
- `UpdateColumnHeaderFilterStatus()` - Cập nhật visual state của column header

## Build và Deploy

Sau khi build thành công, file `RevitScheduleEditor_YYYYMMDD_HHMMSS.dll` sẽ được tạo trong folder `bin\Release\`.

Copy file này vào thư mục Revit Add-ins để sử dụng.

## Debug & Troubleshooting (Added 09/09/2025)

### 🔧 Debug Tools Added:

1. **Test Filter Button** (màu tím):
   - Test TextFiltersWindow với sample data
   - Verify filter dialog hoạt động
   - Independent của actual schedule data

2. **Test Real Button** (màu hồng):
   - Test filter trên data thực đã load
   - Tự động chọn column đầu tiên
   - Debug actual filter workflow

3. **Enhanced Debug Logging**:
   - Chi tiết logging trong FilterButton_Click
   - Track ApplyFilters() execution step by step
   - Count original vs filtered data
   - Exception handling với stack trace

### 🐛 Troubleshooting Steps:

**Nếu filter không hoạt động:**

1. **Load data first**: Click "Preview/Edit" để load schedule data
2. **Test dialog**: Click "Test Filter" để verify dialog works
3. **Test real filter**: Click "Test Real" để test trên data thực
4. **Check debug output**: Xem logs trong Revit debug console

**Debug Log Examples:**
```
[ScheduleEditorWindow] ApplyFilters - Started, active filters: 1
[ScheduleEditorWindow] ApplyFilters - Original data count: 236
[ScheduleEditorWindow] ApplyFilters - Applying filter for column 'CRR_UQID_ASSET' with 50 allowed values
[ScheduleEditorWindow] ApplyFilters - Filtered data count: 125
[ScheduleEditorWindow] ApplyFilters - DataGrid ItemsSource updated successfully
```

**Common Issues:**
- **"All items selected"** → Filter removed, all data shown
- **No debug logs** → Method not called, check button clicks
- **Exception in ApplyFilters** → Data binding issue, check ScheduleData

### 🔍 Current Status (Based on User Log):
- ✅ Dialog hiển thị và hoạt động 
- ✅ Filter button clicks được detect
- ✅ Values được load correctly (225 unique values)
- ❌ ApplyFilters() chưa được gọi → **Cần debug tiếp**

