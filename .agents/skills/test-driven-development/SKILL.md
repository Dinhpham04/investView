---
name: test-driven-development
description: Drives development with tests. Use when implementing any logic, fixing any bug, or changing any behavior. Use when you need to prove that code works, when a bug report arrives, or when you're about to modify existing functionality.
---

# Phát triển hướng kiểm thử (Test-Driven Development - C# .NET)

## Tổng quan

Viết một bài test lỗi trước khi viết phần code thực tế giúp test đó chạy đúng (xanh). Đối với việc sửa lỗi (bug fixes), hãy tái hiện lỗi bằng một bài test trước khi cố gắng sửa nó. Các bài test là bằng chứng ngoại phạm rõ ràng nhất — "trông có vẻ chạy đúng" nghĩa là chưa xong. Một codebase có hệ thống test tốt là siêu năng lực của AI Agent; một codebase không có test là một gánh nặng rủi ro lớn.

## Khi nào sử dụng

- Triển khai bất kỳ logic hay hành vi mới nào (ví dụ: logic lọc file của bộ quét).
- Sửa bất kỳ lỗi nào (Quy trình Prove-It).
- Sửa đổi các chức năng hiện có của hệ thống.
- Xử lý các trường hợp biên (edge cases).
- Bất kỳ thay đổi nào có khả năng làm ảnh hưởng đến các tính năng đang chạy bình thường.

**Khi KHÔNG sử dụng:** Chỉ thay đổi cấu hình đơn thuần, cập nhật tài liệu hướng dẫn hoặc thay đổi tài nguyên tĩnh không ảnh hưởng đến logic xử lý.

## Chu kỳ TDD (The TDD Cycle)

```
    RED (ĐỎ)            GREEN (XANH)          REFACTOR (TỐI ƯU)
 Viết một test      Viết lượng code tối    Tối ưu hóa cấu trúc
  chạy bị lỗi   ──→  thiểu để test pass  ──→    nhưng test vẫn   ──→ (lặp lại)
       │                    │                     chạy xanh
       ▼                    ▼                         │
   Test FAILS          Test PASSES                    ▼
                                              Test vẫn PASSES
```

### Bước 1: RED — Viết một test lỗi

Viết test trước. Test này bắt buộc phải chạy lỗi (fail). Một bài test vừa viết xong đã chạy đúng (pass) ngay lập tức không chứng minh được điều gì cả.

```csharp
// RED: Test này sẽ bị báo lỗi vì class OrphanDetector hoặc phương thức FindOrphans chưa được viết
[Fact]
public void FindOrphans_ShouldReturnOrphanDirectories()
{
    var installedApps = new List<string> { "Git", "VSCode" };
    var detectedFolders = new List<string> { "Git", "AdobePhotoshop" };
    var detector = new OrphanDetector();

    var orphans = detector.FindOrphans(detectedFolders, installedApps);

    Assert.Single(orphans);
    Assert.Equal("AdobePhotoshop", orphans.First());
}
```

### Bước 2: GREEN — Làm cho test chạy đúng

Viết lượng code tối thiểu nhất để vượt qua bài test. Đừng vội vàng tối ưu hay viết các kiến trúc phức tạp ở bước này:

```csharp
// GREEN: Triển khai tối giản nhất
public class OrphanDetector
{
    public IEnumerable<string> FindOrphans(IEnumerable<string> detectedFolders, IEnumerable<string> installedApps)
    {
        var installedSet = new HashSet<string>(installedApps, StringComparer.OrdinalIgnoreCase);
        return detectedFolders.Where(folder => !installedSet.Contains(folder));
    }
}
```

### Bước 3: REFACTOR — Tối giản và làm sạch code

Khi các bài test đã chạy xanh (pass), hãy cải tiến code mà không làm thay đổi hành vi của nó:

- Tách các đoạn logic dùng chung.
- Đặt lại tên biến/hàm cho rõ nghĩa hơn.
- Loại bỏ các đoạn code lặp lại.
- Tối ưu hóa hiệu năng nếu cần thiết.

Chạy lệnh `dotnet test` sau mỗi bước tối ưu để đảm bảo không làm hỏng tính năng sẵn có.

## Quy trình chứng minh lỗi (The Prove-It Pattern cho Bug Fixes)

Khi nhận được báo cáo lỗi, **tuyệt đối không bắt tay vào sửa code ngay.** Hãy bắt đầu bằng việc viết một bài test tái hiện lại chính xác lỗi đó.

```
Nhận báo cáo lỗi
       │
       ▼
  Viết một test để tái hiện lỗi đó
       │
       ▼
  Chạy test BỊ LỖI (xác nhận lỗi thực sự tồn tại)
       │
       ▼
  Viết code sửa lỗi
       │
       ▼
  Chạy test THÀNH CÔNG (chứng minh lỗi đã được sửa)
       │
       ▼
  Chạy lại toàn bộ test suite (đảm bảo không phát sinh lỗi mới)
```

**Ví dụ thực tế:**

```csharp
// Lỗi: "Bộ quét bị crash khi duyệt qua một thư mục hệ thống bị khóa"

// Bước 1: Viết test tái hiện lỗi (Sẽ FAIL với lỗi UnauthorizedAccessException)
[Fact]
public void Scan_ShouldIgnoreInaccessibleFolders_AndNotThrow()
{
    var mockFS = new MockFileSystem();
    mockFS.AddDirectory(@"C:\System Volume Information");
    // Giả lập quyền truy cập thư mục này sẽ quăng ra lỗi UnauthorizedAccessException khi đọc...
    
    var scanner = new FolderScanner(mockFS);
    
    var ex = Record.Exception(() => scanner.Scan(@"C:\System Volume Information"));
    Assert.Null(ex); // Test này sẽ fail -> Xác nhận bug tồn tại
}

// Bước 2: Tiến hành sửa lỗi
public IEnumerable<string> Scan(string path)
{
    try 
    {
        return _fileSystem.Directory.EnumerateDirectories(path);
    }
    catch (UnauthorizedAccessException)
    {
        return Enumerable.Empty<string>(); // Bắt exception và bỏ qua thư mục bị khóa
    }
}

// Bước 3: Chạy lại test thành công -> Lỗi đã được sửa và có chốt chặn phòng ngừa lỗi lặp lại
```

## Kim tự tháp kiểm thử (The Test Pyramid)

Phân bổ công sức viết test theo mô hình kim tự tháp — phần lớn các bài test nên là các test nhỏ và chạy nhanh, số lượng test tích hợp và test hệ thống sẽ ít hơn ở các tầng trên:

```
          ╱╲
         ╱  ╲         System/E2E Tests (~5%)
        ╱    ╲        Chạy trên hệ thống thật, kiểm chứng luồng xóa file thật
       ╱──────╲
      ╱        ╲      Integration Tests (~15%)
     ╱          ╲     Tương tác giữa Registry Scanner và MockFileSystem
    ╱────────────╲
   ╱              ╲   Unit Tests (~80%)
  ╱                ╲  Logic thuần túy (bộ lọc, tính dung lượng), chạy cực nhanh (ms)
 ╱──────────────────╲
```

**Quy tắc Beyoncé (The Beyonce Rule):** Nếu bạn thực sự coi trọng đoạn code đó, hãy viết test cho nó. Các thay đổi về hạ tầng, refactor hay migrate không có nhiệm vụ đi tìm lỗi hộ bạn — hệ thống test của bạn mới có nhiệm vụ đó. Nếu một thay đổi làm hỏng code mà bạn không có test để phát hiện, đó là lỗi của bạn.

### Phân cỡ Test (Resource Model)

Phân loại các bài test dựa trên nguồn tài nguyên hệ thống mà chúng tiêu thụ:

| Kích cỡ | Giới hạn tài nguyên | Tốc độ | Ví dụ thực tế |
|------|------------|-------|---------|
| **Small (Nhỏ)** | Chạy đơn tiến trình, không có I/O, không ghi file thật, không Registry thật | Vài mili-giây | Test các hàm lọc Regex, tính toán dung lượng từ dữ liệu sẵn có |
| **Medium (Vừa)** | Cho phép đa tiến trình, sử dụng MockFileSystem, MockRegistry | Vài giây | Kiểm thử logic tìm thư mục rác với thư viện System.IO.Abstractions |
| **Large (Lớn)** | Tương tác thực tế với File System Windows, Registry Windows thật | Vài phút | Chạy ứng dụng thực tế và quét trên một thư mục tạm của hệ thống |

Các bài test nhỏ (Small) nên chiếm đại đa số trong dự án của bạn. Chúng chạy nhanh, ổn định và cực kỳ dễ debug khi xảy ra lỗi.

### Hướng dẫn lựa chọn loại test

```
Đoạn code chỉ chứa logic xử lý dữ liệu và không có tác dụng phụ (side effects)?
  → Unit test (Small)

Đoạn code có tương tác với Registry, File System, hoặc API?
  → Integration test (Medium) sử dụng System.IO.Abstractions để giả lập

Đoạn code là một luồng tính năng lớn cần chạy thực tế trên Windows?
  → E2E test (Large) — chỉ viết cho các luồng quan trọng nhất và chạy trên thư mục tạm an toàn
```

## Cách viết test tốt

### Kiểm tra Trạng thái, Không kiểm tra Tương tác (State over Interaction)

Hãy kiểm tra **kết quả đầu ra (outcome)** của một hành động, đừng kiểm tra xem bên trong nó đã gọi những phương thức nào. Việc kiểm tra các bước gọi hàm nội bộ (interaction-based) sẽ khiến test của bạn bị vỡ ngay khi bạn refactor code, mặc dù hành vi của tính năng không hề thay đổi.

```csharp
// Tốt: Kiểm tra kết quả thực tế (State-based)
[Fact]
public void FindOrphans_ShouldReturnUninstalledApps()
{
    var detector = new OrphanDetector();
    var result = detector.FindOrphans(new[] { "AppA" }, new[] { "AppB" });
    Assert.Contains("AppA", result);
}

// Tệ: Kiểm tra xem bên trong hàm có gọi hàm khác không (Interaction-based)
[Fact]
public void FindOrphans_ShouldCallRegistryService()
{
    var mockRegistry = new Mock<IRegistryService>();
    var detector = new OrphanDetector(mockRegistry.Object);
    detector.FindOrphans(new[] { "AppA" }, new string[0]);
    mockRegistry.Verify(r => r.GetInstalledApps(), Times.Once()); // Test này dễ bị vỡ khi đổi logic nạp data
}
```

### Ưu tiên tính Rõ ràng hơn tính Tối giản trong Test (DAMP over DRY)

Trong mã nguồn sản phẩm, DRY (Don't Repeat Yourself - Đừng viết lặp lại) là quy tắc vàng. Nhưng trong test, **DAMP (Descriptive And Meaningful Phrases - Ưu tiên tính mô tả và rõ nghĩa)** được ưu tiên hơn. Một hàm test nên đọc giống như một bản mô tả yêu cầu — người đọc có thể hiểu ngay câu chuyện của bài test mà không cần phải đi tra cứu các hàm helper thiết lập dùng chung phức tạp.

```csharp
// DAMP: Mỗi hàm test độc lập, rõ ràng và dễ đọc hiểu
[Fact]
public void Scan_ShouldFilterSystemFolders()
{
    var folder = @"C:\Windows";
    var scanner = new FolderScanner();
    var result = scanner.IsSystemFolder(folder);
    Assert.True(result);
}

[Fact]
public void Scan_ShouldNotFilterUserFolders()
{
    var folder = @"C:\Users\User\AppData";
    var scanner = new FolderScanner();
    var result = scanner.IsSystemFolder(folder);
    Assert.False(result);
}
```

Việc trùng lặp code trong các hàm test là hoàn toàn chấp nhận được nếu nó giúp bài test độc lập và dễ hiểu hơn.

### Ưu tiên sử dụng Code thực tế hơn Mock (Prefer Real Implementations)

Hãy sử dụng các kỹ thuật giả lập theo thứ tự ưu tiên từ cao xuống thấp. Càng sử dụng nhiều code thực tế, bài test càng mang lại độ tin cậy cao:

```
Thứ tự ưu tiên (Từ trên xuống dưới):
1. Real implementation  → Dùng code thật (Độ tin cậy cao nhất, bắt được nhiều bug thật)
2. Fake                 → Dùng bản in-memory (ví dụ: MockFileSystem của System.IO.Abstractions)
3. Stub                 → Trả về dữ liệu cứng được chuẩn bị trước
4. Mock (interaction)   → Giả lập và kiểm tra các cuộc gọi hàm (nên hạn chế dùng)
```

**Chỉ dùng mock khi:** code thật chạy quá chậm, không ổn định hoặc có các tác động không mong muốn ra bên ngoài (ví dụ: xóa file thật trên ổ đĩa). Lạm dụng mock sẽ tạo ra các bài test chạy xanh trong khi ứng dụng chạy thực tế bị lỗi.

### Sử dụng mô hình AAA (Arrange-Act-Assert)

```csharp
[Fact]
public void SizeCalculator_ShouldSumFiles()
{
    // Arrange (Thiết lập): Chuẩn bị môi trường test giả lập
    var mockFS = new MockFileSystem(new Dictionary<string, MockFileData> {
        { @"C:\file1.txt", new MockFileData("123") },
        { @"C:\file2.txt", new MockFileData("12345") }
    });
    var calculator = new SizeCalculator(mockFS);

    // Act (Hành động): Thực thi phương thức cần test
    var size = calculator.GetTotalSize(@"C:\");

    // Assert (Xác minh): Kiểm tra kết quả đầu ra
    Assert.Equal(8, size);
}
```

### Một concept kiểm tra cho mỗi hàm test (One Assertion Per Concept)

```csharp
// Tốt: Mỗi hàm test kiểm tra một hành vi duy nhất
[Fact] public void RejectsEmptyPaths() { ... }
[Fact] public void RejectsInvalidCharacters() { ... }

// Tệ: Nhồi nhét mọi thứ vào một test duy nhất
[Fact]
public void ValidatesPathsCorrectly()
{
    Assert.Throws<ArgumentException>(() => new PathValidator(""));
    Assert.Throws<ArgumentException>(() => new PathValidator("C:|invalid"));
    Assert.NotNull(new PathValidator(@"C:\Valid"));
}
```

### Đặt tên test có tính mô tả (Name Tests Descriptively)

```
// Tốt: Tên test đọc giống như một bản đặc tả yêu cầu
[Fact] public void Scan_WhenAccessDenied_ShouldLogWarningAndContinue() { ... }
[Fact] public void Scan_WhenDirectoryDoesNotExist_ShouldThrowDirectoryNotFound() { ... }

// Tệ: Tên mơ hồ
[Fact] public void Test1() { ... }
[Fact] public void TestWorks() { ... }
```

## Các phản mẫu test cần tránh (Test Anti-Patterns)

| Phản mẫu (Anti-Pattern) | Vấn đề phát sinh | Cách khắc phục |
|---|---|---|
| Test chi tiết triển khai bên trong | Test bị vỡ khi refactor mặc dù tính năng vẫn chạy đúng | Chỉ test đầu vào và đầu ra, không test cấu trúc bên trong |
| Test không ổn định (flaky tests) | Làm mất lòng tin vào bộ test | Viết các assert có tính nhất quán, dùng MockFileSystem |
| Viết test cho framework | Phí thời gian test các hàm có sẵn của .NET | Chỉ viết test cho logic do chính bạn viết ra |
| Không cách ly môi trường | Các test chạy riêng lẻ thì đúng nhưng chạy chung thì lỗi | Đảm bảo mỗi test tự tạo và tự giải phóng MockFileSystem riêng |
| Mock mọi thứ | Test chạy xanh lá nhưng app thật chạy lỗi | Ưu tiên dùng code thật > fake > stub > mock. Chỉ mock ở ranh giới hệ thống bên ngoài |

## Khi nào cần dùng Subagent để viết test

Đối với các bug phức tạp, hãy giao cho một subagent viết test tái hiện lỗi trước:

```
Agent chính: "Hãy tạo một subagent viết một test tái hiện lỗi này: [mô tả lỗi].
Test này phải chạy lỗi (fail) với code hiện tại."

Subagent: Tiến hành viết bài test tái hiện lỗi.

Agent chính: Xác nhận bài test đó chạy lỗi thực sự, sau đó tiến hành sửa code,
rồi chạy lại test để xác nhận nó đã chuyển sang xanh (pass).
```

Việc này giúp bài test được viết một cách khách quan, không bị ảnh hưởng bởi logic sửa lỗi của bạn.

## Các biện hộ thường gặp (Common Rationalizations)

| Tự biện hộ | Thực tế |
|---|---|
| "Tôi sẽ viết test sau khi code chạy được" | Bạn sẽ không viết. Và viết test sau khi xong code thường có xu hướng test xem code đang chạy thế nào chứ không test hành vi đúng của nó. |
| "Logic này quá đơn giản, không cần test" | Code đơn giản sẽ dần trở nên phức tạp. Bài test đóng vai trò là tài liệu mô tả hành vi chuẩn của nó. |
| "Test làm tôi chậm lại" | Test làm bạn chậm lại lúc này nhưng giúp bạn đi cực nhanh và tự tin khi thay đổi code sau này. |
| "Tôi đã tự test bằng tay rồi" | Test bằng tay không có tính kế thừa. Thay đổi ngày mai có thể làm hỏng tính năng hôm nay mà bạn không có cách nào biết. |

## Dấu hiệu cảnh báo (Red Flags)

- Viết các hàm logic nghiệp vụ hệ thống mà không có bất kỳ unit test nào đi kèm.
- Test chạy thành công ngay lần đầu tiên chạy (có thể assert chưa đúng hoặc chưa thực sự quét qua code).
- Sửa bug mà không có bài test tái hiện lỗi đi kèm.
- Đặt tên test mơ hồ không rõ hành vi được kiểm chứng.
- Tắt hoặc bỏ qua (skip) các bài test lỗi để bộ test chạy xanh sạch.

## Xác minh (Verification)

Sau khi hoàn thành code:

- [ ] Mọi logic nghiệp vụ mới đều có bài test tương ứng bảo vệ.
- [ ] Toàn bộ bộ test chạy thành công: `dotnet test`.
- [ ] Việc sửa bug đi kèm bài test tái hiện lỗi (đã chạy fail trước khi sửa).
- [ ] Không có bài test nào bị tắt hoặc bỏ qua mà không có lý do cụ thể.
