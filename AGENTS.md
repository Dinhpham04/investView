# AGENTS.md

Tài liệu này cung cấp hướng dẫn cho các AI coding agents (Claude Code, Cursor, Copilot, Antigravity,...) khi làm việc với mã nguồn trong repository này.

## Tổng quan Repository

Một tập hợp các kỹ năng (skills) dành cho lập trình viên C# .NET. Các kỹ năng là các hướng dẫn và quy trình được đóng gói sẵn nhằm mở rộng khả năng của AI khi phát triển các ứng dụng desktop hoặc hệ thống trong môi trường Windows.

## Tích hợp Codex

Codex sử dụng một **mô hình thực thi theo định hướng kỹ năng (skill-driven execution model)** được điều khiển bởi thư mục `/skills` của repository này.

### Quy tắc cốt lõi

- Nếu một nhiệm vụ khớp với một kỹ năng (skill), bạn BẮT BUỘC phải kích hoạt nó.
- Các kỹ năng được lưu trữ tại `.agents/skills/<skill-name>/SKILL.md`.
- Tuyệt đối không tự ý thực hiện trực tiếp nếu có một skill phù hợp đang tồn tại.
- Luôn tuân thủ chính xác hướng dẫn của skill (không áp dụng nửa vời).

### Ánh xạ Ý định → Kỹ năng (Intent → Skill Mapping)

Agent sẽ tự động ánh xạ ý định của người dùng tới các kỹ năng tương ứng:

- Phỏng vấn / Làm rõ yêu cầu → `interview-me`
- Tinh chỉnh ý tưởng / Thiết kế khái niệm → `idea-refine`
- Tính năng / Chức năng mới → `spec-driven-development`, sau đó đến `incremental-implementation`, `test-driven-development`
- Lập kế hoạch / Chia nhỏ task → `planning-and-task-breakdown`
- Bug / Lỗi / Hành vi bất thường → `debugging-and-error-recovery`
- Review code → `code-review-and-quality`
- Refactoring / Đơn giản hóa code → `code-simplification`
- Vừa làm vừa học / Giải thích lý thuyết, cơ chế, tradeoff, phỏng vấn → `learning-coach`

### Ánh xạ Vòng đời (Lifecycle Mapping)

Agent phải tuân thủ quy trình vòng đời nội bộ này:

- DEFINE (Định nghĩa) → `spec-driven-development`
- PLAN (Lập kế hoạch) → `planning-and-task-breakdown`
- BUILD (Xây dựng) → `incremental-implementation` + `test-driven-development`
- VERIFY (Xác minh) → `debugging-and-error-recovery`
- REVIEW (Đánh giá) → `code-review-and-quality` + `code-simplification`

### Mô hình Thực thi (Execution Model)

Đối với mỗi yêu cầu:

1. Xác định xem có skill nào phù hợp hay không.
2. Kích hoạt skill phù hợp.
3. Tuân thủ nghiêm ngặt quy trình của skill.
4. Chỉ tiến hành code sau khi các bước bắt buộc (spec, lập kế hoạch...) hoàn tất.

### Chống tự biện hộ (Anti-Rationalization)

Những suy nghĩ sau đây là sai lầm và cần phải bỏ qua:

- "Việc này quá nhỏ, không cần dùng skill."
- "Mình có thể tự code nhanh tính năng này."
- "Mình sẽ tìm hiểu ngữ cảnh trước rồi tính."

Hành vi đúng:

- Luôn kiểm tra và ưu tiên sử dụng các skills trước.

---

## Phối hợp: Personas, Skills, và Commands

Repository này có hai lớp đang hoạt động:

- **Skills** (`.agents/skills/<name>/SKILL.md`) — các quy trình làm việc kèm các bước và tiêu chí hoàn thành. Trả lời cho câu hỏi *Làm thế nào (How)*.
- **Tài liệu & Tham chiếu (Documentation & References)** — các quyết định kiến trúc và hướng dẫn kỹ thuật.

---

## Tạo một Skill mới

### Cấu trúc thư mục

```
.agents/
  skills/
  {skill-name}/           # Tên thư mục dạng kebab-case
    SKILL.md              # Bắt buộc: Định nghĩa skill
```

### Định dạng file SKILL.md

```markdown
---
name: {skill-name}
description: {Một câu mô tả chức năng của skill, kèm theo các điều kiện kích hoạt "Sử dụng khi...".}
---

# {Tiêu đề Skill}

{Mô tả ngắn gọn về chức năng của skill và tầm quan trọng của nó.}

## Cách hoạt động (How It Works)

{Danh sách các bước giải thích quy trình thực hiện của skill}

## Xác minh (Verification)

{Checklist để kiểm tra xem nhiệm vụ đã hoàn thành chưa}
```

### Hướng dẫn tối ưu hóa Context

Để giảm thiểu lượng token tiêu thụ:

- **Giữ file SKILL.md dưới 500 dòng** — đưa các tài liệu tham khảo chi tiết vào các file riêng.
- **Viết phần mô tả (description) cụ thể** — giúp AI nhận diện chính xác khi nào cần kích hoạt skill.
- **Sử dụng cơ chế tiết lộ dần (progressive disclosure)** — liên kết tới các file hỗ trợ để chỉ đọc khi thực sự cần thiết.
