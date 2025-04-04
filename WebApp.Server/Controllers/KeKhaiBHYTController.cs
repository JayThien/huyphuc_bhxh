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
    [Route("api/dot-ke-khai")]
    public class KeKhaiBHYTController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<KeKhaiBHYTController> _logger;

        public KeKhaiBHYTController(
            ApplicationDbContext context,
            ILogger<KeKhaiBHYTController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("{dotKeKhaiId}/ke-khai-bhyt")]
        public async Task<ActionResult<IEnumerable<KeKhaiBHYT>>> GetByDotKeKhai(int dotKeKhaiId)
        {
            try
            {
                var keKhaiBHYTs = await _context.KeKhaiBHYTs
                    .Include(k => k.DotKeKhai)
                    .Include(k => k.ThongTinThe)
                    .Where(k => k.dot_ke_khai_id == dotKeKhaiId)
                    .Select(k => new KeKhaiBHYT
                    {
                        id = k.id,
                        dot_ke_khai_id = k.dot_ke_khai_id,
                        thong_tin_the_id = k.thong_tin_the_id,
                        nguoi_thu = k.nguoi_thu,
                        so_thang_dong = k.so_thang_dong,
                        phuong_an_dong = k.phuong_an_dong,
                        han_the_cu = k.han_the_cu,
                        han_the_moi_tu = k.han_the_moi_tu,
                        han_the_moi_den = k.han_the_moi_den,
                        tinh_nkq = k.tinh_nkq,
                        huyen_nkq = k.huyen_nkq,
                        xa_nkq = k.xa_nkq,
                        dia_chi_nkq = k.dia_chi_nkq,
                        benh_vien_kcb = k.benh_vien_kcb,
                        nguoi_tao = k.nguoi_tao,
                        ngay_tao = k.ngay_tao,
                        ngay_bien_lai = k.ngay_bien_lai,
                        so_tien_can_dong = k.so_tien_can_dong,
                        DotKeKhai = k.DotKeKhai,
                        ThongTinThe = k.ThongTinThe
                    })
                    .ToListAsync();

                return Ok(keKhaiBHYTs);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting ke khai BHYT list by dot ke khai {dotKeKhaiId}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi lấy danh sách kê khai BHYT", error = ex.Message });
            }
        }

        [HttpGet("{dotKeKhaiId}/ke-khai-bhyt/{id}")]
        public async Task<ActionResult<KeKhaiBHYT>> GetKeKhaiBHYT(int dotKeKhaiId, int id)
        {
            try
            {
                var keKhaiBHYT = await _context.KeKhaiBHYTs
                    .Include(k => k.ThongTinThe)
                    .Include(k => k.DotKeKhai)
                    .FirstOrDefaultAsync(k => k.dot_ke_khai_id == dotKeKhaiId && k.id == id);

                if (keKhaiBHYT == null)
                {
                    return NotFound(new { message = "Không tìm thấy kê khai BHYT" });
                }

                return Ok(keKhaiBHYT);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting ke khai BHYT {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi lấy thông tin kê khai BHYT", error = ex.Message });
            }
        }

        [HttpPost("{dotKeKhaiId}/ke-khai-bhyt")]
        public async Task<IActionResult> CreateKeKhaiBHYT(int dotKeKhaiId, KeKhaiBHYT keKhaiBHYT)
        {
            // Sử dụng transaction để đảm bảo tính toàn vẹn dữ liệu
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Kiểm tra đợt kê khai tồn tại
                var dotKeKhai = await _context.DotKeKhais
                    .Include(d => d.DonVi)
                    .FirstOrDefaultAsync(d => d.id == dotKeKhaiId);

                if (dotKeKhai == null)
                {
                    return NotFound(new { message = "Không tìm thấy đợt kê khai" });
                }

                // Kiểm tra trạng thái đợt kê khai
                if (dotKeKhai.trang_thai != "chua_gui")
                {
                    return BadRequest(new { message = "Đợt kê khai đã được gửi, không thể thêm mới" });
                }

                // Gán đợt kê khai cho kê khai BHYT
                keKhaiBHYT.dot_ke_khai_id = dotKeKhaiId;
                keKhaiBHYT.DotKeKhai = dotKeKhai;

                // Gán mã hồ sơ từ đợt kê khai nếu có
                if (!string.IsNullOrEmpty(dotKeKhai.ma_ho_so))
                {
                    keKhaiBHYT.ma_ho_so = dotKeKhai.ma_ho_so;
                }

                // Kiểm tra xem thông tin thẻ đã tồn tại chưa
                if (keKhaiBHYT.ThongTinThe != null && !string.IsNullOrEmpty(keKhaiBHYT.ThongTinThe.ma_so_bhxh))
                {
                    var existingThongTinThe = await _context.ThongTinThes
                        .FirstOrDefaultAsync(t => t.ma_so_bhxh == keKhaiBHYT.ThongTinThe.ma_so_bhxh);

                    if (existingThongTinThe != null)
                    {
                        // Sử dụng thông tin thẻ đã tồn tại
                        keKhaiBHYT.thong_tin_the_id = existingThongTinThe.id;
                        keKhaiBHYT.ThongTinThe = existingThongTinThe;
                        
                        // Cập nhật thông tin thẻ nếu cần
                        _context.Entry(existingThongTinThe).CurrentValues.SetValues(keKhaiBHYT.ThongTinThe);
                    }
                }

                // Lấy thông tin người dùng đăng nhập
                var userName = User.Identity != null ? User.Identity.Name : null;
                if (string.IsNullOrEmpty(userName))
                {
                    _logger.LogWarning("Không tìm thấy thông tin người dùng đăng nhập");
                    return BadRequest(new { message = "Không tìm thấy thông tin người dùng đăng nhập" });
                }

                // Gán thông tin người tạo
                keKhaiBHYT.nguoi_tao = userName;
                keKhaiBHYT.ngay_tao = DateTime.Now;

                // Lưu kê khai BHYT
                _context.KeKhaiBHYTs.Add(keKhaiBHYT);
                await _context.SaveChangesAsync();

                // Nếu mọi thứ OK thì commit transaction
                await transaction.CommitAsync();

                return CreatedAtAction(nameof(GetKeKhaiBHYT), 
                    new { dotKeKhaiId, id = keKhaiBHYT.id }, 
                    new { success = true, message = "Tạo kê khai BHYT thành công", data = keKhaiBHYT }
                );
            }
            catch (Exception ex)
            {
                // Có lỗi thì rollback transaction
                await transaction.RollbackAsync();
                _logger.LogError($"Error creating ke khai BHYT: {ex.Message}");
                return StatusCode(500, new {
                    success = false,
                    message = "Lỗi khi tạo kê khai BHYT",
                    error = ex.Message
                });
            }
        }

        [HttpPut("{dotKeKhaiId}/ke-khai-bhyt/{id}")]
        public async Task<IActionResult> UpdateKeKhaiBHYT(int dotKeKhaiId, int id, KeKhaiBHYT keKhaiBHYT)
        {
            if (id != keKhaiBHYT.id || dotKeKhaiId != keKhaiBHYT.dot_ke_khai_id)
            {
                return BadRequest(new { message = "ID không khớp" });
            }

            try
            {
                var existingKeKhaiBHYT = await _context.KeKhaiBHYTs
                    .Include(k => k.ThongTinThe)
                    .Include(k => k.DotKeKhai)
                    .FirstOrDefaultAsync(k => k.dot_ke_khai_id == dotKeKhaiId && k.id == id);
                    
                if (existingKeKhaiBHYT == null)
                {
                    return NotFound(new { message = "Không tìm thấy kê khai BHYT" });
                }

                // Cập nhật thông tin thẻ
                if (existingKeKhaiBHYT.ThongTinThe != null && keKhaiBHYT.ThongTinThe != null)
                {
                    _context.Entry(existingKeKhaiBHYT.ThongTinThe).CurrentValues.SetValues(keKhaiBHYT.ThongTinThe);
                }

                // Lưu giá trị quyen_bien_lai_id hiện tại nếu không được cung cấp trong request
                if (keKhaiBHYT.quyen_bien_lai_id == null && existingKeKhaiBHYT.quyen_bien_lai_id != null)
                {
                    keKhaiBHYT.quyen_bien_lai_id = existingKeKhaiBHYT.quyen_bien_lai_id;
                }

                // Lưu giá trị trang_thai hiện tại nếu không được cung cấp trong request hoặc là giá trị mặc định
                if (string.IsNullOrEmpty(keKhaiBHYT.trang_thai) || keKhaiBHYT.trang_thai == "chua_gui")
                {
                    keKhaiBHYT.trang_thai = existingKeKhaiBHYT.trang_thai;
                }

                // Cập nhật thông tin kê khai
                _context.Entry(existingKeKhaiBHYT).CurrentValues.SetValues(keKhaiBHYT);

                // Đảm bảo mã hồ sơ được đồng bộ với đợt kê khai
                if (existingKeKhaiBHYT.DotKeKhai != null && !string.IsNullOrEmpty(existingKeKhaiBHYT.DotKeKhai.ma_ho_so))
                {
                    existingKeKhaiBHYT.ma_ho_so = existingKeKhaiBHYT.DotKeKhai.ma_ho_so;
                }

                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!KeKhaiBHYTExists(dotKeKhaiId, id))
                {
                    return NotFound(new { message = "Không tìm thấy kê khai BHYT" });
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating ke khai BHYT {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi cập nhật kê khai BHYT", error = ex.Message });
            }
        }

        [HttpDelete("{dotKeKhaiId}/ke-khai-bhyt/{id}")]
        public async Task<IActionResult> DeleteKeKhaiBHYT(int dotKeKhaiId, int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Lấy thông tin kê khai và quyển biên lai
                var keKhaiBHYT = await _context.KeKhaiBHYTs
                    .Include(k => k.QuyenBienLai)
                    .FirstOrDefaultAsync(k => k.dot_ke_khai_id == dotKeKhaiId && k.id == id);
                    
                if (keKhaiBHYT == null)
                {
                    return NotFound(new { message = "Không tìm thấy kê khai BHYT" });
                }

                // Lấy biên lai liên quan
                var bienLai = await _context.BienLais
                    .FirstOrDefaultAsync(b => b.ke_khai_bhyt_id == keKhaiBHYT.id);

                if (bienLai != null && keKhaiBHYT.QuyenBienLai != null)
                {
                    // Xóa biên lai
                    _context.BienLais.Remove(bienLai);
                    await _context.SaveChangesAsync();

                    // Reset số hiện tại về số của biên lai bị xóa
                    keKhaiBHYT.QuyenBienLai.so_hien_tai = keKhaiBHYT.so_bien_lai;
                    
                    // Nếu trạng thái là đã sử dụng thì chuyển về đang sử dụng
                    if (keKhaiBHYT.QuyenBienLai.trang_thai == "da_su_dung")
                    {
                        keKhaiBHYT.QuyenBienLai.trang_thai = "dang_su_dung";
                    }
                }

                // Xóa kê khai BHYT
                _context.KeKhaiBHYTs.Remove(keKhaiBHYT);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error deleting ke khai BHYT {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi xóa kê khai BHYT", error = ex.Message });
            }
        }

        [HttpPost("{dotKeKhaiId}/ke-khai-bhyt/delete-multiple")]
        public async Task<IActionResult> DeleteMultiple(int dotKeKhaiId, [FromBody] DeleteMultipleDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var keKhaiBHYTs = await _context.KeKhaiBHYTs
                    .Include(k => k.BienLai)
                    .Where(k => k.dot_ke_khai_id == dotKeKhaiId && dto.ids.Contains(k.id))
                    .ToListAsync();

                foreach (var keKhai in keKhaiBHYTs)
                {
                    var quyenBienLai = await _context.QuyenBienLais
                        .FirstOrDefaultAsync(q => q.id == keKhai.quyen_bien_lai_id);

                    if (quyenBienLai != null)
                    {
                        quyenBienLai.so_hien_tai = keKhai.so_bien_lai;
                        if (quyenBienLai.trang_thai == "da_su_dung")
                        {
                            quyenBienLai.trang_thai = "dang_su_dung";
                        }
                        _context.QuyenBienLais.Update(quyenBienLai);
                    }
                }

                _context.KeKhaiBHYTs.RemoveRange(keKhaiBHYTs);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError($"Error deleting multiple ke khai BHYT: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi xóa nhiều kê khai BHYT", error = ex.Message });
            }
        }

        private bool KeKhaiBHYTExists(int dotKeKhaiId, int id)
        {
            return _context.KeKhaiBHYTs.Any(e => e.dot_ke_khai_id == dotKeKhaiId && e.id == id);
        }

        [HttpPatch("{dotKeKhaiId}/ke-khai-bhyt/{id}/toggle-urgent")]
        public async Task<IActionResult> ToggleUrgent(int dotKeKhaiId, int id)
        {
            try
            {
                var keKhaiBHYT = await _context.KeKhaiBHYTs
                    .FirstOrDefaultAsync(k => k.dot_ke_khai_id == dotKeKhaiId && k.id == id);
                    
                if (keKhaiBHYT == null)
                {
                    return NotFound(new { message = "Không tìm thấy kê khai BHYT" });
                }

                // Toggle trạng thái urgent
                keKhaiBHYT.is_urgent = !keKhaiBHYT.is_urgent;
                await _context.SaveChangesAsync();

                return Ok(new { is_urgent = keKhaiBHYT.is_urgent });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling urgent status for ke khai BHYT {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi cập nhật trạng thái gấp", error = ex.Message });
            }
        }

        [HttpPost("{id}/cap-nhat-so-bien-lai")]
        public async Task<IActionResult> CapNhatSoBienLai(int id, [FromBody] CapNhatSoBienLaiDto dto)
        {
            try
            {
                var keKhai = await _context.KeKhaiBHYTs
                    .Include(k => k.QuyenBienLai)
                    .FirstOrDefaultAsync(k => k.id == id);

                if (keKhai == null)
                {
                    return NotFound(new { message = "Không tìm thấy kê khai BHYT" });
                }

                if (keKhai.QuyenBienLai == null)
                {
                    return BadRequest(new { message = "Kê khai chưa được gán quyển biên lai" });
                }

                // Kiểm tra số biên lai có hợp lệ không
                var quyenBienLai = keKhai.QuyenBienLai;
                var soBienLai = int.Parse(dto.so_bien_lai);
                var tuSo = int.Parse(quyenBienLai.tu_so);
                var denSo = int.Parse(quyenBienLai.den_so);

                if (soBienLai < tuSo || soBienLai > denSo)
                {
                    return BadRequest(new { message = "Số biên lai không nằm trong khoảng cho phép" });
                }

                // Kiểm tra số biên lai đã được sử dụng chưa
                var daCoSoBienLai = await _context.KeKhaiBHYTs
                    .AnyAsync(k => k.id != id && 
                                  k.quyen_bien_lai_id == quyenBienLai.id && 
                                  k.so_bien_lai == dto.so_bien_lai);

                if (daCoSoBienLai)
                {
                    return BadRequest(new { message = "Số biên lai đã được sử dụng" });
                }

                keKhai.so_bien_lai = dto.so_bien_lai;
                quyenBienLai.so_hien_tai = dto.so_bien_lai;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Cập nhật số biên lai thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error updating so bien lai for ke khai BHYT {id}: {ex.Message}");
                return StatusCode(500, new { message = "Lỗi khi cập nhật số biên lai", error = ex.Message });
            }
        }

        [HttpPost("create-bien-lai")]
        public async Task<IActionResult> CreateBienLai([FromBody] BienLaiCreateDto dto)
        {
            try
            {
                // Kiểm tra quyển biên lai
                var quyenBienLai = await _context.QuyenBienLais
                    .FirstOrDefaultAsync(q => q.quyen_so == dto.quyen_so);

                if (quyenBienLai == null)
                {
                    return BadRequest(new { message = "Quyển biên lai không tồn tại" });
                }

                // Tạo biên lai mới
                var bienLai = new BienLai
                {
                    quyen_so = dto.quyen_so,
                    so_bien_lai = dto.so_bien_lai,
                    ten_nguoi_dong = dto.ten_nguoi_dong,
                    so_tien = dto.so_tien,
                    ghi_chu = dto.ghi_chu,
                    trang_thai = "active",
                    ngay_tao = DateTime.Now
                };

                _context.BienLais.Add(bienLai);
                await _context.SaveChangesAsync();

                return Ok(bienLai);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tạo biên lai");
                return StatusCode(500, new { message = "Có lỗi xảy ra khi tạo biên lai" });
            }
        }
    }

    public class DeleteMultipleDto
    {
        [Required]
        public int[] ids { get; set; } = Array.Empty<int>();
    }

    public class CapNhatSoBienLaiDto
    {
        [Required]
        public string so_bien_lai { get; set; }
    }

    public class BienLaiCreateDto
    {
        [Required]
        public string quyen_so { get; set; }
        [Required]
        public string so_bien_lai { get; set; }
        [Required]
        public string ten_nguoi_dong { get; set; }
        [Required]
        public decimal so_tien { get; set; }
        public string ghi_chu { get; set; }
    }
} 