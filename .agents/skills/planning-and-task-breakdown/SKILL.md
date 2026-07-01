---
name: planning-and-task-breakdown
description: Breaks work into ordered tasks. Use when you have a spec or clear requirements and need to break work into implementable tasks. Use when a task feels too large to start, when you need to estimate scope, or when parallel work is possible.
---

# Lập kế hoạch và Chia nhỏ nhiệm vụ (Planning and Task Breakdown)

## Tổng quan

Phân rã công việc thành các nhiệm vụ (task) nhỏ, có thể xác minh được kèm theo tiêu chí nghiệm thu rõ ràng. Việc chia nhỏ task tốt là ranh giới giữa một agent hoàn thành công việc một cách ổn định và một agent tạo ra một mớ bòng bong hỗn độn. Mỗi task nên đủ nhỏ để triển khai, kiểm thử và xác minh chỉ trong một phiên làm việc tập trung (single session).

## Khi nào sử dụng

- Bạn đã có tài liệu đặc tả (spec) và cần chia nó thành các đơn vị triển khai cụ thể.
- Một nhiệm vụ lớn có vẻ quá mơ hồ hoặc quá rộng để bắt đầu.
- Công việc cần được phân chia song song cho nhiều agent hoặc phiên làm việc khác nhau.
- Bạn cần giải thích và truyền đạt phạm vi công việc (scope) cho con người.
- Thứ tự triển khai các phần việc chưa thực sự rõ ràng.

**Khi KHÔNG sử dụng:** Các thay đổi chỉ trên một file với phạm vi công việc hiển nhiên, hoặc khi tài liệu đặc tả đã chứa sẵn danh sách các task được định nghĩa tốt.

## Quy trình lập kế hoạch

### Bước 1: Vào chế độ lập kế hoạch (Plan Mode)

Trước khi viết bất kỳ dòng code nào, hãy hoạt động ở chế độ chỉ đọc (read-only mode):

- Đọc kỹ spec và các phần liên quan trong codebase hiện tại.
- Xác định các mẫu thiết kế (design patterns) và quy ước code sẵn có (MVVM, tiêm phụ thuộc - DI, vv.).
- Sắp xếp và vẽ sơ đồ phụ thuộc (dependencies) giữa các class và namespace.
- Ghi nhận các rủi ro và các điểm chưa rõ ràng (khóa file hệ thống, quyền hạn truy cập).

**KHÔNG viết code trong lúc lập kế hoạch.** Kết quả đầu ra của bước này là một tài liệu kế hoạch (plan document), không phải code triển khai.

### Bước 2: Xác định sơ đồ phụ thuộc (Dependency Graph)

Vẽ bản đồ thể hiện cái gì phụ thuộc vào cái gì:

```
Models (Thông tin thư mục rác, Danh sách ứng dụng cài đặt)
    │
    ├── Services (Quét Registry, Quét Thư mục hệ thống)
    │       │
    │       ├── Core Logic (Bộ so sánh & Nhận diện rác - OrphanDetector)
    │       │       │
    │       │       └── Action Layer (Bộ xóa file - FileCleaner)
    │       │
    │       └── ViewModel / Controller Layer
    │               │
    │               └── UI View (MainWindow.xaml)
```

Thứ tự triển khai sẽ đi từ dưới lên trên theo sơ đồ phụ thuộc: xây dựng phần móng (nền tảng) trước.

### Bước 3: Cắt lát theo chiều dọc (Slice Vertically)

Thay vì xây dựng toàn bộ cơ sở dữ liệu/model, sau đó viết toàn bộ logic quét, rồi làm toàn bộ UI — hãy xây dựng hoàn chỉnh từng tính năng một từ đầu đến cuối (lát cắt dọc):

**Tồi (cắt lát theo chiều ngang):**
```
Task 1: Xây dựng toàn bộ các cấu trúc dữ liệu / model
Task 2: Viết toàn bộ các hàm quét và tìm kiếm file
Task 3: Thiết kế toàn bộ giao diện WPF
Task 4: Kết nối mọi thứ lại với nhau
```

**Tốt (cắt lát theo chiều dọc):**
```
Task 1: Quét Registry (Đọc Registry và trả về danh sách ứng dụng đang cài đặt + viết unit test)
Task 2: Quét thư mục AppData (Duyệt thư mục AppData và tính dung lượng + viết unit test)
Task 3: So sánh tìm thư mục mồ côi (Đối chiếu hai danh sách và lọc ra thư mục rác + viết unit test)
Task 4: Thiết kế giao diện hiển thị & xóa (Hiển thị danh sách lên WPF ListView, tích chọn để xóa + test thủ công)
```

Mỗi lát cắt dọc đều mang lại một sản phẩm hoạt động được và có thể kiểm thử độc lập.

### Bước 4: Viết Task

Mỗi task cần tuân thủ cấu trúc sau:

```markdown
## Task [N]: [Tiêu đề ngắn gọn mô tả công việc]

**Description (Mô tả):** Một đoạn văn ngắn giải thích task này sẽ hoàn thành điều gì.

**Acceptance criteria (Tiêu chí nghiệm thu):**
- [ ] [Điều kiện cụ thể, có thể kiểm chứng]
- [ ] [Điều kiện cụ thể, có thể kiểm chứng]

**Verification (Xác minh):**
- [ ] Chạy test thành công: `dotnet test --filter "Namespace.TestClass"`
- [ ] Build thành công: `dotnet build`
- [ ] Kiểm tra thủ công: [Mô tả chi tiết cách kiểm tra thủ công để xác nhận]

**Dependencies (Phụ thuộc):** [Mã số của các task mà task này phụ thuộc vào, hoặc "None"]

**Files likely touched (Các file dự kiến thay đổi):**
- `src/Services/FolderScanner.cs`
- `tests/FolderScannerTests.cs`

**Estimated scope (Đánh giá phạm vi):** [Small: 1-2 files | Medium: 3-5 files | Large: 5+ files]
```

### Bước 5: Sắp xếp thứ tự và Điểm kiểm soát (Checkpoint)

Sắp xếp các task sao cho:

1. Các phụ thuộc được thỏa mãn trước (xây nền móng trước).
2. Mỗi task hoàn thành đều để lại hệ thống ở trạng thái chạy được bình thường.
3. Có điểm kiểm soát để xác minh (checkpoint) sau mỗi 2-3 tasks.
4. Các task có rủi ro cao được đẩy lên làm sớm (thất bại sớm - fail fast).

Thêm các checkpoints rõ ràng vào kế hoạch:

```markdown
## Checkpoint: Sau khi hoàn thành Task 1-3
- [ ] Toàn bộ các bài test đều chạy thành công
- [ ] Ứng dụng build không có lỗi
- [ ] Luồng quét tìm thư mục rác hoạt động chính xác (qua CLI hoặc debug log)
- [ ] Đánh giá lại với con người trước khi tiếp tục
```

## Hướng dẫn phân cỡ Task (Task Sizing)

| Kích cỡ | Số lượng files | Phạm vi công việc | Ví dụ thực tế |
|------|-------|-------|---------|
| **XS** | 1 | Một hàm đơn lẻ hoặc đổi cấu hình | Thêm một thư mục hệ thống vào whitelist |
| **S** | 1-2 | Một class hoặc helper đơn giản | Tạo service đọc Registry Windows |
| **M** | 3-5 | Một lát cắt tính năng hoàn chỉnh | Xây dựng luồng nhận diện thư mục rác |
| **L** | 5-8 | Tính năng liên quan nhiều thành phần | Giao diện WPF hiển thị, chọn và kích hoạt xóa |
| **XL** | 8+ | **Quá lớn — Bắt buộc phải chia nhỏ thêm** | — |

Nếu một task có cỡ L trở lên, bạn nên chia nhỏ nó ra. AI hoạt động tốt nhất với các task cỡ S và M.

**Khi nào cần chia nhỏ task thêm:**
- Task dự kiến mất nhiều hơn một phiên làm việc tập trung (khoảng hơn 2 giờ làm việc của agent).
- Bạn không thể mô tả tiêu chí nghiệm thu trong vòng 3 gạch đầu dòng trở xuống.
- Nó chạm tới hai hoặc nhiều hệ thống con độc lập (ví dụ: vừa xử lý UI vừa viết logic xóa file hệ thống).
- Bạn nhận thấy mình viết chữ "và" trong tiêu đề task (dấu hiệu cho thấy đó thực chất là hai task).

## Template mẫu cho tài liệu Kế hoạch (Plan Document)

```markdown
# Kế hoạch triển khai: [Tên tính năng/Dự án]

## Overview (Tổng quan)
[Một đoạn văn ngắn tóm tắt những gì chúng ta đang xây dựng]

## Architecture Decisions (Quyết định kiến trúc)
- [Quyết định cốt lõi 1 và lý do chọn]
- [Quyết định cốt lõi 2 và lý do chọn]

## Task List (Danh sách Task)

### Phase 1: Foundation (Nền tảng)
- [ ] Task 1: ...
- [ ] Task 2: ...

### Checkpoint: Foundation
- [ ] Test pass, build sạch sẽ

### Phase 2: Core Features (Tính năng cốt lõi)
- [ ] Task 3: ...
- [ ] Task 4: ...

### Checkpoint: Core Features
- [ ] Luồng chạy thực tế end-to-end hoạt động đúng

### Phase 3: Polish (Hoàn thiện)
- [ ] Task 5: ...
- [ ] Task 6: ...

### Checkpoint: Complete
- [ ] Mọi tiêu chí nghiệm thu đều được đáp ứng
- [ ] Sẵn sàng để review

## Risks and Mitigations (Rủi ro & Xử lý)
| Rủi ro phát sinh | Mức độ ảnh hưởng | Phương án phòng ngừa/xử lý |
|------|--------|------------|
| [Rủi ro] | [High/Med/Low] | [Phương án] |

## Open Questions (Câu hỏi mở)
- [Những câu hỏi cần con người làm rõ]
```

## Cơ hội làm việc song song (Parallelization)

Khi có nhiều agent hoặc nhiều phiên làm việc đồng thời:

- **An toàn để làm song song:** Các lát cắt tính năng độc lập, viết test cho các thành phần đã hoàn thành, viết tài liệu.
- **Bắt buộc phải làm tuần tự:** Thay đổi cấu trúc dữ liệu dùng chung, các chuỗi logic có tính phụ thuộc trực tiếp.
- **Cần phối hợp chặt chẽ:** Các tính năng chia sẻ chung một interface (cần định nghĩa interface trước, sau đó mới chia việc làm song song).

## Các biện hộ thường gặp (Common Rationalizations)

| Tự biện hộ | Thực tế |
|---|---|
| "Tôi sẽ tự nghĩ và làm trực tiếp" | Đó là cách nhanh nhất để tạo ra một đống code rối rắm và phải viết lại. Dành 10 phút lập kế hoạch sẽ tiết kiệm hàng giờ code. |
| "Các task quá rõ ràng rồi" | Vẫn nên viết ra. Việc ghi rõ ràng các task giúp phát hiện ra các phụ thuộc tiềm ẩn và các trường hợp biên bị bỏ quên. |
| "Lập kế hoạch chỉ là công việc thừa thãi" | Lập kế hoạch chính là công việc thiết kế. Code không có kế hoạch chỉ đơn thuần là gõ phím. |
| "Tôi có thể tự nhớ mọi thứ trong đầu" | Context window của AI là có hạn. Một kế hoạch được viết ra giấy sẽ tồn tại qua các phiên làm việc và không bị quên khi tóm tắt bối cảnh. |

## Dấu hiệu cảnh báo (Red Flags)

- Bắt đầu viết code triển khai mà không có danh sách task bằng văn bản.
- Task chỉ ghi chung chung "triển khai tính năng" mà không có tiêu chí nghiệm thu cụ thể.
- Kế hoạch không có bất kỳ bước xác minh nào.
- Mọi task đều có cỡ XL.
- Không có checkpoint nào giữa các nhiệm vụ.
- Thứ tự phụ thuộc của các task không được xem xét.

## Xác minh (Verification)

Trước khi bắt đầu code, hãy đảm bảo:

- [ ] Mọi task đều có tiêu chí nghiệm thu rõ ràng.
- [ ] Mọi task đều có bước xác minh (test, build, kiểm tra thủ công).
- [ ] Sự phụ thuộc giữa các task được xác định và sắp xếp đúng thứ tự.
- [ ] Không có task nào sửa đổi quá ~5 files.
- [ ] Có checkpoints rõ ràng giữa các giai đoạn chính.
- [ ] Con người đã xem và đồng ý với kế hoạch.
