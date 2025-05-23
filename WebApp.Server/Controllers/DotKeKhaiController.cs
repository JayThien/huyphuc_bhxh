using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApp.API.Data;
using WebApp.API.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace WebApp.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DotKeKhaiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DotKeKhaiController> _logger;

        public DotKeKhaiController(
            ApplicationDbContext context,
            ILogger<DotKeKhaiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<DotKeKhai>>> GetDotKeKhais()
        {
            try
            {
                var dotKeKhais = await _context.DotKeKhais
                    .Include(d => d.DonVi)
                    .Select(d => new
                    {
                        d.id,
                        d.ten_dot,
                        d.so_dot,
                        d.thang,
                        d.nam,
                        d.dich_vu,
                        d.ghi_chu,
                        d.trang_thai,
                        d.nguoi_tao,
                        d.don_vi_id,
                        d.ngay_tao,
                        d.ma_ho_so,
                        DonVi = d.DonVi,
                        tong_so_tien = _context.KeKhaiBHYTs
                            .Where(k => k.dot_ke_khai_id == d.id)
                            .Sum(k => k.so_tien_can_dong),
                        tong_so_the = _context.KeKhaiBHYTs
                            .Count(k => k.dot_ke_khai_id == d.id),
                        url_bill = _context.HoaDonThanhToans
                            .Where(h => h.dot_ke_khai_id == d.id && h.deleted_at == null)
                            .OrderByDescending(h => h.ngay_tao)
                            .Select(h => h.url_bill)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return Ok(dotKeKhais);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting dot ke khai list: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách đợt kê khai", error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<DotKeKhai>> GetDotKeKhai(int id)
        {
            try
            {
                var dotKeKhai = await _context.DotKeKhais
                    .Include(d => d.DonVi)
                    .Where(d => d.id == id)
                    .Select(d => new
                    {
                        d.id,
                        d.ten_dot,
                        d.so_dot,
                        d.thang,
                        d.nam,
                        d.dich_vu,
                        d.ghi_chu,
                        d.trang_thai,
                        d.nguoi_tao,
                        d.don_vi_id,
                        d.ngay_tao,
                        d.ma_ho_so,
                        DonVi = d.DonVi,
                        tong_so_tien = _context.KeKhaiBHYTs
                            .Where(k => k.dot_ke_khai_id == d.id)
                            .Sum(k => k.so_tien_can_dong),
                        tong_so_the = _context.KeKhaiBHYTs
                            .Count(k => k.dot_ke_khai_id == d.id),
                        url_bill = _context.HoaDonThanhToans
                            .Where(h => h.dot_ke_khai_id == d.id && h.deleted_at == null)
                            .OrderByDescending(h => h.ngay_tao)
                            .Select(h => h.url_bill)
                            .FirstOrDefault()
                    })
                    .FirstOrDefaultAsync();

                if (dotKeKhai == null)
                {
                    return NotFound(new { message = "Không tìm thấy đợt kê khai" });
                }

                return Ok(dotKeKhai);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting dot ke khai {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin đợt kê khai", error = ex.Message });
            }
        }

        public class DotKeKhaiDTO
        {
            [Required]
            public int so_dot { get; set; }

            [Required]
            public int thang { get; set; }

            [Required]
            public int nam { get; set; }

            [Required]
            public int don_vi_id { get; set; }

            [Required]
            public int dai_ly_id { get; set; }

            public string? ghi_chu { get; set; }
        }

        [HttpPost]
        public async Task<ActionResult<DotKeKhai>> CreateDotKeKhai(DotKeKhai dotKeKhai)
        {
            try
            {
                // Kiểm tra đơn vị tồn tại và cập nhật dịch vụ
                var donVi = await _context.DonVis.FindAsync(dotKeKhai.don_vi_id);
                if (donVi == null)
                {
                    ModelState.AddModelError("don_vi_id", "Không tìm thấy đơn vị");
                    return BadRequest(new { errors = new[] { "Không tìm thấy đơn vị" } });
                }

                // Tự động xác định dịch vụ từ đơn vị
                dotKeKhai.dich_vu = donVi.IsBHYT ? "BHYT" : "BHXH TN";

                // Kiểm tra dữ liệu đầu vào
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Tự động tạo tên đợt
                dotKeKhai.GenerateTenDot();
                if (string.IsNullOrEmpty(dotKeKhai.ten_dot))
                {
                    return BadRequest(new { message = "Không thể tạo tên đợt do dữ liệu không hợp lệ" });
                }

                // Kiểm tra trùng lặp
                var existingDot = await _context.DotKeKhais
                    .FirstOrDefaultAsync(d => d.don_vi_id == dotKeKhai.don_vi_id
                                            && d.thang == dotKeKhai.thang
                                            && d.nam == dotKeKhai.nam
                                            && d.dich_vu == dotKeKhai.dich_vu
                                            && d.so_dot == dotKeKhai.so_dot);

                if (existingDot != null)
                {
                    return Conflict(new { message = "Đợt kê khai đã tồn tại", existingId = existingDot.id });
                }

                // Thiết lập thông tin tạo
                dotKeKhai.ngay_tao = DateTime.Now;
                dotKeKhai.nguoi_tao = User.Identity.Name;

                _context.DotKeKhais.Add(dotKeKhai);
                await _context.SaveChangesAsync();

                // Nếu có mã hồ sơ, cập nhật mã hồ sơ cho tất cả các bản ghi KeKhaiBHYT của đợt
                if (!string.IsNullOrEmpty(dotKeKhai.ma_ho_so))
                {
                    var keKhaiBHYTs = await _context.KeKhaiBHYTs
                        .Where(k => k.dot_ke_khai_id == dotKeKhai.id)
                        .ToListAsync();

                    foreach (var keKhai in keKhaiBHYTs)
                    {
                        keKhai.ma_ho_so = dotKeKhai.ma_ho_so;
                    }

                    await _context.SaveChangesAsync();
                }

                return CreatedAtAction("GetDotKeKhai", new { id = dotKeKhai.id }, dotKeKhai);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating dot ke khai: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi tạo đợt kê khai", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDotKeKhai(int id, DotKeKhai dotKeKhai)
        {
            try
            {
                if (id != dotKeKhai.id)
                {
                    return BadRequest(new { message = "ID không khớp" });
                }

                // Kiểm tra đơn vị tồn tại và cập nhật dịch vụ
                var donVi = await _context.DonVis.FindAsync(dotKeKhai.don_vi_id);
                if (donVi == null)
                {
                    ModelState.AddModelError("don_vi_id", "Không tìm thấy đơn vị");
                    return BadRequest(new { errors = new[] { "Không tìm thấy đơn vị" } });
                }

                // Tự động xác định dịch vụ từ đơn vị
                dotKeKhai.dich_vu = donVi.IsBHYT ? "BHYT" : "BHXH TN";

                // Kiểm tra dữ liệu đầu vào
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Tự động tạo tên đợt
                dotKeKhai.GenerateTenDot();
                if (string.IsNullOrEmpty(dotKeKhai.ten_dot))
                {
                    return BadRequest(new { message = "Không thể tạo tên đợt do dữ liệu không hợp lệ" });
                }

                // Lấy đợt kê khai hiện tại để kiểm tra mã hồ sơ có thay đổi không
                var existingDotKeKhai = await _context.DotKeKhais.FindAsync(id);
                if (existingDotKeKhai == null)
                {
                    return NotFound(new { message = "Không tìm thấy đợt kê khai" });
                }

                // Kiểm tra trùng lặp nếu thông tin thay đổi
                if (existingDotKeKhai.don_vi_id != dotKeKhai.don_vi_id ||
                    existingDotKeKhai.thang != dotKeKhai.thang ||
                    existingDotKeKhai.nam != dotKeKhai.nam ||
                    existingDotKeKhai.dich_vu != dotKeKhai.dich_vu ||
                    existingDotKeKhai.so_dot != dotKeKhai.so_dot)
                {
                    var duplicateDot = await _context.DotKeKhais
                        .FirstOrDefaultAsync(d => d.id != id &&
                                                d.don_vi_id == dotKeKhai.don_vi_id &&
                                                d.thang == dotKeKhai.thang &&
                                                d.nam == dotKeKhai.nam &&
                                                d.dich_vu == dotKeKhai.dich_vu &&
                                                d.so_dot == dotKeKhai.so_dot);

                    if (duplicateDot != null)
                    {
                        return Conflict(new { message = "Đợt kê khai đã tồn tại", existingId = duplicateDot.id });
                    }
                }

                // Kiểm tra xem mã hồ sơ có thay đổi không
                bool maHoSoChanged = existingDotKeKhai.ma_ho_so != dotKeKhai.ma_ho_so;

                // Cập nhật đợt kê khai
                _context.Entry(existingDotKeKhai).CurrentValues.SetValues(dotKeKhai);
                _context.Entry(existingDotKeKhai).Property(x => x.ngay_tao).IsModified = false;
                _context.Entry(existingDotKeKhai).Property(x => x.nguoi_tao).IsModified = false;

                // Nếu mã hồ sơ thay đổi, cập nhật mã hồ sơ cho tất cả các bản ghi KeKhaiBHYT của đợt
                if (maHoSoChanged && !string.IsNullOrEmpty(dotKeKhai.ma_ho_so))
                {
                    var keKhaiBHYTs = await _context.KeKhaiBHYTs
                        .Where(k => k.dot_ke_khai_id == id)
                        .ToListAsync();

                    foreach (var keKhai in keKhaiBHYTs)
                    {
                        keKhai.ma_ho_so = dotKeKhai.ma_ho_so;
                    }
                }

                await _context.SaveChangesAsync();
                
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DotKeKhaiExists(id))
                {
                    return NotFound(new { message = "Không tìm thấy đợt kê khai" });
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating dot ke khai {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi cập nhật đợt kê khai", error = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDotKeKhai(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var dotKeKhai = await _context.DotKeKhais.FindAsync(id);
                if (dotKeKhai == null)
                {
                    return NotFound(new { message = "Không tìm thấy đợt kê khai" });
                }

                // Lấy tất cả các bản ghi kê khai BHYT trong đợt
                var keKhaiBHYTs = await _context.KeKhaiBHYTs
                    .Include(k => k.QuyenBienLai)
                    .Include(k => k.BienLai)
                    .Where(k => k.dot_ke_khai_id == id)
                    .ToListAsync();

                // Xử lý số biên lai cho từng bản ghi trước khi xóa
                foreach (var keKhai in keKhaiBHYTs)
                {
                    if (keKhai.QuyenBienLai != null && !string.IsNullOrEmpty(keKhai.so_bien_lai))
                    {
                        // Reset số hiện tại về số của biên lai bị xóa
                        keKhai.QuyenBienLai.so_hien_tai = keKhai.so_bien_lai;
                        
                        // Nếu trạng thái là đã sử dụng thì chuyển về đang sử dụng
                        if (keKhai.QuyenBienLai.trang_thai == "da_su_dung")
                        {
                            keKhai.QuyenBienLai.trang_thai = "dang_su_dung";
                        }
                        
                        _context.QuyenBienLais.Update(keKhai.QuyenBienLai);
                    }
                }

                await _context.SaveChangesAsync();

                // Xóa đợt kê khai (các bản ghi kê khai sẽ tự động bị xóa do có Cascade)
                _context.DotKeKhais.Remove(dotKeKhai);
                await _context.SaveChangesAsync();
                
                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error deleting dot ke khai {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi xóa đợt kê khai", error = ex.Message });
            }
        }

        [HttpGet("next-so-dot")]
        public async Task<ActionResult<int>> GetNextSoDot(
            [FromQuery] int donViId, 
            [FromQuery] int thang,
            [FromQuery] int nam)
        {
            try
            {
                var donVi = await _context.DonVis.FindAsync(donViId);
                if (donVi == null)
                {
                    return BadRequest(new { message = "Không tìm thấy đơn vị" });
                }

                var lastDotKeKhai = await _context.DotKeKhais
                    .Where(d => d.don_vi_id == donViId 
                        && d.thang == thang
                        && d.nam == nam)
                    .OrderByDescending(d => d.so_dot)
                    .FirstOrDefaultAsync();

                var nextSoDot = (lastDotKeKhai?.so_dot ?? 0) + 1;

                // Kiểm tra xem số đợt tiếp theo đã tồn tại chưa
                while (await _context.DotKeKhais.AnyAsync(d => 
                    d.don_vi_id == donViId 
                    && d.thang == thang 
                    && d.nam == nam 
                    && d.so_dot == nextSoDot))
                {
                    nextSoDot++;
                }

                return Ok(nextSoDot);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting next so dot: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi lấy số đợt tiếp theo" });
            }
        }

        [HttpGet("{id}/ke-khai-bhyt")]
        public async Task<ActionResult<IEnumerable<KeKhaiBHYT>>> GetKeKhaiBHYTs(int id)
        {
            try
            {
                // Lấy thông tin người dùng hiện tại
                var username = User.Identity?.Name;
                var nguoiDung = await _context.NguoiDungs
                    .FirstOrDefaultAsync(n => n.user_name == username);

                if (nguoiDung == null)
                {
                    return Unauthorized(new { message = "Không tìm thấy thông tin người dùng" });
                }

                _logger.LogInformation($"Bắt đầu lấy danh sách kê khai BHYT cho đợt {id}");

                // Kiểm tra đợt kê khai có tồn tại không
                var dotKeKhai = await _context.DotKeKhais
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.id == id);

                if (dotKeKhai == null)
                {
                    _logger.LogWarning($"Không tìm thấy đợt kê khai ID: {id}");
                    return NotFound(new { message = $"Không tìm thấy đợt kê khai có ID: {id}" });
                }

                _logger.LogInformation($"Tìm thấy đợt kê khai: {JsonSerializer.Serialize(dotKeKhai)}");

                if (dotKeKhai.dich_vu != "BHYT")
                {
                    _logger.LogWarning($"Đợt kê khai {id} không phải loại BHYT. Loại dịch vụ: {dotKeKhai.dich_vu}");
                    return BadRequest(new { message = $"Đợt kê khai này không phải là BHYT (Loại dịch vụ: {dotKeKhai.dich_vu})" });
                }

                // Lấy danh sách kê khai BHYT
                var keKhaiBHYTs = await _context.KeKhaiBHYTs
                    .Include(k => k.ThongTinThe)
                    .Include(k => k.QuyenBienLai)
                    .Where(k => k.dot_ke_khai_id == id)
                    .OrderBy(k => k.ngay_tao)
                    .Select(k => new
                    {
                        ho_ten = k.ThongTinThe.ho_ten,
                        cccd = k.ThongTinThe.cccd,
                        ma_so_bhxh = k.ThongTinThe.ma_so_bhxh,
                        ngay_sinh = k.ThongTinThe.ngay_sinh,
                        gioi_tinh = k.ThongTinThe.gioi_tinh,
                        phuong_an_dong = k.phuong_an_dong,
                        so_dien_thoai = k.ThongTinThe.so_dien_thoai,
                        so_the_bhyt = k.ThongTinThe.so_the_bhyt,
                        so_tien = k.so_tien_can_dong,
                        nguoi_thu = k.nguoi_thu,
                        ngay_bien_lai = k.ngay_bien_lai,
                        ma_tinh_nkq = k.ThongTinThe.ma_tinh_nkq,
                        ma_huyen_nkq = k.ThongTinThe.ma_huyen_nkq,
                        ma_xa_nkq = k.ThongTinThe.ma_xa_nkq,
                        dia_chi_nkq = k.dia_chi_nkq,
                        so_thang_dong = k.so_thang_dong,
                        ma_benh_vien = k.ThongTinThe.ma_benh_vien,
                        ma_hgd = k.ThongTinThe.ma_hgd,
                        quoc_tich = k.ThongTinThe.quoc_tich,
                        ma_tinh_ks = k.ThongTinThe.ma_tinh_ks,
                        ma_huyen_ks = k.ThongTinThe.ma_huyen_ks,
                        ma_xa_ks = k.ThongTinThe.ma_xa_ks,
                        han_the_moi_tu = k.han_the_moi_tu,
                        is_urgent = k.is_urgent,
                        so_bien_lai = k.so_bien_lai,
                        QuyenBienLai = new {
                            quyen_so = k.QuyenBienLai.quyen_so
                        },
                        ma_nhan_vien = nguoiDung.ma_nhan_vien,
                        ngay_tao = k.ngay_tao
                    })
                    .ToListAsync();

                _logger.LogInformation($"Tìm thấy {keKhaiBHYTs.Count} bản ghi kê khai BHYT cho đợt {id}");
                
                // Gán STT theo thứ tự đã sắp xếp
                var keKhaiBHYTsWithSTT = keKhaiBHYTs
                    .Select((k, index) => new {
                        stt = index + 1,
                        ho_ten = k.ho_ten,
                        cccd = k.cccd,
                        ma_so_bhxh = k.ma_so_bhxh,
                        ngay_sinh = k.ngay_sinh,
                        gioi_tinh = k.gioi_tinh,
                        phuong_an_dong = k.phuong_an_dong,
                        so_dien_thoai = k.so_dien_thoai,
                        so_the_bhyt = k.so_the_bhyt,
                        so_tien = k.so_tien,
                        nguoi_thu = k.nguoi_thu,
                        ngay_bien_lai = k.ngay_bien_lai,
                        ma_tinh_nkq = k.ma_tinh_nkq,
                        ma_huyen_nkq = k.ma_huyen_nkq,
                        ma_xa_nkq = k.ma_xa_nkq,
                        dia_chi_nkq = k.dia_chi_nkq,
                        so_thang_dong = k.so_thang_dong,
                        ma_benh_vien = k.ma_benh_vien,
                        ma_hgd = k.ma_hgd,
                        quoc_tich = k.quoc_tich,
                        ma_tinh_ks = k.ma_tinh_ks,
                        ma_huyen_ks = k.ma_huyen_ks,
                        ma_xa_ks = k.ma_xa_ks,
                        han_the_moi_tu = k.han_the_moi_tu,
                        is_urgent = k.is_urgent,
                        so_bien_lai = k.so_bien_lai,
                        QuyenBienLai = k.QuyenBienLai,
                        ma_nhan_vien = k.ma_nhan_vien,
                        ngay_tao = k.ngay_tao
                    })
                    .ToList();
                
                return Ok(keKhaiBHYTsWithSTT);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy danh sách kê khai BHYT cho đợt {id}: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách kê khai BHYT", error = ex.Message });
            }
        }

        [HttpPatch("{id}/trang-thai")]
        public async Task<IActionResult> UpdateTrangThai(int id, [FromBody] UpdateTrangThaiDto dto)
        {
            try
            {
                var dotKeKhai = await _context.DotKeKhais.FindAsync(id);
                if (dotKeKhai == null)
                {
                    return NotFound(new { message = "Không tìm thấy đợt kê khai" });
                }

                dotKeKhai.trang_thai = dto.trang_thai;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Cập nhật trạng thái thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating dot ke khai status: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi cập nhật trạng thái đợt kê khai", error = ex.Message });
            }
        }

        [HttpPatch("{id}/gui")]
        public async Task<IActionResult> GuiDotKeKhai(int id)
        {
            // Sử dụng transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Kiểm tra đợt kê khai tồn tại
                var dotKeKhai = await _context.DotKeKhais
                    .Include(d => d.DonVi)
                    .FirstOrDefaultAsync(d => d.id == id);
                    
                if (dotKeKhai == null)
                {
                    return NotFound(new { message = "Không tìm thấy đợt kê khai" });
                }

                // Kiểm tra trạng thái hiện tại
                if (dotKeKhai.trang_thai != "chua_gui")
                {
                    return BadRequest(new { message = "Đợt kê khai không ở trạng thái chưa gửi" });
                }

                // Lấy thông tin người dùng đăng nhập
                var userName = User.Identity != null ? User.Identity.Name : null;
                if (string.IsNullOrEmpty(userName))
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin người dùng đăng nhập" });
                }

                var nguoiThu = await _context.NguoiDungs
                    .FirstOrDefaultAsync(n => n.user_name == userName);

                if (nguoiThu == null)
                {
                    return BadRequest(new { message = "Không tìm thấy thông tin người thu" });
                }

                // Lấy quyển biên lai đang sử dụng
                var quyenBienLai = await _context.QuyenBienLais
                    .Where(q => q.nhan_vien_thu == nguoiThu.id && 
                           q.trang_thai == "dang_su_dung")
                    .OrderBy(q => q.ngay_cap)
                    .FirstOrDefaultAsync();

                if (quyenBienLai == null)
                {
                    return BadRequest(new { message = "Người thu chưa được cấp quyển biên lai hoặc đã hết số" });
                }

                // Cập nhật trạng thái đợt kê khai sang chờ thanh toán
                dotKeKhai.trang_thai = "cho_thanh_toan";
                
                // Cập nhật trạng thái các kê khai BHYT trong đợt và cấp số biên lai
                var keKhaiBHYTs = await _context.KeKhaiBHYTs
                    .Include(k => k.ThongTinThe)
                    .Where(k => k.dot_ke_khai_id == id && k.so_bien_lai == null)
                    .OrderBy(k => k.id)
                    .ToListAsync();

                foreach (var keKhai in keKhaiBHYTs)
                {
                    // Cập nhật trạng thái
                    keKhai.trang_thai = "cho_thanh_toan";
                    
                    // Gán quyển biên lai cho kê khai
                    keKhai.quyen_bien_lai_id = quyenBienLai.id;

                    if (quyenBienLai.so_hien_tai == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { message = "Số hiện tại không hợp lệ" });
                    }

                    try
                    {
                        var soHienTai = int.Parse(quyenBienLai.so_hien_tai);
                        var denSo = int.Parse(quyenBienLai.den_so);
                        
                        _logger.LogInformation($"Số hiện tại: {soHienTai}, Đến số: {denSo}");

                        if (soHienTai > denSo)
                        {
                            quyenBienLai.trang_thai = "da_su_dung";
                            await _context.SaveChangesAsync();
                            
                            // Tìm quyển biên lai mới
                            quyenBienLai = await _context.QuyenBienLais
                                .Where(q => q.nhan_vien_thu == nguoiThu.id && 
                                       q.trang_thai == "chua_su_dung")
                                .OrderBy(q => q.ngay_cap)
                                .FirstOrDefaultAsync();
                                
                            if (quyenBienLai == null)
                            {
                                await transaction.RollbackAsync();
                                return BadRequest(new { message = "Đã hết số biên lai, vui lòng cấp thêm quyển biên lai mới" });
                            }
                            
                            // Cập nhật trạng thái quyển mới
                            quyenBienLai.trang_thai = "dang_su_dung";
                            quyenBienLai.so_hien_tai = quyenBienLai.tu_so;
                            await _context.SaveChangesAsync();
                            
                            // Cập nhật lại số hiện tại
                            soHienTai = int.Parse(quyenBienLai.so_hien_tai);
                            denSo = int.Parse(quyenBienLai.den_so);
                        }

                        keKhai.so_bien_lai = soHienTai.ToString().PadLeft(quyenBienLai.so_hien_tai.Length, '0');
                        quyenBienLai.so_hien_tai = (soHienTai + 1).ToString().PadLeft(quyenBienLai.so_hien_tai.Length, '0');

                        _logger.LogInformation($"Cấp số biên lai: {keKhai.so_bien_lai}, Số tiếp theo: {quyenBienLai.so_hien_tai}");
                        
                        // Tạo biên lai
                        var bienLai = new BienLai
                        {
                            quyen_so = quyenBienLai.quyen_so,
                            so_bien_lai = keKhai.so_bien_lai,
                            ten_nguoi_dong = keKhai.ThongTinThe.ho_ten,
                            so_tien = keKhai.so_tien_can_dong,
                            ke_khai_bhyt_id = keKhai.id,
                            ma_so_bhxh = keKhai.ThongTinThe.ma_so_bhxh,
                            ma_nhan_vien = nguoiThu.ma_nhan_vien,
                            ngay_bien_lai = keKhai.ngay_bien_lai ?? DateTime.Now,
                            ma_co_quan_bhxh = dotKeKhai.DonVi?.MaCoQuanBHXH ?? "",
                            ma_so_bhxh_don_vi = dotKeKhai.DonVi?.MaSoBHXH ?? "",
                            tinh_chat = "bien_lai_goc",
                            is_bhyt = dotKeKhai.dich_vu == "BHYT",
                            is_bhxh = dotKeKhai.dich_vu == "BHXH TN"
                        };

                        _context.BienLais.Add(bienLai);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Lỗi xử lý số biên lai: {ex.Message}");
                        await transaction.RollbackAsync();
                        throw;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Gửi đợt kê khai thành công" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error sending dot ke khai {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi gửi đợt kê khai", error = ex.Message });
            }
        }

        [HttpPost("{id}/update-ma-ho-so")]
        public async Task<IActionResult> UpdateMaHoSo(int id, [FromBody] UpdateMaHoSoDto dto)
        {
            try
            {
                // Tìm đợt kê khai theo ID
                var dotKeKhai = await _context.DotKeKhais.FindAsync(id);
                if (dotKeKhai == null)
                {
                    return NotFound($"Không tìm thấy đợt kê khai với ID {id}");
                }

                // Cập nhật chỉ trường ma_ho_so
                dotKeKhai.ma_ho_so = dto.ma_ho_so;
                
                // Lưu thay đổi
                await _context.SaveChangesAsync();

                // Cập nhật mã hồ sơ cho tất cả các bản ghi KeKhaiBHYT của đợt
                if (!string.IsNullOrEmpty(dto.ma_ho_so))
                {
                    var keKhaiBHYTs = await _context.KeKhaiBHYTs
                        .Where(k => k.dot_ke_khai_id == id)
                        .ToListAsync();

                    foreach (var keKhai in keKhaiBHYTs)
                    {
                        keKhai.ma_ho_so = dto.ma_ho_so;
                    }

                    await _context.SaveChangesAsync();
                }
                
                return Ok(new { success = true, message = "Cập nhật mã hồ sơ thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating ma_ho_so for dot ke khai {id}: {ex.Message}");
                return StatusCode(500, new { success = false, message = "Lỗi khi cập nhật mã hồ sơ", error = ex.Message });
            }
        }

        public class UpdateTrangThaiDto
        {
            public string trang_thai { get; set; }
        }

        public class UpdateMaHoSoDto
        {
            public string? ma_ho_so { get; set; }
        }

        private bool DotKeKhaiExists(int id)
        {
            return _context.DotKeKhais.Any(e => e.id == id);
        }
    }
} 