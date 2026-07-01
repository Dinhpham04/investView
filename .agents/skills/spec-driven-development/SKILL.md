---
name: spec-driven-development
description: Creates specs before coding. Use when starting a new project, feature, or significant change and no specification exists yet. Use when requirements are unclear, ambiguous, or only exist as a vague idea.
---

# Phát triển dựa trên Đặc tả (Spec-Driven Development)

## Tổng quan

Viết một bản đặc tả kỹ thuật cấu trúc (specification/spec) trước khi viết bất kỳ đoạn code nào. Bản đặc tả là nguồn sự thật chung duy nhất giữa bạn và kỹ sư (con người) — nó định nghĩa những gì chúng ta đang xây dựng, lý do tại sao, và làm thế nào để biết nó đã hoàn thành. Viết code mà không có đặc tả giống như một trò chơi đoán mò.

## Khi nào sử dụng

- Bắt đầu một dự án hoặc tính năng mới.
- Các yêu cầu còn mơ hồ hoặc chưa đầy đủ.
- Thay đổi chạm tới nhiều file hoặc nhiều mô-đun khác nhau.
- Bạn chuẩn bị đưa ra một quyết định về mặt kiến trúc.
- Nhiệm vụ dự kiến mất hơn 30 phút để thực hiện.

**Khi KHÔNG sử dụng:** Sửa lỗi một dòng, sửa lỗi chính tả, hoặc các thay đổi nhỏ mà yêu cầu đã rất rõ ràng và tự đóng gói (self-contained).

## Quy trình kiểm soát theo cổng (The Gated Workflow)

Phát triển dựa trên đặc tả có 4 giai đoạn. Không được chuyển sang giai đoạn tiếp theo cho đến khi giai đoạn hiện tại được xác nhận và thông qua.

```
SPECIFY (Đặc tả) ──→ PLAN (Lập kế hoạch) ──→ TASKS (Tạo Task) ──→ IMPLEMENT (Triển khai)
       │                    │                     │                       │
       ▼                    ▼                     ▼                       ▼
 Con người đánh giá   Con người đánh giá    Con người đánh giá      Con người đánh giá
```

### Giai đoạn 1: Đặc tả (Specify)

Bắt đầu bằng một tầm nhìn cấp cao. Đặt các câu hỏi làm rõ với con người cho đến khi các yêu cầu trở nên cụ thể.

**Nêu rõ các giả định ngay lập tức.** Trước khi viết nội dung đặc tả, hãy liệt kê những giả định bạn đang đưa ra:

```
ASSUMPTIONS I'M MAKING (CÁC GIẢ ĐỊNH CỦA TÔI):
1. Đây là ứng dụng desktop (không phải mobile hay web).
2. Framework sử dụng là .NET 8.0/9.0 (WPF) chạy trên Windows.
3. Việc quét file sử dụng System.IO.Abstractions để phục vụ việc viết test.
4. Việc xóa file yêu cầu quyền admin hoặc xử lý ngoại lệ phân quyền một cách khéo léo.
→ Xác nhận lại với tôi ngay bây giờ hoặc tôi sẽ tiếp tục thực hiện theo các giả định này.
```

Đừng âm thầm tự quyết định các yêu cầu mơ hồ. Mục đích của bản đặc tả là đưa các điểm hiểu nhầm ra ánh sáng *trước khi* viết code — các giả định ngầm là dạng hiểu nhầm nguy hiểm nhất.

**Viết một tài liệu đặc tả (spec) bao gồm 6 phần cốt lõi:**

1. **Objective (Mục tiêu)** — Chúng ta đang xây dựng cái gì và tại sao? Ai là người dùng? Thành công trông như thế nào?
2. **Commands (Câu lệnh)** — Các câu lệnh thực thi đầy đủ kèm theo các cờ (flags), không chỉ ghi tên công cụ chung chung.
   ```
   Build: dotnet build
   Test: dotnet test
   Run: dotnet run --project src/CleanMemoryApp
   ```
3. **Project Structure (Cấu trúc dự án)** — Nơi chứa mã nguồn, nơi đặt các bài test, nơi lưu trữ tài liệu.
   ```
   src/               → Mã nguồn ứng dụng
   src/Models/        → Cấu trúc dữ liệu
   src/Services/      → Logic quét (Scanner) và dọn dẹp (Cleaner)
   src/Views/         → Các màn hình WPF (XAML)
   tests/             → Unit test và integration test (xUnit)
   docs/              → Tài liệu kỹ thuật (ADRs, specs)
   ```
4. **Code Style (Phong cách viết code)** — Một đoạn code mẫu thực tế thể hiện phong cách viết của bạn tốt hơn ba đoạn văn dài mô tả nó. Hãy đưa ra các quy ước đặt tên, quy tắc định dạng và ví dụ cụ thể.
5. **Testing Strategy (Chiến lược kiểm thử)** — Dùng framework nào, các bài test được lưu ở đâu, kỳ vọng về độ phủ (coverage), mức độ kiểm thử cho từng thành phần.
6. **Boundaries (Giới hạn công việc)** — Hệ thống phân cấp 3 mức:
   - **Always do (Luôn làm):** Chạy test trước khi commit, tuân thủ quy ước đặt tên C#, kiểm tra dữ liệu đầu vào.
   - **Ask first (Hỏi trước):** Cài thêm thư viện NuGet mới, can thiệp vào Windows Registry.
   - **Never do (Không bao giờ làm):** Tự ý xóa file khi người dùng chưa chọn, bỏ qua các ngoại lệ hệ thống file (IO exceptions), commit các thông tin nhạy cảm.

**Template mẫu của tài liệu Spec:**

```markdown
# Spec: [Tên dự án/Tính năng]

## Objective (Mục tiêu)
[Chúng ta xây dựng cái gì và tại sao. Các câu chuyện người dùng hoặc tiêu chí nghiệm thu.]

## Tech Stack (Công nghệ sử dụng)
[Framework, ngôn ngữ, các thư viện NuGet quan trọng kèm version]

## Commands (Câu lệnh)
[Lệnh Build, test, run — ghi đầy đủ lệnh]

## Project Structure (Cấu trúc dự án)
[Sơ đồ thư mục kèm mô tả ngắn]

## Code Style (Phong cách code)
[Đoạn code ví dụ + các quy ước cốt lõi]

## Testing Strategy (Chiến lược kiểm thử)
[Framework sử dụng, vị trí file test, mức độ test]

## Boundaries (Giới hạn)
- Luôn làm: [...]
- Hỏi trước: [...]
- Không bao giờ làm: [...]

## Success Criteria (Tiêu chí thành công)
[Làm thế nào để biết tính năng đã xong — các điều kiện cụ thể, có thể kiểm chứng được]

## Open Questions (Câu hỏi mở)
[Những điểm chưa rõ ràng cần con người xác nhận]
```

**Chuyển đổi yêu cầu chung chung thành tiêu chí thành công cụ thể.** Khi nhận được yêu cầu mơ hồ, hãy dịch nó sang các điều kiện kỹ thuật rõ ràng:

```
YÊU CẦU: "Làm cho tính năng quét thư mục chạy nhanh hơn"

TIÊU CHÍ THÀNH CÔNG ĐƯỢC DỊCH LẠI:
- Quét xong thư mục chứa 100,000 files trong dưới 2 giây.
- Sử dụng cơ chế duyệt lặp thông minh (yield return / IEnumerable) để không ngốn RAM.
- CPU sử dụng không vượt quá 30% trong suốt quá trình quét.
→ Đây có phải là các mục tiêu bạn mong muốn không?
```

Việc này giúp bạn có một đích đến rõ ràng để đo lường thay vì tự đoán xem thế nào là "nhanh hơn".

### Giai đoạn 2: Lập kế hoạch (Plan)

Sau khi bản spec được duyệt, hãy lập kế hoạch triển khai chi tiết:

1. Xác định các thành phần chính và sự phụ thuộc (dependencies) giữa chúng.
2. Xác định thứ tự triển khai (cái gì cần xây dựng trước làm nền tảng).
3. Đánh giá các rủi ro và phương án xử lý tương ứng.
4. Xác định những phần có thể làm song song và những phần phải làm tuần tự.
5. Thiết lập các chốt xác minh (checkpoints) giữa các giai đoạn.

Bản kế hoạch cần rõ ràng để con người có thể đọc hiểu và đưa ra phản hồi: "đúng hướng rồi" hoặc "cần thay đổi phần X".

### Giai đoạn 3: Tạo Task (Tasks)

Chia nhỏ kế hoạch thành các task cụ thể, độc lập:

- Mỗi task có thể hoàn thành trong một phiên làm việc tập trung (single session).
- Mỗi task phải có tiêu chí nghiệm thu rõ ràng.
- Mỗi task phải đi kèm bước xác minh (chạy test, build, hoặc kiểm tra thủ công).
- Sắp xếp các task theo thứ tự phụ thuộc logic, không sắp xếp theo độ ưu tiên cảm tính.
- Không có task nào yêu cầu sửa đổi quá ~5 files cùng lúc.

**Template mẫu cho Task:**
```markdown
- [ ] Task: [Mô tả ngắn gọn]
  - Nghiệm thu: [Điều gì phải đúng khi hoàn thành]
  - Xác minh: [Cách kiểm chứng — lệnh chạy test, build hoặc các bước test thủ công]
  - Files thay đổi: [Các file sẽ được chỉnh sửa/tạo mới]
```

### Giai đoạn 4: Triển khai (Implement)

Thực hiện từng task một theo quy trình trong `.agents/skills/incremental-implementation/SKILL.md` (`incremental-implementation`) và `.agents/skills/test-driven-development/SKILL.md` (`test-driven-development`). Sử dụng `.agents/skills/context-engineering/SKILL.md` (`context-engineering`) để chỉ nạp các phần spec và code liên quan cho từng task, tránh làm quá tải context của AI.

## Cập nhật đặc tả liên tục (Keeping the Spec Alive)

Đặc tả là một tài liệu sống, không phải tài liệu viết xong rồi bỏ đó:

- **Cập nhật khi quyết định thay đổi** — Nếu phát hiện cấu trúc dữ liệu cần đổi trong lúc code, hãy cập nhật spec trước, sau đó mới sửa code.
- **Cập nhật khi thay đổi phạm vi** — Các tính năng được thêm vào hoặc cắt bớt phải được cập nhật vào spec.
- **Commit file spec** — File spec cần được lưu trữ trong Git chung với mã nguồn.
- **Tham chiếu spec trong các commit/PR** — Liên kết tới phần spec tương ứng mà commit đó thực hiện.

## Các biện hộ thường gặp (Common Rationalizations)

| Tự biện hộ | Thực tế |
|---|---|
| "Việc này đơn giản, không cần spec" | Việc đơn giản không cần spec *dài*, nhưng vẫn cần tiêu chí nghiệm thu rõ ràng. Một bản spec dài 2 dòng vẫn tốt hơn không có gì. |
| "Tôi sẽ viết spec sau khi code xong" | Đó là viết tài liệu hướng dẫn (documentation), không phải đặc tả (specification). Giá trị của spec nằm ở việc giúp bạn làm rõ tư duy *trước khi* viết code. |
| "Spec làm chúng ta chậm đi" | Dành 15 phút viết spec giúp tiết kiệm hàng giờ code lỗi phải sửa lại. Thiết kế kỹ lưỡng trong 15 phút luôn nhanh hơn mò lỗi trong 15 giờ. |
| "Yêu cầu đằng nào cũng thay đổi mà" | Đó là lý do tại sao spec là tài liệu sống. Một bản spec được cập nhật liên tục vẫn tốt hơn nhiều việc code không có định hướng. |
| "Khách hàng đã biết rõ họ muốn gì rồi" | Ngay cả các yêu cầu có vẻ rõ ràng nhất vẫn chứa các giả định ngầm. Spec giúp lôi các giả định đó ra ánh sáng. |

## Dấu hiệu cảnh báo (Red Flags)

- Bắt đầu viết code mà chưa có bất kỳ yêu cầu nào được viết ra giấy.
- Hỏi "tôi có thể bắt đầu code luôn được chưa?" trước khi làm rõ thế nào là hoàn thành (done).
- Triển khai các tính năng không được đề cập trong bất kỳ tài liệu spec hay danh sách task nào.
- Đưa ra các quyết định kiến trúc lớn mà không tài liệu hóa lại.
- Bỏ qua bước viết spec vì nghĩ "tính năng này quá hiển nhiên".

## Xác minh (Verification)

Trước khi chuyển sang bước triển khai, hãy đảm bảo:

- [ ] Tài liệu đặc tả đã bao phủ đủ 6 phần cốt lõi.
- [ ] Con người đã xem và phê duyệt bản đặc tả này.
- [ ] Tiêu chí thành công cụ thể và có thể kiểm chứng được.
- [ ] Các giới hạn (Luôn làm / Hỏi trước / Không bao giờ) đã được định nghĩa rõ ràng.
- [ ] File spec đã được lưu vào repository.
