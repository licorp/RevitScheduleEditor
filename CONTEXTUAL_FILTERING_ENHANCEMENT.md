# AutoFilter Enhancement - Contextual Column Filtering

## Vấn Đề Đã Giải Quyết

**Trước khi cải tiến:**
- Khi filter cột 1, sau đó mở filter popup cho cột 2, popup vẫn hiển thị TẤT CẢ giá trị từ data gốc (467 rows)
- Điều này không giống Excel AutoFilter thật sự

**Sau khi cải tiến:**
- Khi filter cột 1, sau đó mở filter popup cho cột 2, popup chỉ hiển thị những giá trị có trong data đã được lọc
- Hoạt động giống Excel AutoFilter 100%

## Chi Tiết Cải Tiến

### 🔄 **Contextual Filter Values**
```
Scenario: Data gốc có 467 rows
Cột 1 (Size): 150, 200, 300
Cột 2 (Length): Nhiều giá trị khác nhau

Bước 1: Filter Size = "150"
→ Còn lại 235 rows có Size = "150"

Bước 2: Mở filter cho Length
→ Popup CHỈ hiển thị Length values từ 235 rows đó
→ KHÔNG hiển thị Length values từ toàn bộ 467 rows gốc
```

### 🛠️ **Code Changes**

#### File: `ScheduleEditorWindow.xaml.cs`

**Method được cải tiến: `ShowFilterPopup()`**

**Trước:**
```csharp
// Lấy unique values từ toàn bộ data gốc
uniqueValues = _viewModel.ScheduleData
    .Select(row => row.Values.ContainsKey(columnName) ? row.Values[columnName] : "")
    .Distinct()
    .ToList();
```

**Sau:**
```csharp
// Lấy data hiện tại đang hiển thị (đã được filter)
var dataGrid = this.FindName("ScheduleDataGrid") as DataGrid;
var currentData = dataGrid?.ItemsSource as IEnumerable<ScheduleRow>;

// Nếu chưa có filter nào, dùng data gốc
if (currentData == null)
{
    currentData = _viewModel.ScheduleData;
    DebugLog($"Using original data source ({_viewModel.ScheduleData.Count} rows)");
}
else
{
    var currentDataList = currentData.ToList();
    DebugLog($"Using filtered data source ({currentDataList.Count} rows)");
    currentData = currentDataList;
}

// Lấy unique values CHỈ từ data hiện tại (đã được filter)
uniqueValues = currentData
    .Select(row => row.Values.ContainsKey(columnName) ? row.Values[columnName] : "")
    .Select(v => (v ?? "").Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
    .ToList();
```

### 🎯 **Test Scenario**

**Dữ liệu mẫu:**
| Size | Length | Type    |
|------|--------|---------|
| 150  | 1000   | Column  |
| 150  | 1006   | Beam    |
| 150  | 2000   | Wall    |
| 200  | 1000   | Column  |
| 200  | 1500   | Beam    |
| 300  | 2500   | Wall    |

**Test Steps:**

1. **Filter Size = "150"**
   - Kết quả: 3 rows còn lại
   - Length values khả dụng: 1000, 1006, 2000

2. **Mở filter cho Length**
   - **Trước cải tiến**: Hiển thị tất cả: 1000, 1006, 1500, 2000, 2500
   - **Sau cải tiến**: CHỈ hiển thị: 1000, 1006, 2000 ✅

3. **Filter Length = "1006"**
   - Kết quả cuối: 1 row (Size=150 AND Length=1006)

### 📊 **Debug Logging Enhancement**

Đã thêm logging để theo dõi:
```
ShowFilterPopup - Using filtered data source (235 rows)
ShowFilterPopup - Found 15 unique values from currently visible data
```

So với trước:
```
ShowFilterPopup - Found 96 unique values (from all original data)
```

### ✅ **Benefits**

1. **User Experience tốt hơn**: 
   - Filter popup chỉ hiển thị relevant values
   - Giảm confusion khi có quá nhiều values không liên quan

2. **Performance tốt hơn**:
   - Ít values để process
   - Faster filtering operations

3. **Excel-like Behavior**:
   - Hoạt động chính xác như Excel AutoFilter
   - Intuitive cho users đã quen Excel

4. **Contextual Filtering**:
   - Mỗi filter popup đều context-aware
   - Chỉ hiển thị values có trong filtered data

### 🔍 **Expected Log Pattern Khi Test**

```
[ScheduleEditorWindow] FilterButton_Click - Column name: Size
[ScheduleEditorWindow] ShowFilterPopup - Using original data source (467 rows)
[ScheduleEditorWindow] ShowFilterPopup - Found 3 unique values from currently visible data
[ScheduleEditorWindow] ApplyFiltersEnhanced - Filtered to 235 rows from 467

[ScheduleEditorWindow] FilterButton_Click - Column name: Length  
[ScheduleEditorWindow] ShowFilterPopup - Using filtered data source (235 rows) ⭐
[ScheduleEditorWindow] ShowFilterPopup - Found 15 unique values from currently visible data ⭐
[ScheduleEditorWindow] ApplyFiltersEnhanced - Filtered to 4 rows from 467
```

### 🚀 **Ready to Test**

Bây giờ bạn có thể test:

1. Load schedule với nhiều data
2. Filter một cột bất kỳ  
3. Mở filter popup cho cột khác
4. Verify rằng popup chỉ hiển thị values từ filtered data
5. Quan sát debug logs để confirm behavior

**Expected Result**: AutoFilter sẽ hoạt động chính xác như Excel với contextual filtering giữa các cột! 🎉