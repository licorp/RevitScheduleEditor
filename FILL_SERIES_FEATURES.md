# Tính năng Fill Series - RevitScheduleEditor

## Mô tả
Tính năng Fill Series cho phép người dùng tự động tạo chuỗi số hoặc text có số một cách thông minh, tương tự như Excel.

## Cách sử dụng

### 1. Fill Series với Fill Handle (Kéo thả)

**Thao tác thông thường (Copy):**
- Chọn một cell có giá trị
- Kéo fill handle (ô vuông nhỏ ở góc dưới-phải) để chọn vùng cần fill
- Thả chuột → Giá trị sẽ được copy sang các cell khác

**Fill Series với Ctrl+Kéo:**
- Chọn một cell có giá trị số hoặc text có số (ví dụ: "5", "Item 10", "Room 201")
- **Giữ phím Ctrl** và kéo fill handle
- Thả chuột → Chuỗi số sẽ được tạo tự động (5→6→7, Item 10→Item 11→Item 12, v.v.)

**Visual Feedback:**
- Kéo thông thường: Highlight màu xanh dương
- Kéo với Ctrl: Highlight màu xanh lá (Fill Series mode)
- Con trở chuột thay đổi: Arrow → Cross khi giữ Ctrl

### 2. Fill Series với Phím tắt

**Ctrl+Shift+Enter:**
- Chọn nhiều cell (ít nhất 2 cell)
- Nhấn **Ctrl+Shift+Enter**
- Cell đầu tiên sẽ làm giá trị gốc, các cell sau sẽ được fill series

### 3. Fill Series từ Context Menu

**Click chuột phải:**
- Chọn nhiều cell
- Click chuột phải → Chọn **"📊 Fill Series"**
- Hoặc sử dụng phím tắt **Ctrl+Shift+Enter**

## Các Pattern được hỗ trợ

### 1. Số thuần túy
- **Input:** `5`
- **Output:** `5, 6, 7, 8, 9...`

### 2. Text có số ở cuối
- **Input:** `Room 101`
- **Output:** `Room 101, Room 102, Room 103...`
- **Input:** `Item 5`
- **Output:** `Item 5, Item 6, Item 7...`

### 3. Text có số ở đầu
- **Input:** `10 Pieces`
- **Output:** `10 Pieces, 11 Pieces, 12 Pieces...`

### 4. Text không có số
- **Input:** `Sample`
- **Output:** `Sample 2, Sample 3, Sample 4...`

## Hướng dẫn Quick Start

### Ví dụ cơ bản:
1. Nhập `"Item 1"` vào cell A1
2. Chọn từ A1 đến A10
3. Nhấn **Ctrl+Shift+Enter**
4. Kết quả: `Item 1, Item 2, Item 3...Item 10`

### Ví dụ với số:
1. Nhập `"100"` vào cell B1
2. Chọn từ B1 đến B5
3. Giữ **Ctrl** + kéo fill handle từ B1 đến B5
4. Kết quả: `100, 101, 102, 103, 104`

## So sánh với các tính năng khác

| Tính năng | Phím tắt | Hành vi |
|-----------|----------|---------|
| **Fill Series** | **Ctrl+Shift+Enter** | **Tạo chuỗi số tự động** |
| Smart Fill | Shift+Enter | Phát hiện pattern từ 2 giá trị đầu |
| Copy Fill | Ctrl+Enter | Copy giá trị đầu tiên |
| Auto Fill | Double-click | Thông minh theo context |

## Tips và Tricks

1. **Kiểm tra Visual Feedback:** Màu xanh lá = Fill Series mode
2. **Pattern Recognition:** Tool sẽ tự phát hiện số trong text
3. **Undo Support:** Mọi thao tác Fill Series đều có thể Undo
4. **Multi-column Support:** Có thể Fill Series nhiều cột cùng lúc

## Status Bar
Ở dưới cùng window có hướng dẫn ngắn gọn:
- 📋 **Autofill:** Double-click (multi-select) • Ctrl+Enter (copy) • Shift+Enter (smart) • **Ctrl+Shift+Enter (series)**
- 🖱️ **Fill Handle:** Drag (copy) • **Ctrl+Drag (series)**

---
*Tính năng này được thiết kế để tương tự Excel, giúp người dùng làm việc hiệu quả hơn với dữ liệu Schedule trong Revit.*