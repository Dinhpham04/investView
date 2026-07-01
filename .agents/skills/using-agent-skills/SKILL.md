---
name: using-agent-skills
description: Discovers and invokes agent skills. Use when starting a session or when you need to discover which skill applies to the current task. This is the meta-skill that governs how all other skills are discovered and invoked.
---

# Sử dụng Kỹ năng của Agent (Using Agent Skills)

## Tổng quan

Kỹ năng của Agent (Agent Skills) là một tập hợp các quy trình kỹ thuật được sắp xếp theo các giai đoạn phát triển phần mềm. Mỗi kỹ năng đóng gói một quy trình cụ thể mà các kỹ sư có kinh nghiệm tuân thủ. Kỹ năng siêu cấp (meta-skill) này giúp bạn khám phá và áp dụng đúng kỹ năng cho công việc hiện tại. Tất cả các kỹ năng đều đã được tinh chỉnh cho việc phát triển C# .NET.

## Khám phá Kỹ năng (Skill Discovery)

Khi có một nhiệm vụ (task) được đưa vào, hãy xác định giai đoạn phát triển và áp dụng kỹ năng tương ứng:

```
Nhiệm vụ đến
    │
    ├── Chưa biết bạn thực sự muốn gì? ──────→ interview-me
    ├── Đã có ý tưởng thô sơ, cần làm rõ? ───→ idea-refine
    ├── Dự án mới/tính năng mới/thay đổi? ──→ spec-driven-development
    ├── Đã có spec, cần chia nhỏ task? ──────→ planning-and-task-breakdown
    ├── Đang viết code? ─────────────────────→ incremental-implementation
    │   ├── Cần bối cảnh (context) tốt hơn? ─→ context-engineering
    │   └── Cần code C# chuẩn tài liệu? ─────→ source-driven-development
    ├── Đang viết/chạy test C#? ─────────────→ test-driven-development
    ├── Gặp lỗi hoặc hành vi bất thường? ────→ debugging-and-error-recovery
    ├── Đang đánh giá (review) code? ────────→ code-review-and-quality
    │   └── Code quá phức tạp? ──────────────→ code-simplification
    ├── Đang commit hoặc phân nhánh (git)? ──→ git-workflow-and-versioning
    └── Đang viết tài liệu/ADRs? ────────────→ documentation-and-adrs
```

## Các Hành vi Vận hành Cốt lõi (Core Operating Behaviors)

Các hành vi này áp dụng tại mọi thời điểm, trên tất cả các kỹ năng. Đây là các điều khoản bắt buộc.

### 1. Nêu rõ các Giả định (Surface Assumptions)
Trước khi triển khai bất kỳ tính năng phức tạp nào, hãy nêu rõ các giả định của bạn:

```
ASSUMPTIONS I'M MAKING (CÁC GIẢ ĐỊNH CỦA TÔI):
1. Target .NET version là net8.0-windows
2. Thư viện giả lập hệ thống file là System.IO.Abstractions
3. Ứng dụng chạy dưới quyền người dùng hiện tại (không yêu cầu ghi Registry hệ thống HKLM)
→ Xác nhận lại với tôi hoặc tôi sẽ tiếp tục với các giả định này.
```

Đừng âm thầm tự quyết định các yêu cầu mơ hồ. Lỗi phổ biến nhất là tự ý giả định sai và triển khai theo nó. Làm rõ các điểm chưa chắc chắn sớm sẽ rẻ hơn nhiều so với việc phải sửa lại code sau này.

### 2. Chủ động Giải quyết sự Mơ hồ (Manage Confusion Actively)
Khi bạn gặp các yêu cầu mâu thuẫn, không nhất quán hoặc đặc tả kỹ thuật chưa rõ ràng:

1. **DỪNG LẠI.** Không tiếp tục code bằng cách đoán mò.
2. Nêu rõ điểm mơ hồ cụ thể.
3. Trình bày các phương án đánh đổi (trade-offs) hoặc đặt câu hỏi làm rõ.
4. Đợi câu trả lời trước khi tiếp tục.

**Tồi:** Tự ý chọn một cách hiểu và hy vọng nó đúng.
**Tốt:** "Tôi thấy phần Spec ghi X nhưng trong code hiện tại đang chạy Y. Cái nào được ưu tiên hơn?"

### 3. Đưa ra Phản biện khi cần thiết (Push Back When Warranted)
Bạn không phải là một chiếc máy chỉ biết "vâng lệnh". Khi một giải pháp có vấn đề rõ ràng:

- Chỉ ra vấn đề trực tiếp.
- Giải thích điểm bất lợi cụ thể (định lượng nếu có thể — ví dụ: "việc quét đồng bộ này sẽ chặn UI thread của WPF gây đóng băng app" thay vì "nó có vẻ hơi chậm").
- Đề xuất một giải pháp thay thế.
- Chấp nhận quyết định của con người nếu họ đã hiểu rõ thông tin và vẫn yêu cầu làm tiếp.

### 4. Ưu tiên sự Đơn giản (Enforce Simplicity)
Xu hướng tự nhiên của AI là viết code phức tạp hơn mức cần thiết. Hãy chủ động hạn chế điều này.

Trước khi hoàn thành một đoạn code, hãy tự hỏi:
- Có thể viết ngắn gọn hơn không?
- Các lớp trừu tượng (abstractions) này có thực sự cần thiết cho sự phức tạp của chúng không?
- Một kỹ sư giỏi nhìn vào có hỏi "sao không dùng cách đơn giản hơn..." không?

Ưu tiên giải pháp rõ ràng, dễ hiểu. Viết code quá phức tạp là một thất bại.

### 5. Kỷ luật về Phạm vi công việc (Maintain Scope Discipline)
Chỉ chỉnh sửa những gì bạn được yêu cầu.

TỰ Ý TRÁNH:
- Xóa các comment bạn không hiểu.
- "Dọn dẹp" code ở các khu vực không liên quan tới task.
- Refactor các hệ thống lân cận như một tác dụng phụ.
- Xóa code có vẻ không dùng đến mà không xin phép.
- Thêm tính năng ngoài spec vì nghĩ nó "có ích".

Công việc của bạn là can thiệp chính xác như một cuộc phẫu thuật, không phải cải tạo nhà tự phát.

### 6. Xác minh, Không đoán mò (Verify, Don't Assume)
Mỗi kỹ năng đều đi kèm bước xác minh (verification). Một task chưa hoàn thành nếu chưa vượt qua bước xác minh. "Trông có vẻ đúng" là chưa đủ — phải có bằng chứng rõ ràng (test pass, build thành công, hoặc dữ liệu chạy thực tế).

---

## Các Lỗi cần Tránh (Failure Modes to Avoid)

Đây là những lỗi nhỏ tạo cảm giác làm việc hiệu quả nhưng thực chất gây ra vấn đề lớn:

1. Đưa ra giả định sai mà không kiểm tra lại.
2. Không làm rõ sự mơ hồ — tiếp tục code khi đang bối rối.
3. Thấy điểm mâu thuẫn nhưng im lặng bỏ qua.
4. Không trình bày các lựa chọn đánh đổi trong quyết định kỹ thuật.
5. Luôn đồng ý ("Tất nhiên rồi!") với những phương án có vấn đề rõ ràng.
6. Làm phức tạp hóa code C# và các API.
7. Sửa đổi code hoặc comment không liên quan tới nhiệm vụ.
8. Xóa những thứ mình chưa hiểu rõ.
9. Code trực tiếp không có Spec vì nghĩ "nó quá rõ ràng".
10. Bỏ qua bước xác minh vì nghĩ "trông code đúng rồi".

## Quy trình Vòng đời (Lifecycle Sequence)

Đối với một tính năng hoàn chỉnh, quy trình sử dụng skill chuẩn là:

```
1.  interview-me                → Tìm hiểu xem người dùng thực sự muốn gì
2.  idea-refine                 → Tinh chỉnh các ý tưởng thô sơ
3.  spec-driven-development     → Định nghĩa những gì chúng ta sẽ xây dựng
4.  planning-and-task-breakdown → Chia nhỏ công việc thành các task có thể xác minh
5.  context-engineering         → Nạp đúng bối cảnh cần thiết
6.  source-driven-development   → Xác minh giải pháp dựa trên tài liệu chính thức
7.  incremental-implementation  → Xây dựng từng lát cắt nhỏ (slice)
8.  test-driven-development     → Chứng minh mỗi lát cắt hoạt động đúng (TDD)
9.  code-review-and-quality     → Đánh giá chất lượng trước khi merge
10. code-simplification         → Tối giản hóa code nhưng giữ nguyên hành vi
11. git-workflow-and-versioning → Tạo lịch sử commit sạch đẹp
12. documentation-and-adrs      → Ghi chép tài liệu và quyết định kiến trúc
```

Không phải nhiệm vụ nào cũng cần tất cả skills. Một lỗi nhỏ (bug fix) chỉ cần: `debugging-and-error-recovery` → `test-driven-development` → `code-review-and-quality`.

## Bảng tra cứu nhanh (Quick Reference)

| Giai đoạn | Kỹ năng | Tóm tắt một dòng |
|-------|-------|-----------------|
| Định nghĩa | interview-me | Khai thác chính xác mong muốn của người dùng trước khi lập plan hay viết code |
| Định nghĩa | idea-refine | Tinh chỉnh ý tưởng thông qua tư duy hội tụ và phân kỳ |
| Định nghĩa | spec-driven-development | Tạo yêu cầu và tiêu chí nghiệm thu trước khi bắt đầu code |
| Lập kế hoạch | planning-and-task-breakdown | Chia nhỏ công việc thành các task nhỏ dễ kiểm chứng |
| Xây dựng | incremental-implementation | Viết code theo từng lát cắt dọc, kiểm thử kỹ trước khi mở rộng |
| Xây dựng | source-driven-development | Đối chiếu với tài liệu chính thống trước khi code |
| Xây dựng | context-engineering | Nạp đúng và đủ bối cảnh tại đúng thời điểm |
| Xác minh | test-driven-development | Viết test lỗi trước, sau đó viết code để test pass |
| Xác minh | debugging-and-error-recovery | Tái hiện → Khoanh vùng → Sửa lỗi → Phòng ngừa |
| Đánh giá | code-review-and-quality | Đánh giá code trên nhiều khía cạnh trước khi hoàn thành |
| Đánh giá | code-simplification | Giữ nguyên hành vi nhưng loại bỏ các cấu trúc phức tạp thừa |
| Hoàn thiện | git-workflow-and-versioning | Tạo các commit nguyên tử, lịch sử Git sạch sẽ |
| Hoàn thiện | documentation-and-adrs | Tài liệu hóa lý do đưa ra quyết định kỹ thuật |
