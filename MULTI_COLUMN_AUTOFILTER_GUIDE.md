# Multi-Column AutoFilter - Hướng Dẫn Sử Dụng

## Tính Năng Mới: Multi-Column AutoFilter với Logic AND

### Mô Tả
Tính năng AutoFilter đã được cải tiến để cho phép lọc nhiều cột đồng thời với logic AND, tương tự như AutoFilter trong Microsoft Excel.

### Cách Thức Hoạt Động
1. **Logic AND Between Columns**: Khi áp dụng filter trên nhiều cột:
   - Cột A: Lọc theo điều kiện A
   - Cột B: Lọc theo điều kiện B  
   - **Kết quả**: Chỉ hiển thị các rows thỏa mãn CẢ điều kiện A VÀ điều kiện B

2. **Filter Process**:
   - Bước 1: Ẩn tất cả rows không thỏa mãn điều kiện cột A
   - Bước 2: Từ kết quả bước 1, tiếp tục ẩn các rows không thỏa mãn điều kiện cột B
   - Bước 3: Hiển thị kết quả cuối cùng

### Tính Năng Mới

#### 1. Filter Status Panel
- **Vị trí**: Hiển thị ngay dưới toolbar, phía trên DataGrid
- **Thông tin hiển thị**:
  - Danh sách các cột đang được lọc
  - Số lượng items được chọn trong mỗi filter
  - Số rows hiện tại / Tổng số rows
- **Ẩn/Hiện**: Tự động ẩn khi không có filter nào active

#### 2. Clear All Filters Button
- **Vị trí**: Trong Filter Status Panel
- **Chức năng**: Xóa tất cả filters đang active
- **Keyboard Shortcut**: `Ctrl+Alt+Delete`
- **Visual**: Nút đỏ với icon rõ ràng

#### 3. Enhanced Filter Logic
- **Cải tiến**: Logic AND tối ưu với performance cao
- **Case-insensitive**: So sánh không phân biệt hoa thường
- **Null/Empty handling**: Xử lý đúng các giá trị trống
- **Whitespace normalization**: Tự động trim khoảng trắng

#### 4. Visual Indicators
- **Active Filter Columns**: Column headers có background màu cam
- **Filter Icons**: 
  - 🔽 - No filter
  - 🔽 (orange) - Active filter
- **Row Count Display**: "Showing X of Y rows"

### Cách Sử Dụng

#### Bước 1: Áp Dụng Filter Trên Cột Đầu Tiên
1. Click vào filter button (🔽) trên column header
2. Chọn các giá trị muốn hiển thị
3. Click OK
4. **Kết quả**: Chỉ hiển thị rows có giá trị được chọn ở cột này

#### Bước 2: Áp Dụng Filter Trên Cột Thứ Hai
1. Click vào filter button trên column header khác
2. **Quan trọng**: Danh sách giá trị sẽ chỉ hiển thị những giá trị có trong các rows đã được filter ở bước 1
3. Chọn các giá trị muốn hiển thị
4. Click OK
5. **Kết quả**: Chỉ hiển thị rows thỏa mãn CẢ hai điều kiện

#### Bước 3: Tiếp Tục Với Các Cột Khác
- Lặp lại quá trình cho các cột khác
- Mỗi filter mới sẽ thu hẹp thêm kết quả

### Ví Dụ Thực Tế

**Dữ liệu mẫu:**
| Element ID | Type         | Level  | Material |
|-----------|-------------|---------|----------|
| 123       | Wall        | Level 1 | Concrete |
| 124       | Wall        | Level 2 | Brick    |
| 125       | Column      | Level 1 | Steel    |
| 126       | Column      | Level 2 | Concrete |
| 127       | Beam        | Level 1 | Steel    |

**Scenario**: Tìm tất cả elements thuộc Level 1 VÀ làm từ Steel

**Bước 1**: Filter cột "Level"
- Chọn: ☑ Level 1
- Bỏ chọn: ☐ Level 2
- **Kết quả tạm**: 3 rows (ID: 123, 125, 127)

**Bước 2**: Filter cột "Material" 
- Danh sách chỉ có: Concrete, Steel (từ 3 rows còn lại)
- Chọn: ☑ Steel
- Bỏ chọn: ☐ Concrete
- **Kết quả cuối**: 2 rows (ID: 125, 127)

### Các Tính Năng Hỗ Trợ

#### 1. Filter Status Display
```
🔽 Active Filters: Level (1 items), Material (1 items)     [Clear All Filters]     Showing 2 of 5 rows
```

#### 2. Keyboard Shortcuts
- **Ctrl+Alt+Delete**: Clear tất cả filters
- **Escape**: Đóng filter popup

#### 3. Performance Optimization
- Sử dụng HashSet cho O(1) lookup
- Lazy evaluation cho large datasets
- Memory efficient filtering

### Lưu Ý Quan Trọng

#### 1. Filter Order Independence
- Thứ tự áp dụng filter không ảnh hưởng đến kết quả cuối
- Filter A → Filter B = Filter B → Filter A

#### 2. Data Consistency  
- Filters được áp dụng trên dữ liệu gốc
- Không bị ảnh hưởng bởi sorting

#### 3. Memory Management
- Filters được lưu trong memory session
- Clear khi reload data hoặc change schedule

#### 4. Excel Compatibility
- Logic hoạt động tương tự Excel AutoFilter
- Export data sẽ bao gồm cả filtered và unfiltered data

### Troubleshooting

#### Vấn Đề: Filter không hoạt động
**Giải pháp**: 
- Kiểm tra data đã được load
- Clear all filters và thử lại
- Restart ứng dụng

#### Vấn Đề: Performance chậm với dataset lớn
**Giải pháp**:
- Sử dụng filter từng bước thay vì nhiều filter cùng lúc
- Clear unused filters

#### Vấn Đề: Filter values không chính xác
**Giải pháp**:
- Refresh data
- Check for null/empty values trong data source

### Technical Implementation

#### Core Components
1. **ScheduleEditorWindow.xaml.cs**
   - `_columnFilters`: Dictionary lưu trữ active filters
   - `ApplyFiltersEnhanced()`: Core filtering logic
   - `UpdateFilterStatusDisplay()`: UI status updates

2. **Filter Status Panel** (XAML)
   - Dynamic visibility based on filter state
   - Real-time row count updates
   - Clear all functionality

3. **Enhanced Filter Logic**
   - Multi-column AND operation
   - Case-insensitive comparison
   - Null/empty value handling

### Future Enhancements

#### Planned Features
1. **Custom Filter Expressions**: Cho phép users nhập điều kiện tùy chỉnh
2. **Filter Presets**: Lưu và áp dụng bộ filters thường dùng  
3. **OR Logic Option**: Thêm tùy chọn logic OR giữa các cột
4. **Advanced Text Filters**: Contains, Starts with, Ends with
5. **Number Range Filters**: Greater than, Less than, Between
6. **Date Range Filters**: Cho các trường thời gian

### Version History
- **v1.0**: Basic single-column filtering
- **v2.0**: Multi-column AND logic với enhanced UI
- **v2.1**: Performance optimization và keyboard shortcuts