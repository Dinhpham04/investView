---
name: source-driven-development
description: Grounds every implementation decision in official documentation. Use when you want authoritative, source-cited code free from outdated patterns. Use when building with any framework or library where correctness matters.
---

# Phát triển dựa trên Tài liệu gốc (Source-Driven Development)

## Tổng quan

Mọi quyết định viết code liên quan đến framework hoặc thư viện phải được bảo chứng bằng tài liệu chính thức (official documentation). Không tự ý viết code theo trí nhớ — hãy đối chiếu, trích dẫn nguồn rõ ràng và cho phép người dùng kiểm tra các nguồn đó. Dữ liệu huấn luyện của AI có thể bị cũ, các API bị deprecate (khai tử), và các thực hành tốt nhất (best practices) luôn thay đổi. Kỹ năng này đảm bảo người dùng nhận được code đáng tin cậy vì mọi pattern viết code đều có thể truy vết về nguồn tài liệu chính thống.

## Khi nào sử dụng

- Người dùng muốn code tuân thủ các best practices mới nhất của .NET.
- Viết code mẫu (boilerplate), mã nguồn khởi tạo hoặc các cấu trúc code sẽ được nhân bản ra nhiều nơi trong dự án.
- Triển khai các tính năng mà cách thiết kế chuẩn của framework đóng vai trò quan trọng (WPF Data Binding, kiến trúc MVVM, truy cập registry, phân quyền file).
- Đánh giá (review) hoặc cải tiến code hiện tại đang sử dụng các API của hệ thống.
- Bất cứ khi nào bạn chuẩn bị viết một đoạn code C# liên quan đến hệ điều hành hoặc hệ thống file dựa trên trí nhớ.

**Khi KHÔNG sử dụng:**

- Tính đúng đắn của code không phụ thuộc vào phiên bản cụ thể (đặt lại tên biến, sửa chính tả, di chuyển file).
- Logic thuần túy chạy giống nhau ở mọi phiên bản (vòng lặp, câu điều kiện, cấu trúc dữ liệu cơ bản).
- Người dùng yêu cầu rõ ràng ưu tiên tốc độ hơn là xác minh ("cứ viết đại cho chạy được đã").

## Quy trình thực hiện

```
DETECT (NHẬN DIỆN) ──→ FETCH (TRA CỨU) ──→ IMPLEMENT (TRIỂN KHAI) ──→ CITE (TRÍCH DẪN)
        │                   │                      │                       │
        ▼                   ▼                      ▼                       ▼
   Công nghệ và        Đọc đúng tài        Viết code khớp với      Trình bày nguồn tài
 phiên bản nào?       liệu cần thiết        tài liệu chuẩn         liệu cho người dùng
```

### Bước 1: Nhận diện phiên bản công nghệ (Detect Stack and Versions)

Đọc file cấu hình của dự án để xác định phiên bản chính xác:

```
.csproj         → Target Framework (net8.0-windows, net9.0, vv.), phiên bản các thư viện NuGet
App.config      → Các cấu hình hệ thống cũ (nếu có)
```

Nêu rõ những gì bạn tìm thấy:

```
STACK DETECTED (CÔNG NGHỆ NHẬN DIỆN ĐƯỢC):
- Target Framework: net8.0-windows (từ CleanMemoryApp.csproj)
- Sử dụng thư viện: System.IO.Abstractions (NuGet)
- Giao diện: WPF (Windows Presentation Foundation)
→ Đang tiến hành tra cứu tài liệu chính thức từ Microsoft Learn cho các API tương ứng.
```

Nếu phiên bản không rõ ràng hoặc bị thiếu, **hãy hỏi người dùng**. Đừng tự đoán — phiên bản công nghệ sẽ quyết định đoạn code nào là chuẩn xác.

### Bước 2: Tra cứu tài liệu chính thức (Fetch Official Documentation)

Tìm kiếm và đọc chính xác trang tài liệu liên quan đến tính năng bạn đang viết. Không đọc trang chủ chung chung, không đọc tài liệu tổng quát — hãy đọc đúng trang hướng dẫn cụ thể của API đó.

**Hệ thống phân cấp nguồn tài liệu (Sắp xếp theo độ tin cậy từ cao xuống thấp):**

| Độ ưu tiên | Nguồn tài liệu | Ví dụ thực tế |
|----------|--------|---------|
| 1 | Tài liệu Microsoft chính thức | learn.microsoft.com/en-us/dotnet/ |
| 2 | Blog chính thức của .NET hoặc Github repo | devblogs.microsoft.com/dotnet/, github.com/dotnet/ |
| 3 | Tài liệu của tác giả thư viện NuGet | Trang chủ GitHub của System.IO.Abstractions |

**Không đáng tin cậy — Không bao giờ dùng làm nguồn trích dẫn chính:**

- Các câu trả lời trên Stack Overflow.
- Các bài viết blog cá nhân hoặc hướng dẫn (tutorials) của bên thứ ba.
- Các tài liệu hoặc tóm tắt do AI tự tạo ra.
- Dữ liệu huấn luyện sẵn có của chính bạn (mục đích của kỹ năng này là đối chiếu thực tế để chống lỗi thời).

**Tìm kiếm chính xác trang tài liệu cần thiết:**

```
TỆ:  Đọc tài liệu chung về File System trên .NET.
TỐT: Truy cập learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratefiles
```

Sau khi đọc tài liệu, hãy chú ý đến các cảnh báo deprecation (khai tử API) hoặc hướng dẫn nâng cấp (migration guides).

Nếu xảy ra mâu thuẫn giữa các tài liệu chính thức (ví dụ: tài liệu hướng dẫn nâng cấp mâu thuẫn với tài liệu đặc tả API), hãy trình bày rõ ràng với người dùng để kiểm tra thực tế trên phiên bản hiện tại.

### Bước 3: Triển khai theo tài liệu chuẩn (Implement Following Documented Patterns)

Viết code khớp với những gì tài liệu chính thức hướng dẫn:

- Tuân thủ đúng cấu trúc hàm và kiểu dữ liệu (signatures) từ tài liệu, không tự ý sáng tạo.
- Sử dụng các tính năng ngôn ngữ mới nhất (như C# 12/13 primary constructors, collection expressions) nếu phiên bản target .NET hỗ trợ.
- Tuyệt đối không dùng các API đã bị cảnh báo ngưng hỗ trợ (deprecated).

**Khi tài liệu mâu thuẫn với code hiện có trong dự án:**

```
PHÁT HIỆN MÂU THUẪN:
Mã nguồn hiện tại của dự án đang dùng Registry.LocalMachine để lưu cấu hình ứng dụng,
tuy nhiên tài liệu bảo mật của Microsoft khuyến nghị dùng Registry.CurrentUser để tránh việc yêu cầu người dùng chạy quyền Administrator (UAC).
(Nguồn: https://learn.microsoft.com/en-us/dotnet/standard/security/key-security-guidelines)

Các lựa chọn:
A) Chuyển sang dùng HKEY_CURRENT_USER — bảo mật hơn, chạy không cần quyền admin.
B) Giữ nguyên HKEY_LOCAL_MACHINE như code cũ — đồng bộ với codebase nhưng sẽ nhắc UAC.
→ Bạn muốn tôi thực hiện theo phương án nào?
```

Hãy đưa mâu thuẫn này ra thảo luận với người dùng, đừng âm thầm tự lựa chọn.

### Bước 4: Trích dẫn nguồn (Cite Your Sources)

Mọi đoạn code sử dụng các API đặc thù đều cần được trích dẫn nguồn. Việc này giúp người dùng có thể tự đối chiếu lại quyết định viết code của bạn.

**Ghi chú trong Code Comment:**

```csharp
// Sử dụng Directory.EnumerateDirectories để duyệt thư mục tiết kiệm bộ nhớ RAM
// Nguồn: https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratedirectories
var directories = Directory.EnumerateDirectories(path);
```

**Trình bày trong hội thoại:**

```
Tôi đề xuất dùng Directory.EnumerateDirectories thay thế cho Directory.GetDirectories.
EnumerateDirectories tối ưu hơn cho các thư mục chứa hàng triệu file vì nó trả về IEnumerable (duyệt tới đâu nạp tới đó) thay vì tải toàn bộ danh sách đường dẫn vào bộ nhớ RAM cùng lúc.

Nguồn: https://learn.microsoft.com/en-us/dotnet/api/system.io.directory.enumeratedirectories
"The EnumerateDirectories and GetDirectories methods differ as follows: When you use EnumerateDirectories, you can start enumerating the collection of names before the whole collection is returned..."
```

**Quy tắc trích dẫn:**

- Ghi link đầy đủ (full URL), không dùng link rút gọn.
- Ưu tiên link sâu dẫn tới đúng vị trí neo (anchor) của tính năng (ví dụ: `/enumeratedirectories#usage` thay vì chỉ ghi `/enumeratedirectories`).
- Trích dẫn trực tiếp đoạn văn ngắn từ tài liệu nếu nó hỗ trợ một quyết định thiết kế không hiển nhiên.
- Nếu không thể tìm thấy tài liệu chính thức cho một mẫu code, hãy ghi rõ ràng:

```
CHƯA XÁC MINH: Tôi không tìm thấy tài liệu chính thức từ Microsoft cho signature PInvoke của hàm Windows API này. Đoạn code này được viết dựa trên kiến thức cũ và có thể không hoàn toàn chính xác. Hãy test kỹ trước khi đưa vào chạy thật.
```

## Các biện hộ thường gặp (Common Rationalizations)

| Tự biện hộ | Thực tế |
|---|---|
| "Tôi rất tự tin về hàm này" | Sự tự tin không phải là bằng chứng. Kiến thức cũ của bạn có thể chứa các lỗi thời đã bị thay đổi ở các phiên bản .NET mới hơn. Hãy đối chiếu. |
| "Đọc tài liệu làm tốn token của tôi" | Viết sai code còn làm tốn nhiều token hơn. Người dùng mất hàng giờ debug rồi phát hiện ra signature của API đã đổi. Một lần đọc tài liệu giúp tránh hàng giờ sửa lỗi. |
| "Tôi sẽ ghi chú là code này có thể hơi cũ" | Ghi chú vô trách nhiệm như vậy không có ích gì cả. Hãy đối chiếu và trích dẫn, hoặc ghi rõ là code chưa được xác minh. |

## Dấu hiệu cảnh báo (Red Flags)

- Viết các hàm hệ thống mà chưa từng mở trang tài liệu chính thức của API đó.
- Dùng các cụm từ "tôi nghĩ là", "tôi tin là" khi nói về một API thay vì đưa ra link trích dẫn.
- Sử dụng các API đã lỗi thời (obsolete/deprecated) chỉ vì nó xuất hiện trong các ví dụ cũ trên mạng.
- Gửi code cho người dùng mà không có bất kỳ nguồn trích dẫn nào cho các quyết định kiến trúc lớn.

## Xác minh (Verification)

Sau khi hoàn thành:

- [ ] Phiên bản target framework đã được xác định rõ qua file `.csproj`.
- [ ] Tài liệu chính thức từ Microsoft Learn đã được đọc để kiểm tra các API sử dụng.
- [ ] Mọi nguồn tài liệu đều là tài liệu chính thống, không dùng blog cá nhân làm nguồn chính.
- [ ] Code tuân thủ đúng các pattern hướng dẫn của phiên bản .NET hiện tại.
- [ ] Các quyết định viết code quan trọng đều đi kèm link trích dẫn đầy đủ.
- [ ] Không sử dụng các API đã bị đánh dấu ngưng hỗ trợ.
- [ ] Các mâu thuẫn giữa tài liệu và code cũ được đưa ra làm rõ với người dùng.
