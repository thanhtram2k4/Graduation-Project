# PHIẾU KIỂM SOÁT TIẾN ĐỘ LÀM ĐỒ ÁN TỐT NGHIỆP
(Phiếu dành cho người hướng dẫn/sinh viên)

**Họ tên sinh viên:** Nguyễn Hoàng Thanh Trâm              **Số thẻ SV:** 22020005
**Tên đề tài ĐATN:** Phát triển game 'Hào Khí Sử Việt': Trò chơi thủ thành 2D dựa trên văn hóa dân gian Việt Nam sử dụng Unity
**Họ tên người HD:** TS. Trần Thế Vũ                       **Đơn vị:** Khoa Công nghệ Thông tin

| Tuần | Ngày | Khối lượng đã thực hiện (%) | Khối lượng tiếp tục thực hiện (%) | GVHD ký tên |
| :---: | :---: | :--- | :--- | :---: |
| **1** | 30/3-5/4 | Phân tích yêu cầu chức năng, kỹ thuật và thiết kế luật chơi (Gameplay rules) cho game Hào Khí Sử Việt. Thiết lập cấu trúc dự án Unity ban đầu. (10%) | Xây dựng kiến trúc hệ thống (System Architecture) và mô hình dữ liệu (Data model) bằng ScriptableObject. | |
| **2** | 6/4-12/4 | Định hình System Architecture. Xây dựng các lớp dữ liệu nền tảng (Data Layer) bao gồm UnitData, LevelConfig, ActiveSkillData, StatusEffectData. (18%) | Triển khai chi tiết các ScriptableObject cho hệ thống Thẻ Tướng (HeroCardData) và kiểm toán mã nguồn. | |
| **3** | 13/4-19/4 | Hoàn thiện hệ thống Data Layer cốt lõi. Cấu hình thành công HeroCardData, LevelConfig và chuẩn hóa GameEnums cho toàn dự án. (25%) | Thực hiện kiểm toán toàn diện Phase 2, tái cấu trúc (refactor) lại hệ thống UnitData để dễ mở rộng. | |
| **4** | 20/4-26/4 | Hoàn thành Code Audit Phase 2: Tái cấu trúc hệ thống `UnitData` sang mô hình phân cấp kế thừa (`BaseUnitData`, `DefenderUnitData`, `EnemyUnitData`), loại bỏ hardcode. (32%) | Phát triển cơ chế phòng thủ đặc biệt (Lane Sweeper) và bắt đầu hệ thống cốt lõi (Core Systems). | |
| **5** | 27/4-3/5 | Thiết kế và lập trình thành công cơ chế Last Line of Defense (Lane Sweeper - Hai Bà Trưng). Đồng bộ cơ chế mới vào `LevelConfig`. (40%) | Bắt đầu Phase 3: Triển khai các hệ thống cốt lõi (GridManager, Wave Spawning, Combat System). | |
| **6** | 4/5-10/5 | Bắt đầu xây dựng Core Systems: Xử lý lưới (Grid Placement), cấu hình sinh quái (Wave Spawner) và GameStateManager. (45%) | Tiếp tục hoàn thiện Combat System (tính toán sát thương, đường đạn), logic của Enemy và xử lý va chạm. | |

Duyệt lần 1: Đánh giá khối lượng hoàn thành 45% : 
Được tiếp tục làm ĐATN [X] Không tiếp tục thực hiện ĐATN [ ]
