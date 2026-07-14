# Spec: Sổ lệnh cơ sở

## Mục tiêu

Xây dựng màn “Sổ lệnh” cho giao dịch cơ sở trong hệ thống mô phỏng. Người dùng đã đăng nhập demo có thể xem toàn bộ lệnh cơ sở đã đặt, trạng thái khớp/hủy, tổng giá trị khớp và thao tác sửa/hủy với lệnh còn chờ khớp.

## Phạm vi

- Hiển thị từ navbar cạnh “Bảng giá”.
- Dùng dữ liệu hiện có từ `GET /api/orders`.
- Chưa cần filter, export, chọn nhiều, tổng hợp nâng cao.
- Chưa triển khai “Sổ lệnh điều kiện”; tab/nhãn điều kiện có thể chưa hoạt động.
- Chỉ lệnh trạng thái `New` được sửa/hủy.
- Sửa lệnh mở panel đặt lệnh hiện có và điền sẵn mã, loại lệnh, giá, khối lượng.

## UI/UX

- Màn hình full-page thay thế bảng giá giống “Danh mục nắm giữ”.
- Design bám hệ màu tím tối hiện tại.
- Bảng dùng AG Grid để đồng bộ trải nghiệm bảng dữ liệu.
- Cột chính:
  - Mã CK
  - Mua/Bán
  - Loại
  - KL đặt
  - Giá đặt
  - KL khớp
  - Giá khớp TB
  - KL chờ khớp
  - Giá trị khớp
  - Trạng thái
  - Thời gian đặt
  - Thời gian cập nhật
  - Kênh
  - Sửa/Hủy

## Nghiệp vụ

- `Filled` hiển thị “Đã khớp”.
- `New` hiển thị “Chờ khớp”.
- `Cancelled` hiển thị “Đã hủy”.
- `Rejected` hiển thị “Từ chối”.
- Giá trị khớp = tổng `grossAmount` của executions; nếu không có executions thì dùng `filledQuantity * averageFillPrice`.
- KL chờ khớp = `quantity - filledQuantity` khi lệnh còn `New`, ngược lại là `0`.
- Hủy lệnh gọi `POST /api/orders/{id}/cancel`, sau đó refresh orders/portfolio/holdings.

## Kiểm thử

- Render được màn sổ lệnh từ navbar sau khi đăng nhập demo.
- Hiển thị đúng cột/trạng thái/tổng hợp cơ bản.
- Hủy lệnh `New` gọi đúng API và cập nhật lại danh sách.
- Sửa lệnh `New` mở ticket đặt lệnh với thông tin được điền sẵn.
