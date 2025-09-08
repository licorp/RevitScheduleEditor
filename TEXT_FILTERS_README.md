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
2. **Click vào filter button** (biểu tượng funnel) trên header của cột muốn filter
3. **Chọn các giá trị** muốn hiển thị:
   - Dùng Search box để tìm nhanh
   - Click checkbox để chọn/bỏ chọn từng item
   - Dùng "Select All" để chọn/bỏ chọn tất cả
4. **Click OK** để áp dụng filter
5. **Repeat** cho các cột khác nếu cần

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

## Future Enhancements

- **Advanced filtering**: Thêm các operators như "contains", "starts with", "ends with"
- **Custom filters**: Cho phép user tạo custom filter expressions
- **Save filter presets**: Lưu và load các filter configurations
- **Export filtered data**: Export chỉ data đã được filter
