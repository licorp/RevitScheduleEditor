# 🎯 Text Filters - Hướng dẫn cụ thể cho vấn đề của bạn

## ❌ Vấn đề bạn đang gặp

Từ log debug: 
```
[78484] [ScheduleEditorWindow] 05:35:36.663 - ShowFilterPopup - Selected 242 out of 242 total values 
[78484] [ScheduleEditorWindow] 05:35:36.663 - ShowFilterPopup - All items selected, removed filter for CRR_UQID_ASSET 
```

**➡️ Bạn chọn tất cả 242 items rồi nhấn OK, nên không có filter nào được áp dụng!**

## ✅ Giải pháp

### 🎯 Nguyên tắc Filter Excel:

```
✅ CHECKED items = Sẽ HIỂN THỊ
❌ UNCHECKED items = Sẽ ẨN ĐI
🔄 ALL CHECKED = Hiển thị tất cả = KHÔNG CÓ FILTER
```

### 📋 Để Filter đúng cách:

1. **Click nút Filter** (🔽) trên header cột CRR_UQID_ASSET
2. **BỎ CHỌN** (uncheck) những items bạn muốn **ẨN**
3. **GIỮ CHỌN** (checked) những items bạn muốn **HIỂN THỊ**
4. **Click OK**

### 🧪 Test với Demo Button:

**Thay vì test thủ công, hãy thử:**
1. Click nút **"Demo"** (màu cam) trên toolbar
2. Quan sát: Chỉ 1/3 số items được chọn (30%)
3. Click **OK** 
4. **Kết quả:** DataGrid sẽ chỉ hiển thị những rows có values được chọn

## 💡 Ví dụ cụ thể

**Giả sử cột CRR_UQID_ASSET có:**
- BAVBO02001 
- BAVBO02002
- BAVBO02003
- EFPBO02001
- FEXBO02800
- ... (tổng 242 items)

**Để chỉ hiển thị Ball Valve (BAVBO*):**
```
✅ BAVBO02001    ← GIỮ CHECKED
✅ BAVBO02002    ← GIỮ CHECKED  
✅ BAVBO02003    ← GIỮ CHECKED
☐ EFPBO02001    ← BỎ CHỌN (uncheck)
☐ FEXBO02800    ← BỎ CHỌN (uncheck)
☐ ...           ← BỎ CHỌN tất cả items khác
```

## ⚠️ Hệ thống sẽ cảnh báo

Khi bạn chọn tất cả items, dialog sẽ hiện warning:

> **"All items are selected, so no filtering will be applied."**
> 
> **"To filter data:**
> **• UNCHECK items you want to HIDE**
> **• Only CHECKED items will remain VISIBLE"**

## 🔍 Quan sát Status Text

Ở dưới dialog, có text cho biết tình trạng:

- `All 242 items selected (no filter will be applied)` ← **KHÔNG CÓ FILTER**
- `80 of 242 items selected (162 will be hidden)` ← **CÓ FILTER**
- `No items selected (all will be hidden)` ← **ẨN TẤT CẢ**

## 🎮 Thử ngay:

1. **Click "Demo" button** để xem filter hoạt động
2. Hoặc **click Filter button**, rồi **bỏ chọn** vài items và **click OK**
3. Quan sát DataGrid thay đổi

---

**Tóm lại:** Để filter, bạn cần **BỎ CHỌN** những gì muốn ẩn, không phải chọn tất cả!
