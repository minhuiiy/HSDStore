# CPTStore - Hệ thống quản lý cửa hàng trực tuyến

## Thông tin đăng nhập mặc định

### Tài khoản Admin
- **Email**: admin@cptstore.com
- **Mật khẩu**: Admin@123

## Cấu trúc phân quyền

Hệ thống sử dụng ASP.NET Core Identity để quản lý người dùng và phân quyền. Có hai vai trò chính:

1. **Admin**: Có quyền truy cập vào khu vực quản trị và thực hiện tất cả các chức năng quản lý.
2. **User**: Người dùng thông thường, có thể mua hàng và quản lý tài khoản cá nhân.

## Khởi tạo dữ liệu

Khi ứng dụng khởi động lần đầu, hệ thống sẽ tự động:
- Tạo các vai trò Admin và User nếu chưa tồn tại
- Tạo tài khoản admin mặc định nếu chưa tồn tại

Quy trình này được thực hiện bởi lớp `DbInitializer` trong namespace `CPTStore.Data`.

## Đăng ký người dùng mới

Khi người dùng đăng ký tài khoản mới, họ sẽ tự động được gán vai trò User.

## Quản lý người dùng

Quản trị viên có thể quản lý người dùng thông qua khu vực Admin:
- Xem danh sách người dùng
- Chỉnh sửa thông tin người dùng
- Gán hoặc thu hồi vai trò
- Kích hoạt hoặc vô hiệu hóa tài khoản

## Lưu ý bảo mật

- Mật khẩu admin mặc định chỉ nên được sử dụng trong môi trường phát triển
- Trong môi trường sản xuất, hãy đổi mật khẩu admin ngay sau khi triển khai
- Đảm bảo cấu hình Identity với các chính sách mật khẩu mạnh