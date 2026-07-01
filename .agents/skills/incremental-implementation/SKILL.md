---
name: incremental-implementation
description: Delivers changes incrementally. Use when implementing any feature or change that touches more than one file. Use when you're about to write a large amount of code at once, or when a task feels too big to land in one step.
---

# Triển khai từng bước (Incremental Implementation)

## Tổng quan

Xây dựng ứng dụng theo các lát cắt dọc mỏng (thin vertical slices) — triển khai một phần nhỏ, viết test, xác minh chạy đúng, sau đó mới mở rộng ra. Tránh việc cố gắng code toàn bộ một tính năng lớn trong một lần. Mỗi bước tăng trưởng (increment) nên để lại hệ thống ở trạng thái hoạt động được và có thể kiểm thử được. Đây là kỷ luật thực thi giúp kiểm soát các tính năng lớn một cách hiệu quả.

## Khi nào sử dụng

- Triển khai bất kỳ thay đổi nào liên quan đến nhiều file C# cùng lúc.
- Xây dựng một tính năng mới dựa trên danh sách task đã lập.
- Refactor (tái cấu trúc) mã nguồn C# hiện có.
- Bất cứ khi nào bạn có ý định viết nhiều hơn ~100 dòng code C# trước khi compile (biên dịch) hoặc chạy test.

**Khi KHÔNG sử dụng:** Thay đổi trên một file duy nhất hoặc một phương thức đơn lẻ với phạm vi công việc đã rất nhỏ gọn.

## Chu kỳ tăng trưởng (The Increment Cycle)

```
┌──────────────────────────────────────┐
│                                      │
│   Triển khai ──→ Test ──→ Xác minh ┐ │
│       ▲                            │ │
│       └───── Commit ◄──────────────┘ │
│              │                       │
│              ▼                       │
│        Lát cắt tiếp theo             │
│                                      │
└──────────────────────────────────────┘
```

Đối với mỗi lát cắt:

1. **Triển khai (Implement):** Viết phần code nhỏ nhất để hoàn thành một chức năng cụ thể.
2. **Kiểm thử (Test):** Chạy bộ test (`dotnet test`) hoặc viết thêm test nếu chưa có.
3. **Xác minh (Verify):** Xác nhận lát cắt đó chạy đúng thiết kế (test pass, build sạch, kiểm tra thủ công thành công).
4. **Commit:** Lưu lại tiến trình công việc của bạn với một thông điệp Git mô tả rõ ràng.
5. **Chuyển sang lát cắt tiếp theo:** Tiếp tục xây dựng trên nền tảng đã chạy tốt, không đập đi xây lại.

## Chiến thuật cắt lát (Slicing Strategies)

### Lát cắt dọc (Vertical Slices) - Khuyên dùng

Xây dựng một đường hoàn chỉnh đi qua các lớp cấu trúc:

```
Lát cắt 1: Đọc Registry (Viết bộ quét Registry + xUnit test)
    → Test pass, chương trình in ra danh sách ứng dụng đã cài đặt ở logs.

Lát cắt 2: Quét thư mục AppData (Viết logic duyệt thư mục + tính dung lượng + xUnit test)
    → Test pass, chương trình tính được dung lượng thư mục trong AppData.

Lát cắt 3: Đối chiếu tìm rác (Viết logic so sánh tìm thư mục mồ côi + Whitelist + xUnit test)
    → Test pass, chương trình liệt kê đúng các thư mục rác của app đã gỡ.

Lát cắt 4: Tích hợp Giao diện (Thiết kế cửa sổ WPF + cơ chế xóa an toàn + manual check)
    → Build thành công, người dùng có thể thao tác tích chọn và xóa từ giao diện.
```

Mỗi lát cắt đều mang lại một chức năng thực tế hoạt động được từ đầu đến cuối.

### Cắt lát ưu tiên Interface (Contract-First Slicing)

Khi phần logic nghiệp vụ (backend) và giao diện (UI) cần được phát triển song song:

```
Lát cắt 0: Định nghĩa các interface và dịch vụ dùng chung (IFolderScanner, IOrphanDetector)
Lát cắt 1a: Triển khai các service cụ thể dựa trên interface + viết xUnit test
Lát cắt 1b: Triển khai ViewModel sử dụng các dữ liệu giả lập (mock) khớp với interface
Lát cắt 2: Tích hợp giao diện WPF thực tế và kiểm thử end-to-end
```

### Cắt lát ưu tiên xử lý rủi ro (Risk-First Slicing)

Giải quyết phần việc rủi ro cao hoặc có nhiều điểm chưa chắc chắn nhất trước tiên:

```
Lát cắt 1: Viết logic duyệt các thư mục hệ thống bị khóa hoặc phân quyền hạn chế (rủi ro crash cao nhất)
Lát cắt 2: Xây dựng logic quét thông thường trên nền tảng xử lý phân quyền đã chạy tốt ở lát cắt 1
Lát cắt 3: Thêm bộ lọc Whitelist và cơ chế ghi log chi tiết
```

Nếu Lát cắt 1 thất bại, bạn sẽ phát hiện và khắc phục được ngay trước khi đầu tư thời gian vào viết UI hay logic so sánh.

## Các quy tắc triển khai

### Quy tắc 0: Ưu tiên sự đơn giản (Simplicity First)

Trước khi viết bất kỳ đoạn code nào, hãy tự hỏi: "Giải pháp đơn giản nhất có thể chạy được là gì?"

Sau khi viết xong code, hãy đối chiếu với các câu hỏi sau:
- Có cách nào viết ngắn gọn, dễ hiểu hơn không?
- Các lớp trừu tượng (abstractions) này có thực sự cần thiết không?
- Một lập trình viên có kinh nghiệm nhìn vào có hỏi "tại sao không dùng cách đơn giản này cho nhanh..." không?
- Mình đang code để giải quyết task hiện tại, hay đang chuẩn bị cho một yêu cầu giả định trong tương lai?

```
KIỂM TRA SỰ ĐƠN GIẢN:
✗ Viết một hệ thống Event Aggregator phức tạp chỉ để truyền một callback trạng thái đơn giản.
✓ Sử dụng sự kiện C# standard (Event) hoặc Action delegate thông thường.

✗ Thiết kế Abstract Factory phức tạp để khởi tạo hai service quét file đơn giản.
✓ Khởi tạo trực tiếp bằng từ khóa new hoặc sử dụng Dependency Injection mặc định của .NET.
```

Đừng vội vàng tạo ra các lớp trừu tượng khi chưa thực sự cần thiết. Hãy viết phiên bản code tường minh, dễ hiểu nhất trước. Chỉ tối ưu hóa hiệu năng sau khi đã chứng minh được tính đúng đắn của code bằng các bài test.

### Quy tắc 0.5: Kỷ luật về Phạm vi công việc (Scope Discipline)

Chỉ chỉnh sửa những gì nhiệm vụ yêu cầu.

TUYỆT ĐỐI TRÁNH:
- "Dọn dẹp tiện tay" các file code lân cận không liên quan.
- Sửa lại các namespace, import dư thừa ở những file bạn chỉ đọc mà không sửa.
- Xóa các dòng comment mà bạn chưa hiểu rõ mục đích.
- Thêm thắt các tính năng ngoài spec vì cảm thấy "nó có vẻ hay".

Nếu bạn phát hiện điểm cần cải tiến nằm ngoài phạm vi task hiện tại, hãy ghi chú lại chứ đừng tự ý sửa:

```
NHẬN THẤY NHƯNG KHÔNG CHẠM VÀO:
- File Utility.cs có một hàm không dùng tới (không liên quan tới task quét file).
- Hàm Log của app có thể viết chi tiết hơn (sẽ tạo task riêng sau).
→ Bạn có muốn tôi tạo task riêng cho các vấn đề này không?
```

### Quy tắc 1: Mỗi lần chỉ làm một việc
Mỗi increment chỉ thay đổi một logic duy nhất. Không trộn lẫn các công việc khác nhau (ví dụ: vừa refactor vừa thêm tính năng mới trong cùng một commit).

### Quy tắc 2: Luôn giữ trạng thái Build thành công
Sau mỗi bước chỉnh sửa, dự án phải build thành công (`dotnet build`) và các bài test cũ vẫn phải chạy xanh (`dotnet test`). Không để mã nguồn ở trạng thái lỗi biên dịch giữa các bước.

### Quy tắc 3: Sử dụng cấu hình tính năng (AppSettings / Feature Flags)
Nếu một tính năng chưa hoàn thiện nhưng bạn cần đưa code lên nhánh chính:

```csharp
// Đọc cấu hình từ appsettings.json
bool enableRegistryScan = _configuration.GetValue<bool>("Features:EnableRegistryScan");

if (enableRegistryScan)
{
    // Chạy logic quét mới đang thử nghiệm
}
```

Việc này giúp bạn đưa các phần code nhỏ lên Git mà không sợ làm ảnh hưởng đến các tính năng đang chạy bình thường của người dùng.

### Quy tắc 4: Cấu hình mặc định an toàn (Safe Defaults)
Các phương thức mới nên mặc định ở trạng thái an toàn nhất (ví dụ: chế độ DryRun - chạy thử không xóa thật đối với các công cụ dọn dẹp file).

```csharp
public void DeleteFolder(string path, bool dryRun = true)
{
    if (dryRun)
    {
        _logger.LogInformation($"[Chạy thử] Sẽ xóa thư mục: {path}");
        return;
    }
    _fileSystem.Directory.Delete(path, true);
}
```

## Cách làm việc với Agent (AI)

Khi yêu cầu AI thực hiện công việc từng bước:

```
"Hãy thực hiện Task 3 trong kế hoạch.

Bắt đầu bằng việc viết class so sánh logic (OrphanDetector) và các unit test tương ứng.
Chưa cần đụng tới giao diện WPF vội — chúng ta sẽ thiết kế UI ở bước tiếp theo.

Sau khi code xong, hãy chạy `dotnet test` và `dotnet build` để đảm bảo mọi thứ không bị lỗi."
```

Luôn rõ ràng về phạm vi công việc được phép làm và phần việc chưa được phép chạm tới cho từng bước.

## Checklist cho mỗi bước tăng trưởng

Sau mỗi bước chỉnh sửa, hãy xác minh:

- [ ] Thay đổi chỉ tập trung giải quyết một việc và đã giải quyết trọn vẹn.
- [ ] Toàn bộ các bài test cũ và mới đều chạy thành công (`dotnet test`).
- [ ] Dự án build sạch sẽ không lỗi (`dotnet build`).
- [ ] Sự thay đổi đã được commit với mô tả Git rõ ràng.

## Các biện hộ thường gặp (Common Rationalizations)

| Tự biện hộ | Thực tế |
|---|---|
| "Tôi sẽ viết test một thể vào lúc cuối" | Lỗi sẽ bị chồng chéo. Code sai từ bước 1 sẽ kéo theo logic bước 2-5 sai theo. Hãy viết test cho từng bước. |
| "Làm tất cả cùng lúc sẽ nhanh hơn" | Cảm giác viết một mạch sẽ nhanh hơn, cho tới khi chương trình bị lỗi và bạn phải mò trong 500 dòng code mới sửa xem dòng nào gây lỗi. |
| "Các thay đổi này nhỏ quá, không bõ commit riêng" | Commit là miễn phí. Các commit lớn gộp nhiều việc sẽ giấu đi các lỗi nhỏ và khiến việc rollback (quay xe) khi có lỗi trở thành thảm họa. |
| "Tôi sẽ thêm cấu hình ẩn tính năng sau" | Nếu tính năng chưa hoàn thành, nó không được phép xuất hiện trước mắt người dùng. Hãy viết file cấu hình ngay bây giờ. |

## Dấu hiệu cảnh báo (Red Flags)

- Viết hơn 100 dòng code C# mà chưa từng compile thử hoặc chạy test lần nào.
- Thực hiện nhiều thay đổi không liên quan trong cùng một bước chỉnh sửa.
- Tự ý mở rộng phạm vi công việc ("tiện tay làm thêm cái này").
- Bỏ qua bước test/verify để chuyển sang việc khác cho nhanh.
- Để dự án ở trạng thái lỗi build hoặc lỗi test giữa các bước code.
- Gom quá nhiều thay đổi lớn mà không commit.

## Xác minh (Verification)

Sau khi hoàn thành tất cả các bước của một task:

- [ ] Mỗi bước chỉnh sửa nhỏ đều được test và commit riêng lẻ.
- [ ] Toàn bộ bộ test chạy thành công.
- [ ] Dự án build không có cảnh báo hoặc lỗi.
- [ ] Tính năng hoạt động đúng từ đầu đến cuối như đặc tả.
- [ ] Không còn thay đổi nào chưa được commit (no uncommitted changes).
