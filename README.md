# Business Tour — Five Realms

Mod thử nghiệm cho **Business Tour - Online Multiplayer Board Game** (Steam app `397900`, Unity IL2CPP).

## Có gì trong bản này

- Giữ kết nối Photon khi game chạy nền và ngăn bộ đếm AFK mặc định đá người chơi sau 3 lượt bị bỏ qua bắt buộc.
- Thêm map chọn riêng: **Five Realms — 5 Players**.
- Chỉ map Five Realms mới chuyển giới hạn phòng sang 5 người. Các map gốc vẫn giữ 4 người.
- Five Realms chia vòng 32 ô thành 5 khu vực có giao diện ngẫu nhiên, đồng bộ bằng seed lấy từ tên phòng, và đặt một bẫy Rogue Trap trong mỗi khu vực khi có ô phù hợp.
- Khi vào trận, riêng map này đổi vòng ô vuông thành **ngũ giác nhọn dạng kim cương với 5 đỉnh thật**; mỗi đỉnh mở đầu một cạnh/khu vực. Luật di chuyển vẫn dùng thứ tự cell gốc để giữ đồng bộ mạng.
- Mở rộng các mảng slot lobby/versus từ 4 lên 5 khi map Five Realms đang hoạt động.

## Giới hạn cần nói rõ

Đây là mod client cho game online, không phải thay đổi máy chủ của Business Tour. Nó đã được khóa theo Steam build `24336877` / Unity `6000.0.58f2`, nhưng không thể bảo đảm backend hiện tại cho phép người thứ 5 trước khi thử thật với 5 tài khoản. Tất cả người trong phòng Five Realms phải cài cùng phiên bản mod; client vanilla không hiểu dấu nhận dạng map và có thể không vào được phòng.

Game hiện chỉ khai báo ba họ giao diện ô (`Classic`, `Fantasy`, `Wonderland`). Vì vậy năm khu vực được xáo trộn từ ba họ này và có thể lặp, không phải năm bộ asset hoàn toàn độc lập. Hình học hiển thị được chuyển thành ngũ giác, nhưng graph luật chơi vẫn là vòng 32 ô gốc; đây là chủ ý để client không gửi một loại đường đi mới mà server không hiểu.

Không dùng map này cho xếp hạng, giải đấu hoặc phòng cược cho tới khi đã test đủ. Bản vá không thể chống việc máy chủ chủ động đóng phòng, bảo trì hoặc kick quản trị; nó chỉ xử lý AFK phía client và heartbeat khi chạy nền.

## Trạng thái kiểm thử hiện tại

- Đã kiểm tra trực tiếp qua Steam: BepInEx và plugin nạp thành công, map được chèn vào collection gốc, game đi tới main menu và UI tiếp tục phản hồi mà không có exception.
- Đã kiểm tra bộ cài mới, cài đè/nâng cấp và trình gỡ trên bản sao game cách ly; plugin khác và phần BepInEx dùng chung được giữ lại.
- Chưa gọi bản này là ổn định: vẫn cần mở màn chọn map, chạy một trận thật và kiểm tra với 5 tài khoản độc lập. Log khởi động không thể chứng minh backend chấp nhận người chơi thứ 5.

Lần mở đầu sau khi cài có thể lâu hơn bình thường vì BepInEx phải tạo cache IL2CPP. Nếu màn hình vẫn trống quá lâu sau khi tiêu đề cửa sổ đã trở lại `BusinessTour`, hãy đóng game và lấy `BepInEx\LogOutput.log` để chẩn đoán; không nên chờ vô hạn.

## Cài đặt cho người chơi

1. Thoát Business Tour trên mọi máy.
2. Giải nén gói phát hành.
3. Chạy `BusinessTourFiveRealms-Setup.exe`.
4. Kiểm tra thư mục game được nhận diện, rồi chọn **Install / Cài**.
5. Mỗi người chơi cài cùng gói, mở game bình thường qua Steam và chọn map **Five Realms — 5 Players** khi tạo phòng.

Gỡ bằng `BusinessTourFiveRealms-Uninstall.exe`. Trình gỡ chỉ xóa plugin của dự án; nó giữ BepInEx để không làm hỏng mod khác.

## Build

Yêu cầu bản Business Tour tương thích đã cài và một bản BepInEx IL2CPP x64 trong cache dự án. Chạy:

```powershell
pwsh -NoProfile -File .\tools\Build-Release.ps1
```

Script build tạo DLL, kiểm tra hash game, đóng gói loader + plugin, rồi tạo bộ cài WinForms giống quy trình của Far Far West mod. Thư mục Steam thật không bị ghi trong quá trình build/test.

## Test bắt buộc trước khi gọi là ổn định

- 5 máy cùng build game/mod, vào bằng mã phòng và Steam Invite.
- Mỗi người hoàn thành ít nhất một lượt; kiểm tra mua đất, bẫy, phá sản, kết thúc game.
- Alt-Tab lâu hơn thời gian từng gây rớt; thử ba lượt timeout liên tiếp.
- Host rời/reconnect và chuyển host.
- Chọn lại map gốc, xác nhận phòng quay về tối đa 4 người.

