# Debug cho các vấn đề hiện tại

## 1. Text Filter Issue
Từ log: Filter chọn 13/14 items → hiển thị 227/236 rows
- User uncheck "Ball Valve" → muốn chỉ thấy những rows có "Ball Valve" 
- Nhưng hiện tại: uncheck "Ball Valve" → hiển thị tất cả trừ "Ball Valve"
- Cần đảo ngược logic: Unchecked items = items muốn HIỆN THỊ

## 2. Update Model Issue  
Log: "CanUpdateModel: Found 0 modified rows, can update: False"
- Data binding không trigger IsModified = true
- CellEditEnding handler không cập nhật row state
- Need to investigate ScheduleRow.SetValue implementation

## 3. Copy/Paste Issue
- DataGrid có SelectionMode="Extended", SelectionUnit="Cell" 
- Keyboard bindings đã setup: Ctrl+C, Ctrl+V, Ctrl+X
- Context menu đã có Copy/Paste options
- Có thể issue ở việc ExecuteCopy/ExecutePaste implementation

## Action Plan:
1. Fix text filter logic để match user expectation
2. Debug cell editing để trigger IsModified
3. Test copy/paste functionality
