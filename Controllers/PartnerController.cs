using HotelBooking.Data;
using HotelBooking.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HotelBooking.ViewModels;
using QuestPDF.Fluent;
using HotelBooking.Documents;
using HotelBooking.ViewModels.Rooms;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using HotelBooking.Resources;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace HotelBooking.Controllers
{
    [Authorize]
    public class PartnerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public PartnerController(ApplicationDbContext db, UserManager<ApplicationUser> users, IWebHostEnvironment hostEnvironment, IStringLocalizer<SharedResource> localizer)
        {
            _db = db;
            _users = users;
            _hostEnvironment = hostEnvironment;
            _localizer = localizer;
        }

        // Thêm method để set ViewBag.UserHasProperties
        private async Task SetUserHasPropertiesAsync()
        {
            var userId = _users.GetUserId(User);
            if (!string.IsNullOrEmpty(userId))
            {
                ViewBag.UserHasProperties = await _db.Properties.AnyAsync(p => p.UserId == userId);
            }
            else
            {
                ViewBag.UserHasProperties = false;
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                await SetUserHasPropertiesAsync();
            }
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Start()
        {
            var me = await _users.GetUserAsync(User);
            if (me == null)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Start), "Partner") });

            await SetUserHasPropertiesAsync(); // Thêm dòng này
            var vm = new PartnerStartViewModel { Email = me.Email ?? string.Empty };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(PartnerStartViewModel vm)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Start), "Partner") });
            if (!ModelState.IsValid) return View(vm);

            var pa = await _db.PartnerAccounts.FirstOrDefaultAsync(x => x.UserId == me.Id);
            if (pa == null)
            {
                pa = new PartnerAccount { UserId = me.Id, ContactEmail = vm.Email.Trim(), Status = PartnerStatus.New };
                _db.PartnerAccounts.Add(pa);
            }
            else
            {
                pa.ContactEmail = vm.Email.Trim();
                pa.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            
            // Tạo property mới khi người dùng ấn "đăng chỗ nghỉ"
            var existingCount = await _db.Properties.CountAsync(p => p.UserId == me.Id);
            var propertyNumber = existingCount + 1;
            
            var newProperty = new Property
            {
                UserId = me.Id,
                Name = $"Cơ sở lưu trú {propertyNumber}",
                Type = PropertyType.Hotel,
                CountryCode = "VN",
                City = "",
                AddressLine = "",
                IsDraft = true,
                Status = PropertyStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            
            _db.Properties.Add(newProperty);
            await _db.SaveChangesAsync();
            
            TempData["success"] = "Đã lưu email liên hệ cho tài khoản đối tác và tạo cơ sở lưu trú mới.";
            return RedirectToAction(nameof(Contact));
        }

        [HttpGet]
        public async Task<IActionResult> Contact()
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Contact), "Partner") });

            await SetUserHasPropertiesAsync(); // Thêm dòng này
            var pa = await _db.PartnerAccounts.FirstOrDefaultAsync(x => x.UserId == me.Id);
            if (pa == null) return RedirectToAction(nameof(Start));

            var vm = new PartnerContactViewModel
            {
                FirstName = pa.ContactFirstName ?? string.Empty,
                LastName = pa.ContactLastName ?? string.Empty,
                CountryCode = pa.PhoneCountryCode ?? "+84",
                Phone = pa.PhoneNumber ?? string.Empty
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact(PartnerContactViewModel vm)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Contact), "Partner") });
            if (!ModelState.IsValid) return View(vm);

            var pa = await _db.PartnerAccounts.FirstOrDefaultAsync(x => x.UserId == me.Id);
            if (pa == null) return RedirectToAction(nameof(Start));

            pa.ContactFirstName = vm.FirstName.Trim();
            pa.ContactLastName = vm.LastName.Trim();
            pa.PhoneCountryCode = vm.CountryCode;
            pa.PhoneNumber = vm.Phone.Trim();
            pa.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Onboarding));
        }

        [HttpGet]
        public async Task<IActionResult> Onboarding(int? propertyId = null)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Onboarding), "Partner") });

            await SetUserHasPropertiesAsync(); // Thêm dòng này
            
            // Thay đổi logic: tìm property theo ID hoặc tìm draft
            Property? property = null;
            if (propertyId.HasValue)
            {
                property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId.Value && p.UserId == me.Id);
            }
            
            if (property == null)
            {
                // Nếu không tìm thấy property theo ID, tìm draft
                property = await _db.Properties.Where(p => p.UserId == me.Id && p.IsDraft).OrderByDescending(p => p.Id).FirstOrDefaultAsync();
            }
            
            var vm = new PropertyStep1ViewModel();
            if (property != null)
            {
                vm.PropertyId = property.Id;
                vm.Name = property.Name;
                vm.LocalName = property.LocalName;
                vm.NoLocalDifferent = string.IsNullOrWhiteSpace(property.LocalName);
                vm.Type = property.Type;
                vm.CountryCode = property.CountryCode;
                vm.City = property.City;
                vm.AddressLine = property.AddressLine;
                vm.PostalCode = property.PostalCode;
                    // THÊM ?? string.Empty ĐỂ SỬA CẢNH BÁO
        vm.PropertyPhoneCountryCode = property.PropertyPhoneCountryCode ?? "+84";
        vm.PropertyPhoneNumber = property.PropertyPhoneNumber ?? string.Empty;
        vm.PicFirstName = property.PicFirstName ?? string.Empty;
        vm.PicLastName = property.PicLastName ?? string.Empty;
        vm.PicEmail = property.PicEmail ?? string.Empty;
        vm.PicPosition = property.PicPosition ?? string.Empty;
        vm.PicPhoneCountryCode = property.PicPhoneCountryCode ?? "+84";
        vm.PicPhoneNumber = property.PicPhoneNumber ?? string.Empty;
            }
            return View(vm);
        }

        // Thêm action để tạo property mới
        [HttpGet]
        public async Task<IActionResult> CreateNewProperty()
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(CreateNewProperty), "Partner") });

            await SetUserHasPropertiesAsync();
            
            // Đếm số properties hiện có để tạo tên duy nhất
            var existingCount = await _db.Properties.CountAsync(p => p.UserId == me.Id);
            var propertyNumber = existingCount + 1;
            
            // Tạo property mới
            var newProperty = new Property
            {
                UserId = me.Id,
                Name = $"Cơ sở lưu trú {propertyNumber}",
                Type = PropertyType.Hotel,
                CountryCode = "VN",
                City = "",
                AddressLine = "",
                IsDraft = true,
                Status = PropertyStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };
            
            _db.Properties.Add(newProperty);
            await _db.SaveChangesAsync();
            
            return RedirectToAction(nameof(Onboarding), new { propertyId = newProperty.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Onboarding(PropertyStep1ViewModel vm)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Onboarding), "Partner") });
            if (!ModelState.IsValid) return View(vm);

            Property? entity = null;
            if (vm.PropertyId.HasValue)
                entity = await _db.Properties.FirstOrDefaultAsync(p => p.Id == vm.PropertyId.Value && p.UserId == me.Id);

            if (entity == null)
            {
                // Tạo property mới - mỗi lần tạo sẽ tạo ra property riêng biệt
                entity = new Property { 
                     // Gán UserId của người dùng đang đăng nhập
        UserId = me.Id, 

        // Gán các thông tin khác từ form
        Name = vm.Name.Trim(),
        LocalName = vm.NoLocalDifferent ? null : vm.LocalName?.Trim(),
        Type = vm.Type,
        CountryCode = vm.CountryCode,
        City = vm.City.Trim(),
        AddressLine = vm.AddressLine.Trim(),
        PostalCode = vm.PostalCode?.Trim(),
        IsDraft = true,
        Status = PropertyStatus.Draft,
        PropertyPhoneCountryCode = vm.PropertyPhoneCountryCode,
        PropertyPhoneNumber = vm.PropertyPhoneNumber.Trim(),
        PicFirstName = vm.PicFirstName.Trim(),
        PicLastName = vm.PicLastName.Trim(),
        PicEmail = vm.PicEmail.Trim(),
        PicPosition = vm.PicPosition,
        PicPhoneCountryCode = vm.PicPhoneCountryCode,
        PicPhoneNumber = vm.PicPhoneNumber.Trim(),
        CreatedAt = DateTime.UtcNow

                };
                _db.Properties.Add(entity);
            }
            else
            {
// KHỐI LỆNH CẬP NHẬT
    entity.Name = vm.Name.Trim();
    entity.LocalName = vm.NoLocalDifferent ? null : vm.LocalName?.Trim();
    entity.Type = vm.Type;
    entity.CountryCode = vm.CountryCode;
    entity.City = vm.City.Trim();
    entity.AddressLine = vm.AddressLine.Trim();
    entity.PostalCode = vm.PostalCode?.Trim();
    entity.PropertyPhoneCountryCode = vm.PropertyPhoneCountryCode;
    entity.PropertyPhoneNumber = vm.PropertyPhoneNumber.Trim();
    entity.PicFirstName = vm.PicFirstName.Trim();
    entity.PicLastName = vm.PicLastName.Trim();
    entity.PicEmail = vm.PicEmail.Trim();
    entity.PicPosition = vm.PicPosition;
    entity.PicPhoneCountryCode = vm.PicPhoneCountryCode;
    entity.PicPhoneNumber = vm.PicPhoneNumber.Trim();
    entity.UpdatedAt = DateTime.UtcNow;

    // Nếu cơ sở đã được phê duyệt, chỉ cập nhật thông tin, KHÔNG đổi trạng thái/luồng duyệt
    if (entity.Status == PropertyStatus.Approved)
    {
        entity.IsDraft = false; // Đảm bảo không quay lại bản nháp
        // KHÔNG thay đổi entity.Status
    }
            }

            await _db.SaveChangesAsync();
            TempData["success"] = entity.Status == PropertyStatus.Approved
                ? "Đã cập nhật thông tin. Không cần duyệt lại."
                : "Đã lưu thông tin cơ sở lưu trú.";

            // Nếu đã Approved: quay lại trang chi tiết (không vào luồng hợp đồng/thanh toán nữa)
            if (entity.Status == PropertyStatus.Approved)
            {
                return RedirectToAction(nameof(PropertyDetail), new { id = entity.Id });
            }

            // Chưa Approved: tiếp tục quy trình như cũ
            return RedirectToAction(nameof(Payment), new { propertyId = entity.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Payment(int propertyId)
        {
            await SetUserHasPropertiesAsync(); // Thêm dòng này
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == _users.GetUserId(User));
            if (property == null) return NotFound();

            var vm = new PropertyPaymentViewModel
            {
                PropertyId = property.Id,
                PaymentMethod = property.PaymentMethod ?? PaymentMethodType.Card,
                BankName = property.BankName,
                BankBranch = property.BankBranch,
                BankAccountNumber = property.BankAccountNumber,
                BankAccountHolderName = property.BankAccountHolderName,
                AllowPaymentAtHotel = property.AllowPaymentAtHotel ?? false
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Payment(PropertyPaymentViewModel vm)
        {
            if (vm.PaymentMethod == PaymentMethodType.BankTransfer)
            {
                if (string.IsNullOrEmpty(vm.BankName)) ModelState.AddModelError(nameof(vm.BankName), "Vui lòng chọn ngân hàng.");
                if (string.IsNullOrEmpty(vm.BankBranch)) ModelState.AddModelError(nameof(vm.BankBranch), "Vui lòng nhập chi nhánh.");
                if (string.IsNullOrEmpty(vm.BankAccountNumber)) ModelState.AddModelError(nameof(vm.BankAccountNumber), "Vui lòng nhập số tài khoản.");
                if (string.IsNullOrEmpty(vm.BankAccountHolderName)) ModelState.AddModelError(nameof(vm.BankAccountHolderName), "Vui lòng nhập tên chủ tài khoản.");
            }
            if (!ModelState.IsValid) return View(vm);

            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == vm.PropertyId && p.UserId == _users.GetUserId(User));
            if (property == null) return NotFound();

            property.PaymentMethod = vm.PaymentMethod;
            property.AllowPaymentAtHotel = vm.AllowPaymentAtHotel;
            if (vm.PaymentMethod == PaymentMethodType.BankTransfer)
            {
                property.BankName = vm.BankName;
                property.BankBranch = vm.BankBranch;
                property.BankAccountNumber = vm.BankAccountNumber;
                property.BankAccountHolderName = vm.BankAccountHolderName;
            }
            else
            {
                property.BankName = null;
                property.BankBranch = null;
                property.BankAccountNumber = null;
                property.BankAccountHolderName = null;
            }
            property.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            TempData["success"] = "Đã lưu thông tin thanh toán.";
            return RedirectToAction(nameof(Contract), new { propertyId = vm.PropertyId });
        }

        [HttpGet]
        public async Task<IActionResult> Contract(int propertyId)
        {
            await SetUserHasPropertiesAsync(); // Thêm dòng này
            var property = await _db.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == _users.GetUserId(User));
            if (property == null) return NotFound();

            var vm = new PropertyContractViewModel
            {
                PropertyId = propertyId,
                LegalEntityName = property.LegalEntityName,
                LegalEntityAddress = property.LegalEntityAddress,
                TaxPayerName = property.TaxPayerName,
                TaxPayerAddress = property.TaxPayerAddress,
                IsSignatoryDirector = property.IsSignatoryDirector,
                SignatoryName = property.SignatoryName,
                SignatoryPosition = property.SignatoryPosition,
                SignatoryPhoneNumber = property.SignatoryPhoneNumber,
                SignatoryEmail = property.SignatoryEmail,
                ExistingBusinessLicensePath = property.BusinessLicensePath,
                ExistingSignatoryIdCardPath = property.SignatoryIdCardPath
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contract(PropertyContractViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == vm.PropertyId && p.UserId == _users.GetUserId(User));
            if (property == null) return NotFound();

            if (vm.BusinessLicenseFile != null)
            {
                string wwwRootPath = _hostEnvironment.WebRootPath;
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(vm.BusinessLicenseFile.FileName);
                string filePath = Path.Combine(wwwRootPath, @"uploads/licenses", fileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await vm.BusinessLicenseFile.CopyToAsync(fileStream); }
                property.BusinessLicensePath = @"/uploads/licenses/" + fileName;
            }

            if (vm.SignatoryIdCardFile != null)
            {
                string wwwRootPath = _hostEnvironment.WebRootPath;
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(vm.SignatoryIdCardFile.FileName);
                string filePath = Path.Combine(wwwRootPath, @"uploads/idcards", fileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create)) { await vm.SignatoryIdCardFile.CopyToAsync(fileStream); }
                property.SignatoryIdCardPath = @"/uploads/idcards/" + fileName;
            }

            property.LegalEntityName = vm.LegalEntityName;
            property.LegalEntityAddress = vm.LegalEntityAddress;
            property.TaxPayerName = vm.TaxPayerName;
            property.TaxPayerAddress = vm.TaxPayerAddress;
            property.IsSignatoryDirector = vm.IsSignatoryDirector;
            property.SignatoryName = vm.SignatoryName;
            property.SignatoryPosition = vm.SignatoryPosition;
            property.SignatoryPhoneNumber = vm.SignatoryPhoneNumber;
            property.SignatoryEmail = vm.SignatoryEmail;

            await _db.SaveChangesAsync();
            TempData["success"] = "Đã lưu thông tin hợp đồng.";
            return RedirectToAction(nameof(Review), new { propertyId = vm.PropertyId });
        }

        // GET: /Partner/Review/{propertyId}
[HttpGet]
public async Task<IActionResult> Review(int propertyId)
{
    var property = await _db.Properties
        .FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == _users.GetUserId(User));

    if (property == null) return NotFound();

    var vm = new ReviewViewModel
    {
        PropertyId = property.Id,
        // THÊM ?? string.Empty VÀO CÁC DÒNG SAU
        PropertyName = property.Name ?? string.Empty,
        PropertyAddress = $"{property.AddressLine}, {property.City}",
        PaymentMethod = property.PaymentMethod.ToString() ?? "Chưa rõ",
        LegalEntityName = property.LegalEntityName,
        LegalEntityAddress = property.LegalEntityAddress,
        SignatoryName = property.SignatoryName,
        SignatoryPosition = property.SignatoryPosition,
        SignatoryEmail = property.SignatoryEmail,
        SignatoryPhoneNumber = property.SignatoryPhoneNumber
    };

    return View(vm);
}

public class CreateRoomRequest
{
    public int PropertyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int MaxGuests { get; set; }
    public string? BedType { get; set; }
    public int? Size { get; set; }
    public bool SmokingAllowed { get; set; }
    public string ReturnTab { get; set; } = "rooms";
}
  // GET: /Partner/GenerateContractPdf/{propertyId}
[HttpGet]
public async Task<IActionResult> GenerateContractPdf(int propertyId)
{
    // Bỏ kiểm tra UserId vì action này được gọi bởi cả Partner và Staff.
    // Bảo mật đã được xử lý ở các trang gọi đến (Review.cshtml và Details.cshtml).
    var property = await _db.Properties.AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == propertyId);

    if (property == null)
    {
        return NotFound();
    }

    // NOTE: remove misplaced class definition here (moved below)

    // Tạo lại view model để truyền vào mẫu PDF
    var reviewModel = new ReviewViewModel
    {
        PropertyName = property.Name,
        PropertyAddress = $"{property.AddressLine}, {property.City}",
        LegalEntityName = property.LegalEntityName,
        LegalEntityAddress = property.LegalEntityAddress,
        SignatoryName = property.SignatoryName,
        SignatoryPosition = property.SignatoryPosition,
        SignatoryEmail = property.SignatoryEmail
    };

    var document = new ContractDocument(reviewModel, _localizer);
    byte[] pdfBytes = document.GeneratePdf();

    return File(pdfBytes, "application/pdf");
}



        // Action này sẽ được gọi khi người dùng nhấn nút "Gửi" cuối cùng
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Finalize(int propertyId)
{
    await SetUserHasPropertiesAsync(); // Thêm dòng này
    var property = await _db.Properties
        .FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == _users.GetUserId(User));

    if (property == null)
    {
        return NotFound();
    }

    // Đánh dấu cơ sở lưu trú này không còn là bản nháp nữa
    property.Status = PropertyStatus.Submitted;
    property.UpdatedAt = DateTime.UtcNow;

    await _db.SaveChangesAsync();

    // Chuyển hướng đến trang thông báo thành công
    return RedirectToAction(nameof(Success));
}

// Bỏ yêu cầu role "Partner", chỉ cần kiểm tra người dùng có property hay không
public async Task<IActionResult> MyProperties()
{
    var userId = _users.GetUserId(User);
    if (string.IsNullOrEmpty(userId))
    {
        return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(MyProperties), "Partner") });
    }
    
    await SetUserHasPropertiesAsync();
    
    // Kiểm tra xem người dùng có property nào không
    var allProperties = await _db.Properties
        .Where(p => p.UserId == userId)
        .ToListAsync();
        
    var myProperties = allProperties
        .OrderByDescending(p => p.CreatedAt)
        .ToList();
        
    // Load thumbnail ảnh đầu tiên cho mỗi property
    var propIds = myProperties.Select(p => p.Id).ToList();
    var thumbs = await _db.PropertyData
        .Where(pd => propIds.Contains(pd.PropertyId) && pd.PhotoPaths != null && pd.PhotoPaths != "")
        .Select(pd => new { pd.PropertyId, pd.PhotoPaths })
        .ToListAsync();
    var idToThumb = thumbs.ToDictionary(
        x => x.PropertyId,
        x => (x.PhotoPaths ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty
    );
    ViewBag.PropertyThumbs = idToThumb; // key: propertyId, value: first photo url
    
    return View(myProperties);
}

// Thêm action debug để kiểm tra
[HttpGet]
public async Task<IActionResult> DebugProperties()
{
    var userId = _users.GetUserId(User);
    var properties = await _db.Properties
        .Where(p => p.UserId == userId)
        .ToListAsync();
        
    var result = new
    {
        UserId = userId,
        TotalProperties = properties.Count,
        Properties = properties.Select(p => new
        {
            Id = p.Id,
            Name = p.Name,
            Status = p.Status.ToString(),
            IsDraft = p.IsDraft,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        }).ToList()
    };
        
    return Json(result);
}

// Thêm action test để tạo nhiều properties
[HttpGet]
public async Task<IActionResult> TestCreateMultipleProperties()
{
    var me = await _users.GetUserAsync(User);
    if (me == null) return RedirectToAction("Login", "Account");

    await SetUserHasPropertiesAsync();
    
    // Tạo 3 properties test
    for (int i = 1; i <= 3; i++)
    {
        var testProperty = new Property
        {
            UserId = me.Id,
            Name = $"Khách sạn Test {i}",
            Type = PropertyType.Hotel,
            CountryCode = "VN",
            City = $"Thành phố {i}",
            AddressLine = $"Địa chỉ {i}",
            IsDraft = true,
            Status = PropertyStatus.Draft,
            CreatedAt = DateTime.UtcNow.AddDays(-i) // Tạo ngày khác nhau
        };
        
        _db.Properties.Add(testProperty);
    }
    
    await _db.SaveChangesAsync();
    
    TempData["success"] = "Đã tạo 3 properties test. Hãy kiểm tra trang MyProperties.";
    return RedirectToAction(nameof(MyProperties));
}

// Action này chỉ để hiển thị trang thành công
[HttpGet]
public async Task<IActionResult> Success()
{
    await SetUserHasPropertiesAsync(); // Thay thế logic cũ bằng method mới
    return View();
}

        // Thêm action để kiểm tra database
        [HttpGet]
        public async Task<IActionResult> CheckDatabase()
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();
            
            var allProperties = await _db.Properties
                .Where(p => p.UserId == me.Id)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
                
            var result = new
            {
                UserId = me.Id,
                UserEmail = me.Email,
                TotalProperties = allProperties.Count,
                Properties = allProperties.Select(p => new
                {
                    Id = p.Id,
                    Name = p.Name,
                    Status = p.Status.ToString(),
                    IsDraft = p.IsDraft,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    City = p.City,
                    AddressLine = p.AddressLine
                }).ToList()
            };
                
            return Json(result);
        }

        // Action để hiển thị chi tiết property
        [HttpGet]
        public async Task<IActionResult> PropertyDetail(int id)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();
            
            var property = await _db.Properties
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == me.Id);
                
            if (property == null)
            {
                return NotFound("Không tìm thấy cơ sở lưu trú này.");
            }
            
            var viewModel = new PropertyDetailViewModel
            {
                Property = property,
                RegistrationNumber = $"REG-{property.Id:D6}",
                CurrentStep = GetCurrentStep(property.Status),
                TotalSteps = 3
            };
            
            return View(viewModel);
        }

        // Xóa hoàn toàn một cơ sở lưu trú và toàn bộ dữ liệu liên quan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProperty(int id)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.UserId == me.Id);
            if (property == null)
            {
                TempData["error"] = "Không tìm thấy cơ sở lưu trú.";
                return RedirectToAction(nameof(MyProperties));
            }

            using var txn = await _db.Database.BeginTransactionAsync();
            try
            {
                // Xóa giá theo ngày và giá cơ bản
                var dailyRates = _db.RoomDailyRates.Where(r => r.PropertyId == id);
                _db.RoomDailyRates.RemoveRange(dailyRates);

                var basePrices = _db.RoomPrices.Where(r => r.PropertyId == id);
                _db.RoomPrices.RemoveRange(basePrices);

                // Lấy danh sách phòng thuộc property
                var roomIds = await _db.Rooms
                    .Where(r => r.PropertyId == id)
                    .Select(r => r.Id)
                    .ToListAsync();

                if (roomIds.Count > 0)
                {
                    _db.RoomBeds.RemoveRange(_db.RoomBeds.Where(b => roomIds.Contains(b.RoomId)));
                    _db.RoomAmenities.RemoveRange(_db.RoomAmenities.Where(a => roomIds.Contains(a.RoomId)));
                    _db.RoomPhotos.RemoveRange(_db.RoomPhotos.Where(p => roomIds.Contains(p.RoomId)));
                    _db.Rooms.RemoveRange(_db.Rooms.Where(r => r.PropertyId == id));
                }

                // Xóa gói giá và dữ liệu property data
                _db.PricePackages.RemoveRange(_db.PricePackages.Where(pp => pp.PropertyId == id));
                _db.PropertyData.RemoveRange(_db.PropertyData.Where(pd => pd.PropertyId == id));

                // Cuối cùng xóa property
                _db.Properties.Remove(property);

                await _db.SaveChangesAsync();
                await txn.CommitAsync();

                TempData["success"] = "Đã xóa cơ sở lưu trú.";
            }
            catch
            {
                await txn.RollbackAsync();
                TempData["error"] = "Xóa thất bại. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(MyProperties));
        }

        // Helper method để lấy step hiện tại
        private int GetCurrentStep(PropertyStatus status)
        {
            return status switch
            {
                PropertyStatus.Draft => 1,
                PropertyStatus.Submitted => 1,
                PropertyStatus.UnderReview => 2,
                PropertyStatus.Approved => 3,
                PropertyStatus.Rejected => 3,
                _ => 1
            };
        }

        // Action để hiển thị và xử lý thông tin chi tiết property
        [HttpGet]
        public async Task<IActionResult> PropertyData(int? propertyId, string? tab = "property")
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();
            
            // Nếu không có propertyId, redirect về MyProperties
            if (!propertyId.HasValue)
            {
                return RedirectToAction("MyProperties", "Partner");
            }
            
            var property = await _db.Properties
                .FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == me.Id);
                
            if (property == null)
            {
                return NotFound("Không tìm thấy cơ sở lưu trú này.");
            }

            // Kiểm tra xem property đã có PropertyData chưa
            var propertyData = await _db.PropertyData
                .FirstOrDefaultAsync(pd => pd.PropertyId == propertyId);

            var viewModel = new PropertyDataViewModel
            {
                PropertyId = property.Id,
                PropertyName = property.Name,
                RegistrationNumber = $"REG-{property.Id:D6}",
                CurrentTab = tab,
                // Các thuộc tính khác sẽ được khởi tạo từ PropertyData nếu có
            };

            // Nếu đã có PropertyData, load dữ liệu cũ
            if (propertyData != null)
            {
                Console.WriteLine($"=== Loading PropertyData ===");
                Console.WriteLine($"PropertyData.StarRating from DB: {propertyData.StarRating}");
                
                viewModel.CheckInTime = propertyData.CheckInTime;
                viewModel.CheckOutTime = propertyData.CheckOutTime;
                viewModel.IsReception24Hours = propertyData.IsReception24Hours;
                viewModel.NumberOfRooms = propertyData.NumberOfRooms;
                viewModel.RoomDescription = propertyData.RoomDescription;
                viewModel.StarRating = propertyData.StarRating;
                
                Console.WriteLine($"ViewModel.StarRating after assignment: {viewModel.StarRating}");
                viewModel.PhotoCategoriesJson = propertyData.PhotoCategoriesJson;
                // Khôi phục URL ảnh đã lưu để render lại khi quay lại
                if (!string.IsNullOrWhiteSpace(propertyData.PhotoPaths))
                {
                    viewModel.SavedPhotoUrls = propertyData.PhotoPaths
                        .Split('|', StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                }
                
                // Các thuộc tính amenities
                viewModel.HasSmokingArea = propertyData.HasSmokingArea;
                viewModel.HasAccessibleBathroom = propertyData.HasAccessibleBathroom;
                viewModel.HasElevator = propertyData.HasElevator;
                viewModel.HasCafe = propertyData.HasCafe;
                viewModel.HasRestaurant = propertyData.HasRestaurant;
                viewModel.HasBar = propertyData.HasBar;
                viewModel.HasFrontDesk = propertyData.HasFrontDesk;
                viewModel.HasExpressCheckIn = propertyData.HasExpressCheckIn;
                viewModel.HasConcierge = propertyData.HasConcierge;
                viewModel.HasExpressCheckOut = propertyData.HasExpressCheckOut;
                viewModel.HasPublicWifi = propertyData.HasPublicWifi;
                viewModel.HasAccessibleParking = propertyData.HasAccessibleParking;
                viewModel.HasParkingArea = propertyData.HasParkingArea;
                viewModel.HasLaundryService = propertyData.HasLaundryService;
                viewModel.Has24HourSecurity = propertyData.Has24HourSecurity;
                viewModel.HasLuggageStorage = propertyData.HasLuggageStorage;
                viewModel.HasAirportTransfer = propertyData.HasAirportTransfer;
            }
            
            // Load Rooms với đầy đủ thông tin
            var rooms = await _db.Rooms
                .Include(r => r.Beds)
                .Where(r => r.PropertyId == propertyId)
                .ToListAsync();

            viewModel.Rooms = rooms.Select(r => new RoomItemVm
            {
                Id = r.Id,
                Name = r.Name,
                RoomType = r.RoomType,
                Size = r.Size,
                SizeUnit = r.SizeUnit,
                Quantity = r.Quantity,
                MaxGuests = r.CapacityAdults + r.CapacityChildren,
                CapacityAdults = r.CapacityAdults,
                CapacityChildren = r.CapacityChildren,
                AllowChildren = r.AllowChildren,
                AllowExtraBed = r.AllowExtraBed,
                SecurityDeposit = r.SecurityDeposit,
                Beds = r.Beds.SelectMany(b => b.GetAllBedItems()).Select(b => new BedItemVm
                {
                    Type = b.Type,
                    Count = b.Count,
                    BedroomIndex = b.BedroomIndex
                }).ToList()
            }).ToList();

            // Load PricePackage nếu có (lấy package đầu tiên làm mặc định)
            var pricePackage = await _db.PricePackages
                .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
            
            if (pricePackage != null)
            {
                viewModel.PricePackage = new PricePackageViewModel
                {
                    Id = pricePackage.Id,
                    CancellationPolicy = pricePackage.CancellationPolicy,
                    CancellationPolicyDisplayName = pricePackage.CancellationPolicyDisplayName,
                    BreakfastIncluded = pricePackage.BreakfastIncluded,
                    BreakfastDisplayName = pricePackage.BreakfastDisplayName,
                    CreatedAt = pricePackage.CreatedAt
                };
            }

            // Load tất cả PricePackages để hiển thị trong dropdown
            var allPricePackages = await _db.PricePackages
                .Where(p => p.PropertyId == propertyId)
                .ToListAsync();
            ViewBag.AllPricePackages = allPricePackages;

            // Load tất cả RoomPrices với PricePackageId
            var roomPrices = await _db.RoomPrices
                .Include(rp => rp.PricePackage)
                .Where(rp => rp.PropertyId == propertyId)
                .OrderBy(rp => rp.Id) // Order by ID để đảm bảo thứ tự
                .ToListAsync();
            
            Console.WriteLine($"=== PropertyData GET: Loaded RoomPrices ===");
            Console.WriteLine($"Total RoomPrices: {roomPrices.Count}");
            foreach (var rp in roomPrices)
            {
                Console.WriteLine($"  RoomPrice {rp.Id}: Room {rp.RoomId}, Amount={rp.Amount}, PackageId={rp.PricePackageId}");
            }
            
            // Tạo dictionary: RoomId -> List<RoomPrice>
            var roomPriceMap = new Dictionary<int, List<Models.RoomPrice>>();
            foreach (var rp in roomPrices)
            {
                if (!roomPriceMap.ContainsKey(rp.RoomId))
                {
                    roomPriceMap[rp.RoomId] = new List<Models.RoomPrice>();
                }
                roomPriceMap[rp.RoomId].Add(rp);
            }
            ViewBag.RoomPriceMap = roomPriceMap;

            // Load current room prices (giữ lại để tương thích với code cũ)
            // Lấy giá đầu tiên cho mỗi RoomId (có thể có nhiều RoomPrice cho cùng RoomId)
            var priceMap = await _db.RoomPrices
                .Where(p => p.PropertyId == propertyId)
                .GroupBy(p => p.RoomId)
                .ToDictionaryAsync(g => g.Key, g => g.First().Amount);

            ViewBag.RoomPriceMapLegacy = priceMap; // Đổi tên để tránh conflict

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> EditRoom(int propertyId, int id)
        {
            var room = await _db.Rooms
                .Include(r => r.Beds)
                .Include(r => r.Amenities)
                .Include(r => r.Photos)
                .FirstOrDefaultAsync(r => r.Id == id && r.PropertyId == propertyId);
            if (room == null) return NotFound();

            // log data room
            Console.WriteLine("=== data room photo ===");
            var photoData = room.Photos.OrderBy(p => p.SortOrder).Select(p => new {
                Id = p.Id,
                Url = p.Url,
                Category = p.Category,
                SortOrder = p.SortOrder
            }).ToList();
            Console.WriteLine(JsonSerializer.Serialize(photoData, new JsonSerializerOptions { WriteIndented = true }));

            // Debug dữ liệu từ database
            Console.WriteLine("=== EDITROOM DEBUG ===");
            Console.WriteLine($"Room.IsSingleBedroom: {room.IsSingleBedroom}");
            Console.WriteLine($"Room.Beds count: {room.Beds.Count}");
            foreach (var bed in room.Beds)
            {
                Console.WriteLine($"DB Bed: Types=[{string.Join(", ", bed.Types)}], Counts=[{string.Join(", ", bed.Counts)}], BedroomIndex={bed.BedroomIndex}");
            }

            var vm = new ViewModels.Rooms.RoomCreateViewModel
            {
                PropertyId = propertyId,
                RoomId = room.Id,
                RoomType = room.RoomType,
                RoomName = room.Name,
                SizeNumber = room.Size,
                SizeUnit = room.SizeUnit,
                SmokingAllowed = room.SmokingAllowed,
                Quantity = room.Quantity,
                IsSingleBedroom = room.IsSingleBedroom,
                CapacityAdults = room.CapacityAdults,
                CapacityChildren = room.CapacityChildren,
                AllowChildren = room.AllowChildren,
                AllowExtraBed = room.AllowExtraBed,
                SecurityDeposit = room.SecurityDeposit,
                // Tạo dữ liệu giường ngủ dựa trên loại phòng
                Beds = room.IsSingleBedroom 
                    ? room.Beds.SelectMany(b => b.GetAllBedItems()).Select(b => new ViewModels.Rooms.BedItem { Type = b.Type, Count = b.Count, BedroomIndex = 0 }).ToList()
                    : new List<ViewModels.Rooms.BedItem>(),
                Bedrooms = room.IsSingleBedroom 
                    ? new List<ViewModels.Rooms.BedroomItem>()
                    : room.Beds
                        .GroupBy(b => b.BedroomIndex)
                        .OrderBy(g => g.Key)
                        .Select(g => new ViewModels.Rooms.BedroomItem 
                        { 
                            Beds = g.SelectMany(b => b.GetAllBedItems()).Select(b => new ViewModels.Rooms.BedItem 
                            { 
                                Type = b.Type, 
                                Count = b.Count,
                                BedroomIndex = b.BedroomIndex
                            }).ToList() 
                        }).ToList(),
                SelectedAmenities = room.Amenities.Select(a => a.Name).ToList(),
                SavedPhotoUrls = room.Photos.OrderBy(p => p.SortOrder).Select(p => p.Url).ToList(),
                PhotoCategories = room.Photos.OrderBy(p => p.SortOrder).Select(p => p.Category).ToList(),
                SavedPhotoData = photoData // Thêm photoData object
            };

            return View("RoomData", vm);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRoom(int propertyId, int id)
        {
            var room = await _db.Rooms
                .Include(r => r.Beds)
                .Include(r => r.Amenities)
                .Include(r => r.Photos)
                .FirstOrDefaultAsync(r => r.Id == id && r.PropertyId == propertyId);
            
            if (room == null) return NotFound();

            // Xóa các bản ghi liên quan
            _db.RoomBeds.RemoveRange(room.Beds);
            _db.RoomAmenities.RemoveRange(room.Amenities);
            _db.RoomPhotos.RemoveRange(room.Photos);
            
            // Xóa phòng
            _db.Rooms.Remove(room);
            
            await _db.SaveChangesAsync();
            
            return RedirectToAction("PropertyData", new { propertyId = propertyId, tab = "rooms" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRoomDuplicate(int propertyId, int id)
        {
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id && r.PropertyId == propertyId);
            if (room != null)
            {
                _db.Rooms.Remove(room);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(PropertyData), new { propertyId, tab = "rooms" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PropertyData(PropertyDataViewModel viewModel)
        {
            Console.WriteLine("===DEBUG===photos");
            Console.WriteLine($"PropertyId: {viewModel?.PropertyId}");
            Console.WriteLine($"ModelState.IsValid: {ModelState.IsValid}");
            Console.WriteLine($"Request.ContentType: {Request.ContentType}");
            Console.WriteLine($"Request.Form.Count: {Request.Form.Count}");
            Console.WriteLine($"Request.Form.Files.Count: {Request.Form.Files.Count}");
            
            // Log ModelState errors
            if (!ModelState.IsValid)
            {
                Console.WriteLine("=== MODELSTATE ERRORS ===");
                foreach (var error in ModelState)
                {
                    if (error.Value.Errors.Count > 0)
                    {
                        Console.WriteLine($"Key: {error.Key}");
                        foreach (var err in error.Value.Errors)
                        {
                            Console.WriteLine($"  Error: {err.ErrorMessage}");
                        }
                    }
                }
            }
            
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ ModelState is invalid, returning View with errors");
                await SetUserHasPropertiesAsync();
                return View(viewModel);
            }

            // Kiểm tra xem property có tồn tại và thuộc về user không
            var property = await _db.Properties
                .FirstOrDefaultAsync(p => p.Id == viewModel.PropertyId && p.UserId == me.Id);
                
            if (property == null)
            {
                return NotFound("Không tìm thấy cơ sở lưu trú này.");
            }

            // Tìm hoặc tạo mới PropertyData
            var propertyData = await _db.PropertyData
                .FirstOrDefaultAsync(pd => pd.PropertyId == viewModel.PropertyId);

            if (propertyData == null)
            {
                propertyData = new PropertyData
                {
                    PropertyId = viewModel.PropertyId,
                    CreatedAt = DateTime.UtcNow
                };
                _db.PropertyData.Add(propertyData);
            }

            // Cập nhật dữ liệu
            propertyData.CheckInTime = viewModel.CheckInTime;
            propertyData.CheckOutTime = viewModel.CheckOutTime;
            propertyData.IsReception24Hours = viewModel.IsReception24Hours;
            propertyData.NumberOfRooms = viewModel.NumberOfRooms;
            propertyData.RoomDescription = viewModel.RoomDescription;
            
            // Xử lý StarRating: Request.Form["StarRating"] có thể trả về nhiều giá trị (tất cả radio buttons)
            // Cần lấy giá trị cuối cùng (radio được checked) hoặc giá trị duy nhất
            if (Request.Form.ContainsKey("StarRating"))
            {
                var starRatingValues = Request.Form["StarRating"].ToString();
                Console.WriteLine($"StarRating from Request.Form (raw): '{starRatingValues}'");
                
                // Nếu có nhiều giá trị (ví dụ: "1,2,3,4,5"), lấy giá trị cuối cùng (radio được checked)
                string starRatingValue;
                if (starRatingValues.Contains(','))
                {
                    var values = starRatingValues.Split(',');
                    starRatingValue = values[values.Length - 1].Trim(); // Lấy giá trị cuối cùng
                    Console.WriteLine($"StarRating has multiple values, using last one: '{starRatingValue}'");
                }
                else
                {
                    starRatingValue = starRatingValues.Trim();
                }
                
                if (string.IsNullOrWhiteSpace(starRatingValue))
                {
                    propertyData.StarRating = null;
                    Console.WriteLine("StarRating set to null (empty string)");
                }
                else if (int.TryParse(starRatingValue, out int rating) && rating >= 1 && rating <= 5)
                {
                    propertyData.StarRating = rating;
                    Console.WriteLine($"StarRating set to {rating}");
                }
                else
                {
                    propertyData.StarRating = viewModel.StarRating; // Fallback to viewModel value
                    Console.WriteLine($"StarRating fallback to viewModel value: {viewModel.StarRating}");
                }
            }
            else
            {
            propertyData.StarRating = viewModel.StarRating;
                Console.WriteLine($"StarRating from viewModel (form key not found): {viewModel.StarRating}");
            }
            
            Console.WriteLine($"Final StarRating value: {propertyData.StarRating}");
            
            // Cập nhật amenities
            propertyData.HasSmokingArea = viewModel.HasSmokingArea;
            propertyData.HasAccessibleBathroom = viewModel.HasAccessibleBathroom;
            propertyData.HasElevator = viewModel.HasElevator;
            propertyData.HasCafe = viewModel.HasCafe;
            propertyData.HasRestaurant = viewModel.HasRestaurant;
            propertyData.HasBar = viewModel.HasBar;
            propertyData.HasFrontDesk = viewModel.HasFrontDesk;
            propertyData.HasExpressCheckIn = viewModel.HasExpressCheckIn;
            propertyData.HasConcierge = viewModel.HasConcierge;
            propertyData.HasExpressCheckOut = viewModel.HasExpressCheckOut;
            propertyData.HasPublicWifi = viewModel.HasPublicWifi;
            propertyData.HasAccessibleParking = viewModel.HasAccessibleParking;
            propertyData.HasParkingArea = viewModel.HasParkingArea;
            propertyData.HasLaundryService = viewModel.HasLaundryService;
            propertyData.Has24HourSecurity = viewModel.Has24HourSecurity;
            propertyData.HasLuggageStorage = viewModel.HasLuggageStorage;
            propertyData.HasAirportTransfer = viewModel.HasAirportTransfer;

            // Lưu thông tin ảnh - sẽ được cập nhật sau khi xử lý manifest

            // Debug: Log thông tin về files được upload
            Console.WriteLine($"=== DEBUG PHOTO UPLOAD ===");
            Console.WriteLine($"PropertyPhotos count: {viewModel.PropertyPhotos?.Count ?? 0}");
            if (viewModel.PropertyPhotos != null)
            {
                for (int i = 0; i < viewModel.PropertyPhotos.Count; i++)
                {
                    var file = viewModel.PropertyPhotos[i];
                    Console.WriteLine($"  File {i}: {file?.FileName} (Size: {file?.Length} bytes)");
                }
            }
            Console.WriteLine($"PhotoManifestJson: {viewModel.PhotoManifestJson}");
            Console.WriteLine($"DeletedPhotoUrlsJson: {viewModel.DeletedPhotoUrlsJson}");

            // Manifest: xử lý xóa / cập nhật thứ tự + category / thêm ảnh mới
            var existingUrls = string.IsNullOrWhiteSpace(propertyData.PhotoPaths)
                ? new List<string>()
                : propertyData.PhotoPaths.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

            Console.WriteLine("=== List ảnh cũ (existing photos from DB) ===");
            Console.WriteLine($"Count: {existingUrls.Count}");
            for (int i = 0; i < existingUrls.Count; i++)
            {
                Console.WriteLine($"  Existing[{i}]: {existingUrls[i]}");
            }

            var deletedUrls = new List<string>();
            try
            {
                if (!string.IsNullOrWhiteSpace(viewModel.DeletedPhotoUrlsJson))
                {
                    deletedUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(viewModel.DeletedPhotoUrlsJson) ?? new List<string>();
                }
            }
            catch { }

            Console.WriteLine("=== List ảnh bị xóa (deleted photos) ===");
            Console.WriteLine($"Count: {deletedUrls.Count}");
            for (int i = 0; i < deletedUrls.Count; i++)
            {
                Console.WriteLine($"  Deleted[{i}]: {deletedUrls[i]}");
            }

            // Xóa URL cũ (và có thể xóa file vật lý)
            if (deletedUrls.Count > 0)
            {
                existingUrls = existingUrls.Where(u => !deletedUrls.Contains(u)).ToList();
            }
            
            Console.WriteLine($"=== Existing URLs after deletion: {existingUrls.Count} ===");

            // Build danh sách cuối cùng theo manifest
            var manifestItems = new List<(int? NewIndex, string? Url, string Category, int Order)>();
            var categories = new List<string>();
            try
            {
                var raw = string.IsNullOrWhiteSpace(viewModel.PhotoManifestJson) ? "[]" : viewModel.PhotoManifestJson;
                Console.WriteLine($"=== Manifest JSON ===");
                Console.WriteLine($"Raw manifest: {raw}");
                
                var dicts = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.Nodes.JsonObject>>(raw) ?? new();
                Console.WriteLine($"=== Manifest Items (parsed) ===");
                Console.WriteLine($"Count: {dicts.Count}");
                
                foreach (var d in dicts)
                {
                    int order = d["order"]?.GetValue<int>() ?? 0;
                    string category = d["category"]?.GetValue<string>() ?? "others";
                    string? url = d["url"]?.GetValue<string>();
                    int? newIdx = d["newIndex"]?.GetValue<int?>();
                    manifestItems.Add((newIdx, url, category, order));
                    
                    if (newIdx.HasValue)
                    {
                        Console.WriteLine($"  Manifest[{order}]: NEW photo (newIndex={newIdx}, category={category})");
                    }
                    else if (!string.IsNullOrEmpty(url))
                    {
                        Console.WriteLine($"  Manifest[{order}]: EXISTING photo (url={url}, category={category})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing manifest: {ex.Message}");
            }

            // Thư mục lưu file
            var wwwRootPath2 = _hostEnvironment.WebRootPath;
            var uploadsDir2 = Path.Combine(wwwRootPath2, "uploads", "properties", viewModel.PropertyId.ToString());
            Directory.CreateDirectory(uploadsDir2);

            // Tìm order lớn nhất để tạo mảng đủ lớn
            var maxOrder = manifestItems.Any() ? manifestItems.Max(m => m.Order) : -1;
            var finalUrls = new List<string>();
            var finalCategories = new List<string>();

            // Sắp xếp manifest theo order để xử lý đúng thứ tự
            var sortedManifest = manifestItems.OrderBy(m => m.Order).ToList();
            
            // Debug: Log thông tin files nhận được từ cả model binding và Request.Form.Files
            Console.WriteLine($"=== DEBUG PHOTO PROCESSING ===");
            Console.WriteLine($"Total files from model binding: {viewModel.PropertyPhotos?.Count ?? 0}");
            Console.WriteLine($"Total files from Request.Form.Files: {Request.Form.Files.Count}");
            
            // Lấy files trực tiếp từ Request.Form.Files thay vì dựa vào model binding
            var allUploadedFiles = Request.Form.Files.Where(f => f.Name == "PropertyPhotos").ToList();
            
            Console.WriteLine("=== List ảnh mới (new photos received from client) ===");
            Console.WriteLine($"Count from Request.Form.Files: {allUploadedFiles.Count}");
            for (int i = 0; i < allUploadedFiles.Count; i++)
            {
                var f = allUploadedFiles[i];
                Console.WriteLine($"  New[{i}]: {f.FileName} (Size: {f.Length} bytes, ContentType: {f.ContentType})");
            }
            
            Console.WriteLine($"Count from model binding: {viewModel.PropertyPhotos?.Count ?? 0}");
            if (viewModel.PropertyPhotos != null)
            {
                for (int i = 0; i < viewModel.PropertyPhotos.Count; i++)
                {
                    var f = viewModel.PropertyPhotos[i];
                    Console.WriteLine($"  ModelBinding[{i}]: {f?.FileName} (Size: {f?.Length} bytes, ContentType: {f?.ContentType})");
                }
            }
            
            // Sử dụng files từ Request thay vì từ model binding nếu model binding không đúng
            if (allUploadedFiles.Count > (viewModel.PropertyPhotos?.Count ?? 0))
            {
                Console.WriteLine($"⚠️ WARNING: Request has {allUploadedFiles.Count} files but model binding only has {viewModel.PropertyPhotos?.Count ?? 0}");
                Console.WriteLine($"Using files from Request.Form.Files instead of model binding");
            }
            
            if (viewModel.PropertyPhotos != null)
            {
                for (int i = 0; i < viewModel.PropertyPhotos.Count; i++)
                {
                    var f = viewModel.PropertyPhotos[i];
                    Console.WriteLine($"  Model File[{i}]: {f?.FileName} (Size: {f?.Length} bytes, ContentType: {f?.ContentType})");
                }
            }
            Console.WriteLine($"Manifest items count: {manifestItems.Count}");
            foreach (var m in sortedManifest)
            {
                Console.WriteLine($"  Manifest: Order={m.Order}, NewIndex={m.NewIndex}, Url={m.Url}, Category={m.Category}");
            }
            
            for (int i = 0; i <= maxOrder; i++)
            {
                var it = sortedManifest.FirstOrDefault(m => m.Order == i);
                if (it.Url != null || it.NewIndex.HasValue)
                {
                if (!string.IsNullOrEmpty(it.Url))
                {
                        // Ảnh cũ: giữ nguyên URL
                        finalUrls.Add(it.Url);
                        finalCategories.Add(it.Category);
                        Console.WriteLine($"  Processed existing photo at order {i}: {it.Url}");
                }
                else if (it.NewIndex.HasValue)
                {
                        // Sử dụng files từ Request.Form.Files thay vì model binding để đảm bảo lấy đúng tất cả files
                        var fileList = allUploadedFiles.Count > 0 ? allUploadedFiles : (viewModel.PropertyPhotos ?? new List<IFormFile>());
                        Console.WriteLine($"  Trying to get file at index {it.NewIndex.Value} from {fileList.Count} files (source: {(allUploadedFiles.Count > 0 ? "Request.Form.Files" : "Model binding")})");
                        
                        if (it.NewIndex.Value >= 0 && it.NewIndex.Value < fileList.Count)
                        {
                            var file = fileList[it.NewIndex.Value];
                    if (file != null && file.Length > 0 && file.ContentType.StartsWith("image/"))
                    {
                        var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(uploadsDir2, fileName);
                        using (var fs = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fs);
                        }
                                var photoUrl = $"/uploads/properties/{viewModel.PropertyId}/{fileName}";
                                finalUrls.Add(photoUrl);
                                finalCategories.Add(it.Category);
                                Console.WriteLine($"  ✅ Processed new photo at order {i} (index {it.NewIndex.Value}): {photoUrl}");
                            }
                            else
                            {
                                Console.WriteLine($"  ❌ File at index {it.NewIndex.Value} is null or invalid");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  ❌ Index {it.NewIndex.Value} is out of range (0-{fileList.Count - 1})");
                            Console.WriteLine($"  Available files: {string.Join(", ", fileList.Select((f, idx) => $"[{idx}]{f.FileName}"))}");
                        }
                    }
                }
            }
            
            Console.WriteLine($"Final URLs count: {finalUrls.Count}");
            Console.WriteLine($"Final Categories count: {finalCategories.Count}");

            // Nếu có manifest, cập nhật theo manifest (kể cả khi rỗng - người dùng đã xóa hết ảnh)
            if (!string.IsNullOrWhiteSpace(viewModel.PhotoManifestJson))
            {
                if (finalUrls.Any())
                {
                    propertyData.PhotoPaths = string.Join('|', finalUrls);
                    propertyData.PhotoCategoriesJson = System.Text.Json.JsonSerializer.Serialize(finalCategories);
                }
                else
                {
                    // Manifest rỗng = người dùng đã xóa hết ảnh
                    propertyData.PhotoPaths = string.Empty;
                    propertyData.PhotoCategoriesJson = "[]";
                    Console.WriteLine("Info: Manifest is empty, clearing all photos");
                }
            }
            // Nếu không có manifest, giữ nguyên danh sách cũ (không thay đổi)

            propertyData.UpdatedAt = DateTime.UtcNow;

            // Log trước khi save
            Console.WriteLine($"=== Before SaveAsync ===");
            Console.WriteLine($"PropertyData.StarRating: {propertyData.StarRating}");
            Console.WriteLine($"PropertyData.Id: {propertyData.Id}");
            Console.WriteLine($"PropertyData.PropertyId: {propertyData.PropertyId}");

            await _db.SaveChangesAsync();

            // Verify sau khi save
            var savedData = await _db.PropertyData.FirstOrDefaultAsync(pd => pd.Id == propertyData.Id);
            Console.WriteLine($"=== After SaveAsync ===");
            Console.WriteLine($"Saved PropertyData.StarRating: {savedData?.StarRating}");

            TempData["success"] = "Thông tin cơ sở lưu trú đã được lưu thành công!";
            
            // Chuyển hướng đến tab "Cấu hình phòng" thay vì quay về PropertyDetail
            return RedirectToAction(nameof(PropertyData), new { propertyId = viewModel.PropertyId, tab = "rooms" });
        }

        [HttpGet]
        public async Task<IActionResult> RegisterNewProperty()
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            // Tạo property mới
            var existingCount = await _db.Properties.CountAsync(p => p.UserId == me.Id);
            var propertyNumber = existingCount + 1;
            
            var newProperty = new Property
            {
                UserId = me.Id,
                Name = $"Cơ sở lưu trú {propertyNumber}",
                Type = PropertyType.Hotel,
                CountryCode = "VN",
                City = "",
                AddressLine = "",
                IsDraft = true,
                Status = PropertyStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            _db.Properties.Add(newProperty);
            await _db.SaveChangesAsync();

            // Redirect đến Onboarding với property mới
            return RedirectToAction("Onboarding", new { propertyId = newProperty.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRoom(CreateRoomRequest req)
        {
            // Ở đây minh họa lưu tối thiểu: bạn có thể thay bằng bảng Rooms thực tế
            // Hiện chưa có bảng Rooms, nên chỉ redirect lại tab rooms để hoàn thiện UI flow
            TempData["success"] = "Tạo phòng thành công (demo UI).";
            return RedirectToAction(nameof(PropertyData), new { propertyId = req.PropertyId, tab = req.ReturnTab });
        }

        // GET: tạo/sửa phòng
        [HttpGet]
        public async Task<IActionResult> RoomData(int propertyId, int? roomId)
        {
            var prop = await _db.Properties.Where(p => p.Id == propertyId)
                .Select(p => new { p.Name })
                .FirstOrDefaultAsync();

            var vm = new ViewModels.Rooms.RoomCreateViewModel
            {
                PropertyId = propertyId,
                RoomId = roomId ?? 0,
                PropertyName = prop?.Name ?? $"Cơ sở lưu trú {propertyId}",
                RegistrationNumber = $"REG-{propertyId:D6}",
                AmenityGroups = new Dictionary<string, string[]>
                {
                    [_localizer["IntegratedAmenities"].Value] = new[] { 
                        _localizer["BalconyTerrace"].Value, 
                        _localizer["ConnectingRooms"].Value, 
                        _localizer["PrivatePool"].Value 
                    },
                    [_localizer["RoomAmenities"].Value] = new[] { 
                        _localizer["AirConditioning"].Value, 
                        _localizer["Desk"].Value, 
                        _localizer["Microwave"].Value, 
                        _localizer["IroningFacilities"].Value, 
                        _localizer["TV"].Value,
                        _localizer["Minibar"].Value, 
                        _localizer["HairDryer"].Value, 
                        _localizer["Wifi"].Value, 
                        _localizer["WashingMachine"].Value, 
                        _localizer["SharedBathroom"].Value,
                        _localizer["Refrigerator"].Value, 
                        _localizer["CoffeeTeaMaker"].Value 
                    },
                    [_localizer["Bathroom"].Value] = new[] { 
                        _localizer["Toiletries"].Value, 
                        _localizer["Bathrobe"].Value, 
                        _localizer["Bathtub"].Value, 
                        _localizer["Shower"].Value, 
                        _localizer["PrivateBathroom"].Value, 
                        _localizer["HotWater"].Value 
                    }
                }
            };
            return View(vm);
        }

        // POST: lưu phòng rồi quay lại tab Rooms (demo UI)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoomData(ViewModels.Rooms.RoomCreateViewModel m)
        {
            Console.WriteLine("=== BẮT ĐẦU XỬ LÝ ROOMDATA POST ===");
            Console.WriteLine($"PropertyId: {m.PropertyId}");
            Console.WriteLine($"RoomId: {m.RoomId}");
            Console.WriteLine($"RoomType: {m.RoomType}");
            Console.WriteLine($"RoomName: {m.RoomName}");
            Console.WriteLine($"SizeNumber: {m.SizeNumber}");
            Console.WriteLine($"Quantity: {m.Quantity}");
            Console.WriteLine($"CapacityAdults: {m.CapacityAdults}");
            Console.WriteLine($"CapacityChildren: {m.CapacityChildren}");
            Console.WriteLine($"SecurityDeposit: {m.SecurityDeposit}");
            Console.WriteLine($"IsSingleBedroom: {m.IsSingleBedroom}");
            
            // LOG FORM DATA CHI TIẾT
            Console.WriteLine("=== FORM DATA CHI TIẾT ===");
            Console.WriteLine($"Beds count: {m.Beds?.Count ?? 0}");
            if (m.Beds != null && m.Beds.Any())
            {
                for (int i = 0; i < m.Beds.Count; i++)
                {
                    var bed = m.Beds[i];
                    Console.WriteLine($"  Beds[{i}]: Type='{bed.Type}', Count={bed.Count}, BedroomIndex={bed.BedroomIndex}");
                }
            }
            
            Console.WriteLine($"Bedrooms count: {m.Bedrooms?.Count ?? 0}");
            if (m.Bedrooms != null && m.Bedrooms.Any())
            {
                for (int i = 0; i < m.Bedrooms.Count; i++)
                {
                    var bedroom = m.Bedrooms[i];
                    Console.WriteLine($"  Bedrooms[{i}]: Beds count = {bedroom.Beds?.Count ?? 0}");
                    if (bedroom.Beds != null && bedroom.Beds.Any())
                    {
                        for (int j = 0; j < bedroom.Beds.Count; j++)
                        {
                            var bed = bedroom.Beds[j];
                            Console.WriteLine($"    Bedrooms[{i}].Beds[{j}]: Type='{bed.Type}', Count={bed.Count}, BedroomIndex={bed.BedroomIndex}");
                        }
                    }
                }
            }
            
            // if (!ModelState.IsValid) 
            // {
            //     Console.WriteLine("=== MODELSTATE KHÔNG HỢP LỆ ===");
            //     Console.WriteLine("Chi tiết lỗi validation:");
            //     foreach (var kvp in ModelState)
            //     {
            //         if (kvp.Value.Errors.Count > 0)
            //         {
            //             Console.WriteLine($"Field '{kvp.Key}':");
            //             foreach (var error in kvp.Value.Errors)
            //             {
            //                 Console.WriteLine($"  - {error.ErrorMessage}");
            //             }
            //             Console.WriteLine($"  - Value: '{kvp.Value.AttemptedValue}'");
            //         }
            //     }
            //     return View(m);
            // }
            
            Console.WriteLine("=== MODELSTATE HỢP LỆ - TIẾP TỤC XỬ LÝ ===");

            // Tạo mới hoặc lấy phòng hiện có
            Console.WriteLine("=== XỬ LÝ ROOM ENTITY ===");
            Room room;
            if (m.RoomId == 0)
            {
                Console.WriteLine("Tạo phòng mới");
                room = new Room { PropertyId = m.PropertyId };
                _db.Rooms.Add(room);
                Console.WriteLine($"Đã thêm room mới với PropertyId: {m.PropertyId}");
            }
            else
            {
                Console.WriteLine($"Cập nhật phòng hiện có với RoomId: {m.RoomId}");
                room = await _db.Rooms
                    .Include(r => r.Beds)
                    .Include(r => r.Amenities)
                    .Include(r => r.Photos)
                    .FirstOrDefaultAsync(r => r.Id == m.RoomId && r.PropertyId == m.PropertyId);
                if (room == null) 
                {
                    Console.WriteLine("Không tìm thấy phòng để cập nhật");
                    return NotFound();
                }

                Console.WriteLine($"Tìm thấy phòng: {room.Name}, có {room.Beds.Count} giường, {room.Amenities.Count} tiện nghi, {room.Photos.Count} ảnh");
                _db.RoomBeds.RemoveRange(room.Beds);
                _db.RoomAmenities.RemoveRange(room.Amenities);
                Console.WriteLine("Đã xóa giường và tiện nghi cũ");
                // Ảnh: giữ ảnh cũ, thêm ảnh mới. (Xóa ảnh ở bên dưới nếu client gửi DeletedPhotoIds)
            }

            // Map fields
            Console.WriteLine("=== MAP FIELDS ===");
            room.RoomType = m.RoomType;
            room.Name = m.RoomName;
            room.Size = m.SizeNumber;
            room.SizeUnit = m.SizeUnit;
            room.SmokingAllowed = m.SmokingAllowed;
            room.Quantity = m.Quantity;
            room.IsSingleBedroom = m.IsSingleBedroom;
            room.CapacityAdults = m.CapacityAdults;
            room.CapacityChildren = m.CapacityChildren;
            room.AllowChildren = m.AllowExtraBed;
            room.AllowExtraBed = m.AllowExtraBed;
            room.SecurityDeposit = m.SecurityDeposit;
            
            Console.WriteLine($"Đã map fields: Type={room.RoomType}, Name={room.Name}, Size={room.Size}{room.SizeUnit}");
            Console.WriteLine($"Quantity={room.Quantity}, Adults={room.CapacityAdults}, Children={room.CapacityChildren}");
            Console.WriteLine($"SecurityDeposit={room.SecurityDeposit}");

            // Xử lý giường ngủ
            Console.WriteLine("=== XỬ LÝ GIƯỜNG NGỦ ===");
            Console.WriteLine($"IsSingleBedroom: {m.IsSingleBedroom}");
            Console.WriteLine($"Số lượng loại giường từ form (single): {m.Beds?.Count ?? 0}");
            Console.WriteLine($"Số lượng phòng ngủ từ form (multi): {m.Bedrooms?.Count ?? 0}");
            
            // Debug chi tiết dữ liệu từ form
            if (m.Beds != null && m.Beds.Any())
            {
                Console.WriteLine("=== BEDS DATA ===");
                foreach (var bed in m.Beds)
                {
                    Console.WriteLine($"Bed: Type={bed.Type}, Count={bed.Count}, BedroomIndex={bed.BedroomIndex}");
                }
            }
            
            if (m.Bedrooms != null && m.Bedrooms.Any())
            {
                Console.WriteLine("=== BEDROOMS DATA ===");
                for (int i = 0; i < m.Bedrooms.Count; i++)
                {
                    var bedroom = m.Bedrooms[i];
                    Console.WriteLine($"Bedroom {i}: {bedroom.Beds?.Count ?? 0} beds");
                    if (bedroom.Beds != null)
                    {
                        foreach (var bed in bedroom.Beds)
                        {
                            Console.WriteLine($"  - Bed: Type={bed.Type}, Count={bed.Count}, BedroomIndex={bed.BedroomIndex}");
                        }
                    }
                }
            }
            
            if (m.IsSingleBedroom)
            {
                // Single bedroom: LUÔN sử dụng BedroomIndex = 0
                Console.WriteLine("=== XỬ LÝ SINGLE BEDROOM ===");
                foreach (var b in m.Beds ?? new())
                {
                    if (b.Count > 0 && !string.IsNullOrWhiteSpace(b.Type))
                    {
                        // FIX: Single bedroom luôn có BedroomIndex = 0
                        var newBed = new RoomBed { BedroomIndex = 0 };
                        newBed.AddBedItem(b.Type, b.Count);
                        room.Beds.Add(newBed);
                        Console.WriteLine($"Đã thêm giường (single): {b.Type} x{b.Count} - BedroomIndex: 0 (forced)");
                    }
                }
            }
            else
            {
                // Multi bedroom: sử dụng m.Bedrooms với BedroomIndex từ vòng lặp
                Console.WriteLine("=== XỬ LÝ MULTI BEDROOM ===");
                for (int bedroomIndex = 0; bedroomIndex < (m.Bedrooms?.Count ?? 0); bedroomIndex++)
                {
                    var bedroom = m.Bedrooms[bedroomIndex];
                    Console.WriteLine($"Xử lý phòng ngủ {bedroomIndex + 1} (BedroomIndex: {bedroomIndex})");
                    
                    foreach (var b in bedroom.Beds ?? new())
                    {
                        if (b.Count > 0 && !string.IsNullOrWhiteSpace(b.Type))
                        {
                            // FIX: Sử dụng bedroomIndex từ vòng lặp, không phải từ form
                            var newBed = new RoomBed { BedroomIndex = bedroomIndex };
                            newBed.AddBedItem(b.Type, b.Count);
                            room.Beds.Add(newBed);
                            Console.WriteLine($"Đã thêm giường (multi): {b.Type} x{b.Count} - Phòng ngủ {bedroomIndex + 1} (BedroomIndex: {bedroomIndex})");
                        }
                    }
                }
            }
            Console.WriteLine($"Tổng số giường trong room: {room.Beds.Count}");
            
            // Xử lý tiện nghi
            Console.WriteLine("=== XỬ LÝ TIỆN NGHI ===");
            Console.WriteLine($"Số lượng tiện nghi từ form: {m.SelectedAmenities?.Count ?? 0}");
            foreach (var a in m.SelectedAmenities ?? new())
            {
                room.Amenities.Add(new RoomAmenity { Name = a });
                Console.WriteLine($"Đã thêm tiện nghi: {a}");
            }
            Console.WriteLine($"Tổng số tiện nghi trong room: {room.Amenities.Count}");

            // ===== Ảnh: xử lý xóa theo DeletedPhotoIds và thêm ảnh mới theo category =====
            // Xóa
            var deletedJson = Request.Form["DeletedPhotoIds"].FirstOrDefault();
            Console.WriteLine("=== XỬ LÝ DELETEDPHOTOIDS ===");
            Console.WriteLine($"DeletedPhotoIds từ form: '{deletedJson}'");
            
            if (!string.IsNullOrWhiteSpace(deletedJson))
            {
                try
                {
                    var delIds = System.Text.Json.JsonSerializer.Deserialize<List<int>>(deletedJson) ?? new();
                    Console.WriteLine($"Đã parse được {delIds.Count} photo IDs để xóa: [{string.Join(", ", delIds)}]");
                    
                    if (delIds.Count > 0)
                    {
                        var toRemove = room.Photos.Where(p => delIds.Contains(p.Id)).ToList();
                        Console.WriteLine($"Tìm thấy {toRemove.Count} photos trong database để xóa:");
                        foreach (var photo in toRemove)
                        {
                            Console.WriteLine($"  - Photo ID: {photo.Id}, URL: {photo.Url}, Category: {photo.Category}");
                        }
                        
                        if (toRemove.Count > 0)
                        {
                            _db.RoomPhotos.RemoveRange(toRemove);
                            Console.WriteLine($"✅ Đã đánh dấu {toRemove.Count} photos để xóa khỏi database");
                        }
                    }
                }
                catch (Exception ex)
                { 
                    Console.WriteLine($"❌ Lỗi khi parse DeletedPhotoIds: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("⚠️ Không có DeletedPhotoIds trong form data");
            }
            
            Console.WriteLine($"Số lượng photos trong room sau khi xóa: {room.Photos.Count}");

            // Thêm mới theo category từ FormData
            var dir = Path.Combine("wwwroot", "uploads", "rooms", (room.PropertyId == 0 ? m.PropertyId : room.PropertyId).ToString());
            Directory.CreateDirectory(dir);
            int sortOrder = room.Photos.Any() ? room.Photos.Max(p => p.SortOrder) + 1 : 0;

            async Task saveFilesAsync(IEnumerable<IFormFile> files, string category)
            {
                foreach (var file in files)
                {
                    if (file?.Length > 0)
                    {
                        var ext = Path.GetExtension(file.FileName);
                        var name = $"{Guid.NewGuid()}{ext}";
                        var savePath = Path.Combine(dir, name);
                        await using (var fs = System.IO.File.Create(savePath))
                            await file.CopyToAsync(fs);
                        var url = $"/uploads/rooms/{room.PropertyId}/{name}";
                        room.Photos.Add(new RoomPhoto { Url = url, SortOrder = sortOrder++, Category = category });
                    }
                }
            }

            // Debug: Kiểm tra tất cả files trong Request.Form.Files
            Console.WriteLine("=== DEBUG FORM FILES ===");
            Console.WriteLine($"Tổng số files trong Request.Form.Files: {Request.Form.Files.Count}");
            
            // Thử các cách khác để lấy files
            Console.WriteLine("=== THỬ CÁC CÁCH LẤY FILES ===");
            
            // Cách 1: Request.Form.Files trực tiếp
            var allFiles = Request.Form.Files.ToList();
            Console.WriteLine($"Cách 1 - Request.Form.Files.ToList(): {allFiles.Count}");
            
            // Cách 2: HttpContext.Request.Form.Files
            var contextFiles = HttpContext.Request.Form.Files.ToList();
            Console.WriteLine($"Cách 2 - HttpContext.Request.Form.Files: {contextFiles.Count}");
            
            // Cách 3: Kiểm tra Request.Headers
            Console.WriteLine("=== REQUEST HEADERS ===");
            Console.WriteLine($"Content-Type: {Request.Headers["Content-Type"]}");
            Console.WriteLine($"Content-Length: {Request.Headers["Content-Length"]}");
            
            // Liệt kê tất cả file names và thông tin chi tiết
            if (Request.Form.Files.Count == 0)
            {
                Console.WriteLine("⚠️ KHÔNG CÓ FILE NÀO TRONG Request.Form.Files!");
            }
            else
            {
                Console.WriteLine("📁 DANH SÁCH FILES:");
                foreach (var file in Request.Form.Files)
                {
                    Console.WriteLine($"  - Name: '{file.Name}', Length: {file.Length}, ContentType: {file.ContentType}");
                }
            }
            
            // Kiểm tra form data khác
            Console.WriteLine("=== DEBUG FORM DATA ===");
            Console.WriteLine($"Form keys: {string.Join(", ", Request.Form.Keys)}");
            Console.WriteLine($"Form values count: {Request.Form.Count}");
            
            // Liệt kê một số form values quan trọng
            foreach (var key in Request.Form.Keys)
            {
                var value = Request.Form[key].ToString();
                if (value.Length > 100) value = value.Substring(0, 100) + "...";
                Console.WriteLine($"  - {key}: '{value}'");
            }
            
            // Lấy files bằng cách chắc chắn đúng
            var bedroomFiles = Request.Form.Files.Where(f => f.Name == "BedroomPhotos").ToList();
            var bathroomFiles = Request.Form.Files.Where(f => f.Name == "BathroomPhotos").ToList();
            var additionalFiles = Request.Form.Files.Where(f => f.Name == "AdditionalPhotos").ToList();
            var moreAdditionalFiles = Request.Form.Files.Where(f => f.Name == "MoreAdditionalPhotos").ToList();

            Console.WriteLine("=== LẤY FILES BẰNG CÁCH CHẮC CHẮN ĐÚNG ===");
            Console.WriteLine($"BedroomPhotos: {bedroomFiles.Count} files");
            Console.WriteLine($"BathroomPhotos: {bathroomFiles.Count} files");
            Console.WriteLine($"AdditionalPhotos: {additionalFiles.Count} files");
            Console.WriteLine($"MoreAdditionalPhotos: {moreAdditionalFiles.Count} files");
            
            // Gộp AdditionalPhotos và MoreAdditionalPhotos
            var allAdditionalFiles = additionalFiles.Concat(moreAdditionalFiles).ToList();
            Console.WriteLine($"Tổng AdditionalPhotos: {allAdditionalFiles.Count} files");
            
            // Sử dụng cách này (chắc chắn đúng)
            var finalBedroomFiles = bedroomFiles;
            var finalBathroomFiles = bathroomFiles;
            var finalAdditionalFiles = allAdditionalFiles;

            Console.WriteLine("=== XỬ LÝ ẢNH VỚI CÁCH LẤY FILES CHẮC CHẮN ĐÚNG ===");
            var totalFiles = finalBedroomFiles.Count + finalBathroomFiles.Count + finalAdditionalFiles.Count;
            Console.WriteLine($"Tổng số files: {totalFiles}");
            
            if (totalFiles > 0)
            {
                Console.WriteLine("🚀 BẮT ĐẦU LƯU FILES:");
                await saveFilesAsync(finalBedroomFiles, "bedroom");
                await saveFilesAsync(finalBathroomFiles, "bathroom");
                await saveFilesAsync(finalAdditionalFiles, "additional");
                Console.WriteLine("✅ HOÀN THÀNH LƯU FILES");
            }
            else
            {
                Console.WriteLine("⚠️ KHÔNG CÓ FILE NÀO ĐỂ XỬ LÝ!");
            }

            Console.WriteLine("=== HOÀN THÀNH XỬ LÝ - CHUẨN BỊ LƯU DATABASE ===");
            Console.WriteLine($"Số lượng ảnh cuối cùng: {room.Photos.Count}");
            Console.WriteLine($"Số lượng giường cuối cùng: {room.Beds.Count}");
            Console.WriteLine($"Số lượng tiện nghi cuối cùng: {room.Amenities.Count}");
            
            // LOG CHI TIẾT TRƯỚC KHI LƯU DATABASE
            Console.WriteLine("=== LOG CHI TIẾT TRƯỚC KHI LƯU DATABASE ===");
            Console.WriteLine($"Room Info:");
            Console.WriteLine($"  - ID: {room.Id} (0 = new)");
            Console.WriteLine($"  - PropertyId: {room.PropertyId}");
            Console.WriteLine($"  - Name: {room.Name}");
            Console.WriteLine($"  - RoomType: {room.RoomType}");
            Console.WriteLine($"  - IsSingleBedroom: {room.IsSingleBedroom}");
            Console.WriteLine($"  - Quantity: {room.Quantity}");
            Console.WriteLine($"  - CapacityAdults: {room.CapacityAdults}");
            Console.WriteLine($"  - CapacityChildren: {room.CapacityChildren}");
            Console.WriteLine($"  - SecurityDeposit: {room.SecurityDeposit}");
            
            Console.WriteLine($"RoomBeds Details ({room.Beds.Count} beds):");
            for (int i = 0; i < room.Beds.Count; i++)
            {
                var bed = room.Beds[i];
                Console.WriteLine($"  Bed {i + 1}:");
                Console.WriteLine($"    - ID: {bed.Id} (0 = new)");
                Console.WriteLine($"    - RoomId: {bed.RoomId}");
                Console.WriteLine($"    - BedroomIndex: {bed.BedroomIndex}");
                Console.WriteLine($"    - Types: [{string.Join(", ", bed.Types)}]");
                Console.WriteLine($"    - Counts: [{string.Join(", ", bed.Counts)}]");
                Console.WriteLine($"    - BedItems: {bed.GetAllBedItems().Count} items");
                foreach (var bedItem in bed.GetAllBedItems())
                {
                    Console.WriteLine($"      * {bedItem.Type} x{bedItem.Count} (BedroomIndex: {bedItem.BedroomIndex})");
                }
            }
            
            Console.WriteLine($"RoomAmenities Details ({room.Amenities.Count} amenities):");
            for (int i = 0; i < room.Amenities.Count; i++)
            {
                var amenity = room.Amenities[i];
                Console.WriteLine($"  Amenity {i + 1}: {amenity.Name} (ID: {amenity.Id})");
            }
            
            Console.WriteLine($"RoomPhotos Details ({room.Photos.Count} photos):");
            for (int i = 0; i < room.Photos.Count; i++)
            {
                var photo = room.Photos[i];
                Console.WriteLine($"  Photo {i + 1}: {photo.Url} (Category: {photo.Category}, SortOrder: {photo.SortOrder})");
            }
            
            Console.WriteLine("=== BẮT ĐẦU LƯU DATABASE ===");
            await _db.SaveChangesAsync();
            Console.WriteLine("✅ Đã lưu thành công vào database");
            Console.WriteLine($"Room ID sau khi lưu: {room.Id}");
            Console.WriteLine("=== KẾT THÚC ROOMDATA POST ===");

            return RedirectToAction(nameof(PropertyData), new { propertyId = m.PropertyId, tab = "rooms" });
        }

        [HttpPost]
        public async Task<IActionResult> CreatePricePackage(int propertyId, string? cancellationPolicy, bool breakfastIncluded, int? packageId = null, string returnTab = "pricing")
        {
            try
            {
                Console.WriteLine("=== BẮT ĐẦU CREATEPRICEPACKAGE POST ===");
                Console.WriteLine($"PropertyId: {propertyId}");
                Console.WriteLine($"CancellationPolicy: {cancellationPolicy}");
                Console.WriteLine($"BreakfastIncluded: {breakfastIncluded}");

                // Kiểm tra property có tồn tại không
                var property = await _db.Properties.FindAsync(propertyId);
                if (property == null)
                {
                    Console.WriteLine("Property không tồn tại");
                    return NotFound();
                }

                // Nếu người dùng không chọn chính sách hủy, dùng mặc định
                var finalCancellationPolicy = string.IsNullOrWhiteSpace(cancellationPolicy)
                    ? "refund_1"
                    : cancellationPolicy;

                // Nếu có packageId, cập nhật; nếu không, tạo mới
                if (packageId.HasValue && packageId.Value > 0)
                {
                    var existingPackage = await _db.PricePackages
                        .FirstOrDefaultAsync(p => p.Id == packageId.Value && p.PropertyId == propertyId);

                if (existingPackage != null)
                {
                    Console.WriteLine("Cập nhật price package hiện có");
                    existingPackage.CancellationPolicy = finalCancellationPolicy;
                    existingPackage.BreakfastIncluded = breakfastIncluded;
                    existingPackage.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                        Console.WriteLine("PackageId không tồn tại, tạo mới");
                        var pricePackage = new PricePackage
                        {
                            PropertyId = propertyId,
                            CancellationPolicy = finalCancellationPolicy,
                            BreakfastIncluded = breakfastIncluded,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _db.PricePackages.Add(pricePackage);
                    }
                }
                else
                {
                    // Luôn tạo gói giá mới (cho phép nhiều gói giá)
                    Console.WriteLine("Tạo price package mới");
                    var pricePackage = new PricePackage
                    {
                        PropertyId = propertyId,
                        CancellationPolicy = finalCancellationPolicy,
                        BreakfastIncluded = breakfastIncluded,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.PricePackages.Add(pricePackage);
                }

                await _db.SaveChangesAsync();

                Console.WriteLine("Đã lưu price package thành công vào database");
                Console.WriteLine("=== KẾT THÚC CREATEPRICEPACKAGE POST ===");

                TempData["Success"] = packageId.HasValue ? "Đã cập nhật gói giá thành công" : "Đã tạo gói giá thành công";
                return RedirectToAction(nameof(PropertyData), new { propertyId = propertyId, tab = returnTab });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tạo price package: {ex.Message}");
                return RedirectToAction(nameof(PropertyData), new { propertyId = propertyId, tab = returnTab });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeletePricePackage(int propertyId, int packageId, string returnTab = "pricing")
        {
            try
            {
                var package = await _db.PricePackages
                    .FirstOrDefaultAsync(p => p.Id == packageId && p.PropertyId == propertyId);
                
                if (package == null)
                {
                    return NotFound();
                }

                // Kiểm tra xem có RoomPrice nào đang sử dụng gói giá này không
                var hasRoomPrices = await _db.RoomPrices
                    .AnyAsync(rp => rp.PricePackageId == packageId);
                
                if (hasRoomPrices)
                {
                    TempData["Error"] = "Không thể xóa gói giá này vì đang được sử dụng bởi một hoặc nhiều giá phòng.";
                    return RedirectToAction(nameof(PropertyData), new { propertyId = propertyId, tab = returnTab });
                }

                _db.PricePackages.Remove(package);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Đã xóa gói giá thành công.";
                return RedirectToAction(nameof(PropertyData), new { propertyId = propertyId, tab = returnTab });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi xóa price package: {ex.Message}");
                TempData["Error"] = "Có lỗi xảy ra khi xóa gói giá.";
                return RedirectToAction(nameof(PropertyData), new { propertyId = propertyId, tab = returnTab });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveRoomPrices(int propertyId)
        {
            try
            {
                Console.WriteLine("=== SaveRoomPrices START ===");
                Console.WriteLine($"Request.ContentType: {Request.ContentType}");
                Console.WriteLine($"Request.Form.Keys.Count: {Request.Form.Keys.Count}");
                Console.WriteLine("All Form Keys:");
                foreach (var key in Request.Form.Keys)
                {
                    var values = Request.Form[key];
                    Console.WriteLine($"  {key}: [{string.Join(", ", values)}]");
                }
                
                // Đọc dữ liệu từ Request.Form vì model binding có thể không hoạt động đúng với Dictionary<int, List<T>>
                var prices = new Dictionary<int, List<decimal>>();
                var pricePackages = new Dictionary<int, List<int?>>();
                var priceIds = new Dictionary<int, List<int>>();

                // Parse prices - format: prices[17][]
                foreach (var key in Request.Form.Keys)
                {
                    Console.WriteLine($"Checking key: {key}");
                    if (key.StartsWith("prices[") && key.Contains("]"))
                    {
                        // Extract roomId from "prices[17][]" or "prices[17]"
                        var startIdx = 7; // "prices[".Length
                        var endIdx = key.IndexOf(']', startIdx);
                        if (endIdx > startIdx)
                        {
                            var roomIdStr = key.Substring(startIdx, endIdx - startIdx);
                            Console.WriteLine($"  Found price key: {key}, roomIdStr: {roomIdStr}");
                            if (int.TryParse(roomIdStr, out int roomId))
                            {
                                var values = Request.Form[key];
                                Console.WriteLine($"  Room {roomId} has {values.Count} price values");
                                if (!prices.ContainsKey(roomId))
                                    prices[roomId] = new List<decimal>();
                                
                                foreach (var value in values)
                                {
                                    // Remove all non-numeric characters
                                    var cleanValue = value.ToString().Replace(",", "").Replace(".", "").Trim();
                                    Console.WriteLine($"    Parsing value: '{value}' -> cleaned: '{cleanValue}'");
                                    if (decimal.TryParse(cleanValue, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out decimal price))
                                    {
                                        prices[roomId].Add(price);
                                        Console.WriteLine($"    ✓ Parsed price for Room {roomId}: {price}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"    ✗ Failed to parse price for Room {roomId}: '{value}' (cleaned: '{cleanValue}')");
                                    }
                                }
                            }
                        }
                    }
                }

                // Parse pricePackages - format: pricePackages[17][]
                foreach (var key in Request.Form.Keys)
                {
                    if (key.StartsWith("pricePackages[") && key.Contains("]"))
                    {
                        // Extract roomId from "pricePackages[17][]"
                        var startIdx = 14; // "pricePackages[".Length
                        var endIdx = key.IndexOf(']', startIdx);
                        if (endIdx > startIdx)
                        {
                            var roomIdStr = key.Substring(startIdx, endIdx - startIdx);
                            Console.WriteLine($"  Found package key: {key}, roomIdStr: {roomIdStr}");
                            if (int.TryParse(roomIdStr, out int roomId))
                            {
                                var values = Request.Form[key];
                                Console.WriteLine($"  Room {roomId} has {values.Count} package values");
                                if (!pricePackages.ContainsKey(roomId))
                                    pricePackages[roomId] = new List<int?>();
                                
                                foreach (var value in values)
                                {
                                    if (string.IsNullOrWhiteSpace(value))
                                    {
                                        pricePackages[roomId].Add(null);
                                        Console.WriteLine($"    Added null package for Room {roomId}");
                                    }
                                    else if (int.TryParse(value, out int packageId))
                                    {
                                        pricePackages[roomId].Add(packageId);
                                        Console.WriteLine($"    Added package {packageId} for Room {roomId}");
                                    }
                                    else
                                    {
                                        pricePackages[roomId].Add(null);
                                        Console.WriteLine($"    Failed to parse package, added null for Room {roomId}");
                                    }
                                }
                            }
                        }
                    }
                }

                // Parse priceIds - format: priceIds[17][]
                foreach (var key in Request.Form.Keys)
                {
                    if (key.StartsWith("priceIds[") && key.Contains("]"))
                    {
                        // Extract roomId from "priceIds[17][]"
                        var startIdx = 9; // "priceIds[".Length
                        var endIdx = key.IndexOf(']', startIdx);
                        if (endIdx > startIdx)
                        {
                            var roomIdStr = key.Substring(startIdx, endIdx - startIdx);
                            Console.WriteLine($"  Found priceId key: {key}, roomIdStr: {roomIdStr}");
                            if (int.TryParse(roomIdStr, out int roomId))
                            {
                                var values = Request.Form[key];
                                Console.WriteLine($"  Room {roomId} has {values.Count} priceId values");
                                if (!priceIds.ContainsKey(roomId))
                                    priceIds[roomId] = new List<int>();
                                
                                foreach (var value in values)
                                {
                                    if (int.TryParse(value, out int priceId))
                                    {
                                        priceIds[roomId].Add(priceId);
                                        Console.WriteLine($"    Added priceId {priceId} for Room {roomId}");
                                    }
                                    else
                                    {
                                        priceIds[roomId].Add(0);
                                        Console.WriteLine($"    Added priceId 0 (new) for Room {roomId}");
                                    }
                                }
                            }
                        }
                    }
                }

                Console.WriteLine($"=== SaveRoomPrices Debug ===");
                Console.WriteLine($"PropertyId: {propertyId}");
                Console.WriteLine($"Prices count: {prices.Count}");
                foreach (var kvp in prices)
                {
                    Console.WriteLine($"  Room {kvp.Key}: {kvp.Value.Count} prices");
                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        Console.WriteLine($"    Price {i}: {kvp.Value[i]}");
                    }
                }
                Console.WriteLine($"PricePackages count: {pricePackages.Count}");
                foreach (var kvp in pricePackages)
                {
                    Console.WriteLine($"  Room {kvp.Key}: {kvp.Value.Count} packages");
                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        Console.WriteLine($"    Package {i}: {kvp.Value[i]}");
                    }
                }
                Console.WriteLine($"PriceIds count: {priceIds.Count}");
                foreach (var kvp in priceIds)
                {
                    Console.WriteLine($"  Room {kvp.Key}: {kvp.Value.Count} ids");
                    for (int i = 0; i < kvp.Value.Count; i++)
                    {
                        Console.WriteLine($"    Id {i}: {kvp.Value[i]}");
                    }
                }

                // Validation: Kiểm tra giá phòng phải cao hơn mức bảo hộ
                var rooms = await _db.Rooms.Where(r => r.PropertyId == propertyId).ToListAsync();
                var validationErrors = new List<string>();

                foreach (var room in rooms)
                {
                    if (prices.TryGetValue(room.Id, out var priceList))
                    {
                        foreach (var amount in priceList)
                    {
                        if (room.SecurityDeposit.HasValue && amount < room.SecurityDeposit.Value)
                        {
                            validationErrors.Add($"Phòng '{room.Name}': Giá phải cao hơn mức bảo hộ {room.SecurityDeposit.Value:N0} VND");
                            }
                        }
                    }
                }

                if (validationErrors.Any())
                {
                    TempData["Error"] = string.Join("; ", validationErrors);
                    return RedirectToAction("Preview", new { propertyId });
                }

                // Lấy tất cả RoomPrice IDs được gửi từ form (để xóa những cái không còn trong form)
                var submittedPriceIds = new HashSet<int>();
                foreach (var idList in priceIds.Values)
                {
                    foreach (var id in idList)
                    {
                        if (id > 0)
                        {
                            submittedPriceIds.Add(id);
                        }
                    }
                }

                Console.WriteLine($"=== Submitted Price IDs ===");
                Console.WriteLine($"Total submitted IDs: {submittedPriceIds.Count}");
                foreach (var id in submittedPriceIds)
                {
                    Console.WriteLine($"  PriceId: {id}");
                }

                // Lấy tất cả RoomIds có trong form (để chỉ xóa RoomPrice của các phòng này)
                var submittedRoomIds = new HashSet<int>();
                submittedRoomIds.UnionWith(prices.Keys);
                submittedRoomIds.UnionWith(pricePackages.Keys);
                submittedRoomIds.UnionWith(priceIds.Keys);

                Console.WriteLine($"=== Submitted Room IDs ===");
                Console.WriteLine($"Total submitted Room IDs: {submittedRoomIds.Count}");
                foreach (var roomId in submittedRoomIds)
                {
                    Console.WriteLine($"  RoomId: {roomId}");
                }

                Console.WriteLine("=== [CHECKPOINT] Đã đến trước log [11] ===");
                
                // ===== LOG QUAN TRỌNG: Kiểm tra giá phòng trước khi lưu =====
                Console.WriteLine("=== [11] Giá phòng trước khi lưu vào DB ===");
                Console.WriteLine($"PropertyId: {propertyId}");
                Console.WriteLine($"Tổng số phòng: {rooms.Count}");
                
                // Load all price packages để lấy tên
                Console.WriteLine("=== [11-1] Đang load price packages... ===");
                var allPackagesDict = await _db.PricePackages
                    .Where(p => p.PropertyId == propertyId)
                    .ToDictionaryAsync(p => p.Id, p => p.CancellationPolicyDisplayName);
                Console.WriteLine($"=== [11-2] Đã load {allPackagesDict.Count} price packages ===");
                
                foreach (var room in rooms)
                {
                    if (prices.TryGetValue(room.Id, out var priceList))
                    {
                        pricePackages.TryGetValue(room.Id, out var packageList);
                        priceIds.TryGetValue(room.Id, out var idList);
                        Console.WriteLine($"  Room {room.Id} ({room.Name}):");
                        Console.WriteLine($"    - Số lượng giá: {priceList.Count}");
                        Console.WriteLine($"    - Số lượng packages: {packageList?.Count ?? 0}");
                        Console.WriteLine($"    - Số lượng priceIds: {idList?.Count ?? 0}");
                        
                        // Log chi tiết packages
                        if (packageList != null && packageList.Any())
                        {
                            Console.WriteLine($"    - [DEBUG GÓI GIÁ] Danh sách packages:");
                            for (int pkgIdx = 0; pkgIdx < packageList.Count; pkgIdx++)
                            {
                                var pkgId = packageList[pkgIdx];
                                var pkgName = pkgId.HasValue && allPackagesDict.ContainsKey(pkgId.Value)
                                    ? allPackagesDict[pkgId.Value]
                                    : (pkgId.HasValue ? "NOT FOUND" : "NULL");
                                Console.WriteLine($"      Package[{pkgIdx}]: Id={pkgId}, Name={pkgName}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"    - [DEBUG GÓI GIÁ] KHÔNG CÓ PACKAGES");
                        }
                        
                        for (int i = 0; i < priceList.Count; i++)
                        {
                            var amount = priceList[i];
                            var packageId = i < (packageList?.Count ?? 0) ? packageList[i] : null;
                            var priceId = i < (idList?.Count ?? 0) ? idList[i] : 0;
                            
                            // Log chi tiết package cho mỗi price
                            var packageInfo = packageId.HasValue && allPackagesDict.ContainsKey(packageId.Value)
                                ? allPackagesDict[packageId.Value]
                                : (packageId.HasValue ? "NOT FOUND" : "NULL");
                            
                            Console.WriteLine($"    - Giá {i + 1}: {amount:N0} VND | PackageId: {packageId} | PackageName: {packageInfo} | PriceId: {priceId} | Status: {(priceId > 0 ? "UPDATE" : "CREATE NEW")}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  Room {room.Id} ({room.Name}): KHÔNG CÓ GIÁ");
                    }
                }
                Console.WriteLine("=== [END DEBUG] ===");
                
                // Lưu giá phòng - hỗ trợ nhiều giá cho mỗi phòng với PricePackageId khác nhau
                // TẠO MỚI VÀ CẬP NHẬT TRƯỚC, SAU ĐÓ MỚI XÓA
                var updatedPriceIds = new HashSet<int>();
                
                foreach (var room in rooms)
                {
                    if (!prices.TryGetValue(room.Id, out var priceList)) 
                    {
                        Console.WriteLine($"  Room {room.Id}: No prices found, skipping");
                        continue;
                    }
                    if (!pricePackages.TryGetValue(room.Id, out var packageList)) packageList = new List<int?>();
                    if (!priceIds.TryGetValue(room.Id, out var idList)) idList = new List<int>();

                    Console.WriteLine($"  Processing Room {room.Id}: {priceList.Count} prices, {packageList.Count} packages, {idList.Count} ids");

                    // Chỉ xử lý theo số lượng prices (đây là số lượng thực tế)
                    for (int i = 0; i < priceList.Count; i++)
                    {
                        var amount = priceList[i];
                        // Lấy package tương ứng với index i
                        // Nếu không có package ở index i, chỉ lấy null (KHÔNG lấy package đầu tiên để tránh nhầm lẫn)
                        var packageId = i < packageList.Count ? packageList[i] : null;
                        var priceId = i < idList.Count ? idList[i] : 0;

                        Console.WriteLine($"  Processing Room {room.Id}, Price {i}: Amount={amount}, PackageId={packageId}, PriceId={priceId}");
                        if (i >= packageList.Count)
                        {
                            Console.WriteLine($"    WARNING: No package at index {i}, using null (packageList.Count={packageList.Count})");
                        }

                        // Validate amount > 0
                        if (amount <= 0)
                        {
                            Console.WriteLine($"    WARNING: Amount is {amount}, skipping this price entry");
                            continue;
                        }

                        // Nếu priceId > 0, cập nhật existing; nếu = 0, tạo mới
                        if (priceId > 0)
                        {
                            var existing = await _db.RoomPrices
                                .Include(rp => rp.PricePackage)
                                .FirstOrDefaultAsync(p => p.Id == priceId && p.PropertyId == propertyId && p.RoomId == room.Id);
                            if (existing != null)
                            {
                                var oldPackageId = existing.PricePackageId;
                                var oldPackageName = existing.PricePackage?.CancellationPolicyDisplayName ?? "NULL";
                                
                                existing.Amount = amount;
                                existing.PricePackageId = packageId;
                                existing.UpdatedAt = DateTime.UtcNow;
                                updatedPriceIds.Add(existing.Id);
                                
                                var newPackageName = packageId.HasValue 
                                    ? (await _db.PricePackages.FirstOrDefaultAsync(p => p.Id == packageId.Value))?.CancellationPolicyDisplayName ?? "NOT FOUND"
                                    : "NULL";
                                
                                Console.WriteLine($"    ✓ Updated existing RoomPrice {priceId}: Amount={amount}, PackageId={packageId}");
                                Console.WriteLine($"    [DEBUG GÓI GIÁ] PackageId: {oldPackageId} ({oldPackageName}) -> {packageId} ({newPackageName})");
                            }
                            else
                            {
                                Console.WriteLine($"    WARNING: RoomPrice {priceId} not found, creating new one");
                                var newPrice = new Models.RoomPrice
                        {
                            PropertyId = propertyId,
                                    RoomId = room.Id,
                            Amount = amount,
                                    Currency = "VND",
                                    PricePackageId = packageId
                                };
                                _db.RoomPrices.Add(newPrice);
                                
                                var newPackageName = packageId.HasValue 
                                    ? (await _db.PricePackages.FirstOrDefaultAsync(p => p.Id == packageId.Value))?.CancellationPolicyDisplayName ?? "NOT FOUND"
                                    : "NULL";
                                
                                Console.WriteLine($"    ✓ Created new RoomPrice for Room {room.Id}, Package {packageId} ({newPackageName}), Amount={amount}");
                            }
                    }
                    else
                    {
                            var newPrice = new Models.RoomPrice
                            {
                                PropertyId = propertyId,
                                RoomId = room.Id,
                                Amount = amount,
                                Currency = "VND",
                                PricePackageId = packageId
                            };
                            _db.RoomPrices.Add(newPrice);
                            
                            var newPackageName = packageId.HasValue 
                                ? (await _db.PricePackages.FirstOrDefaultAsync(p => p.Id == packageId.Value))?.CancellationPolicyDisplayName ?? "NOT FOUND"
                                : "NULL";
                            
                            Console.WriteLine($"    ✓ Created new RoomPrice for Room {room.Id}, Package {packageId} ({newPackageName}), Amount={amount}");
                        }
                    }
                }
                
                // Save changes để có ID cho các RoomPrice mới
                await _db.SaveChangesAsync();
                Console.WriteLine("=== Saved new/updated RoomPrices ===");
                
                // Sau khi save, lấy ID của các RoomPrice mới vừa tạo và thêm vào updatedPriceIds
                // Để tránh bị xóa nhầm
                foreach (var room in rooms)
                {
                    if (!prices.TryGetValue(room.Id, out var priceList)) continue;
                    if (!priceIds.TryGetValue(room.Id, out var idList)) idList = new List<int>();

                    for (int i = 0; i < priceList.Count; i++)
                    {
                        var amount = priceList[i];
                        var priceId = i < idList.Count ? idList[i] : 0;

                        // Nếu priceId = 0 (new price), tìm RoomPrice vừa tạo bằng cách match Amount và RoomId
                        if (priceId == 0 && amount > 0)
                        {
                            var packageId = i < (pricePackages.TryGetValue(room.Id, out var pkgList) ? pkgList.Count : 0) 
                                ? (pricePackages[room.Id][i]) 
                                : (pricePackages.TryGetValue(room.Id, out var pkgList2) && pkgList2.Count > 0 ? pkgList2[0] : null);
                            
                            var newlyCreated = await _db.RoomPrices
                                .Where(p => p.PropertyId == propertyId 
                                    && p.RoomId == room.Id 
                                    && p.Amount == amount 
                                    && p.PricePackageId == packageId
                                    && !updatedPriceIds.Contains(p.Id))
                                .OrderByDescending(p => p.Id)
                                .FirstOrDefaultAsync();
                            
                            if (newlyCreated != null)
                            {
                                updatedPriceIds.Add(newlyCreated.Id);
                                Console.WriteLine($"    ✓ Added newly created RoomPrice {newlyCreated.Id} to updatedPriceIds");
                            }
                        }
                    }
                }

                // BÂY GIỜ MỚI XÓA các RoomPrice không còn trong form
                // CHỈ xóa các RoomPrice của các phòng có trong form, và không có trong updatedPriceIds
                var existingRoomPrices = await _db.RoomPrices
                    .Where(p => p.PropertyId == propertyId && submittedRoomIds.Contains(p.RoomId))
                    .ToListAsync();
                
                var toDelete = existingRoomPrices
                    .Where(p => !updatedPriceIds.Contains(p.Id))
                    .ToList();
                
                Console.WriteLine($"=== Existing RoomPrices ===");
                Console.WriteLine($"Total existing: {existingRoomPrices.Count}");
                foreach (var rp in existingRoomPrices)
                {
                    Console.WriteLine($"  RoomPrice {rp.Id}: Room {rp.RoomId}, Amount {rp.Amount}, PackageId {rp.PricePackageId}");
                }
                
                Console.WriteLine($"=== To Delete ===");
                Console.WriteLine($"Total to delete: {toDelete.Count}");
                foreach (var rp in toDelete)
                {
                    Console.WriteLine($"  RoomPrice {rp.Id}: Room {rp.RoomId}, Amount {rp.Amount}");
                }
                
                if (toDelete.Any())
                {
                    Console.WriteLine($"  Deleting {toDelete.Count} RoomPrices that are no longer in form");
                    _db.RoomPrices.RemoveRange(toDelete);
                }

                // Save changes lần cuối (cho các RoomPrice bị xóa)
                await _db.SaveChangesAsync();
                
                // Verify saved data
                var savedRoomPrices = await _db.RoomPrices
                    .Include(rp => rp.PricePackage)
                    .Where(p => p.PropertyId == propertyId)
                    .ToListAsync();
                Console.WriteLine($"=== Final Saved RoomPrices ===");
                Console.WriteLine($"Total saved: {savedRoomPrices.Count}");
                foreach (var rp in savedRoomPrices)
                {
                    var packageName = rp.PricePackage != null ? rp.PricePackage.CancellationPolicyDisplayName : "NULL";
                    Console.WriteLine($"  RoomPrice {rp.Id}: Room {rp.RoomId}, Amount={rp.Amount}, PackageId={rp.PricePackageId}, PackageName={packageName}");
                }
                
                Console.WriteLine("=== SaveRoomPrices Completed ===");
                TempData["Success"] = "Đã lưu giá phòng thành công";
                return RedirectToAction("Preview", new { propertyId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi lưu giá phòng: {ex.Message}";
                return RedirectToAction("Preview", new { propertyId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Preview(int propertyId)
        {
            var property = await _db.Properties.FindAsync(propertyId);
            if (property == null) return NotFound();

            var propertyData = await _db.PropertyData
                .Include(p => p.Property)
                .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
            var rooms = await _db.Rooms
                .Include(r => r.Beds)
                .Where(r => r.PropertyId == propertyId)
                .ToListAsync();
            // Load tất cả PricePackages
            var allPricePackages = await _db.PricePackages
                .Where(p => p.PropertyId == propertyId)
                .ToListAsync();
            
            // Load tất cả RoomPrices với PricePackage
            var allRoomPrices = await _db.RoomPrices
                .Include(rp => rp.PricePackage)
                .Where(p => p.PropertyId == propertyId)
                .OrderBy(rp => rp.Id)
                .ToListAsync();
            
            Console.WriteLine($"=== Preview GET: Loaded RoomPrices ===");
            Console.WriteLine($"Total RoomPrices: {allRoomPrices.Count}");
            foreach (var rp in allRoomPrices)
            {
                var packageName = rp.PricePackage != null ? rp.PricePackage.CancellationPolicyDisplayName : "NULL";
                Console.WriteLine($"  RoomPrice {rp.Id}: Room {rp.RoomId}, Amount={rp.Amount}, PackageId={rp.PricePackageId}, PackageName={packageName}");
            }
            
            // Group RoomPrices by RoomId
            var roomPricesByRoom = allRoomPrices
                .GroupBy(rp => rp.RoomId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var viewModel = new PreviewViewModel
            {
                PropertyId = propertyId,
                PropertyName = property.Name,
                PropertyData = propertyData,
                Rooms = rooms,
                PricePackage = allPricePackages.FirstOrDefault(), // Giữ lại để tương thích
                RoomPrices = roomPricesByRoom.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.First().Amount) // Giữ lại để tương thích
            };
            
            // Pass additional data via ViewBag
            ViewBag.AllPricePackages = allPricePackages;
            ViewBag.RoomPricesByRoom = roomPricesByRoom;

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Publish(int propertyId)
        {
            try
            {
                var meId = _users.GetUserId(User);
                var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == meId);
                if (property == null) return NotFound();

                // Validate tối thiểu trước khi publish
                var rooms = await _db.Rooms.Where(r => r.PropertyId == propertyId).ToListAsync();
                if (!rooms.Any()) { TempData["Error"] = "Hãy tạo ít nhất một phòng trước khi đăng."; return RedirectToAction("Preview", new { propertyId }); }

                var pricePackage = await _db.PricePackages.FirstOrDefaultAsync(p => p.PropertyId == propertyId);
                if (pricePackage == null) { TempData["Error"] = "Hãy thiết lập Gói giá trước khi đăng."; return RedirectToAction("Preview", new { propertyId }); }

                var anyPrice = await _db.RoomPrices.AnyAsync(p => p.PropertyId == propertyId);
                if (!anyPrice) { TempData["Error"] = "Hãy thiết lập giá cho phòng trước khi đăng."; return RedirectToAction("Preview", new { propertyId }); }

                // Đăng ngay: đánh dấu đã phê duyệt (mở bán) và không còn draft
                property.IsDraft = false;
                property.Status = PropertyStatus.Approved;
                property.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                TempData["Success"] = "Chỗ nghỉ đã được đăng lên Booking.com và sẵn sàng mở bán!";
                // Chuyển sang trang hiển thị công khai
                return RedirectToAction("Hotel", "Public", new { id = propertyId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi đăng tải: {ex.Message}";
                return RedirectToAction("Preview", new { propertyId });
            }
        }

        // HUB: Trung tâm quản lý cơ sở lưu trú (sau khi đăng xong)
        [HttpGet]
        public async Task<IActionResult> PropertyHub(int propertyId)
        {
            await SetUserHasPropertiesAsync();
            var meId = _users.GetUserId(User);
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == meId);
            if (property == null) return NotFound();

            // Lấy danh sách booking cho property này với thông tin phòng
            var bookings = await _db.Bookings
                .Where(b => b.PropertyId == propertyId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            // Lấy thông tin phòng cho mỗi booking
            var bookingWithRooms = new List<dynamic>();
            foreach (var booking in bookings)
            {
                var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == booking.RoomId);
                bookingWithRooms.Add(new
                {
                    Booking = booking,
                    RoomName = room?.Name ?? "Phòng không xác định"
                });
            }

            // Lấy booking gần đây (30 ngày qua) để hiển thị trong phần "Đặt phòng" - chỉ 5 booking gần nhất
            var recentBookings = bookingWithRooms
                .Where(b => b.Booking.CreatedAt >= DateTime.Today.AddDays(-30))
                .GroupBy(b => b.Booking.Id) // Nhóm theo Booking ID để tránh trùng lặp
                .Select(g => g.First()) // Lấy booking đầu tiên trong mỗi nhóm
                .OrderByDescending(b => b.Booking.CreatedAt) // Sắp xếp theo ngày tạo mới nhất
                .Take(5) // Chỉ lấy 5 booking gần nhất
                .ToList();

            // Tính toán thống kê (30 ngày qua)
            var last30Days = DateTime.Today.AddDays(-30);
            var recentBookingsForStats = bookings.Where(b => b.CreatedAt >= last30Days).ToList();
            
            // Tính doanh thu sau khi trừ hoa hồng Booking.com (20%)
            var totalRevenue = recentBookingsForStats.Sum(b => b.TotalPrice * 0.8m); // 80% sau khi trừ 20% hoa hồng
            var averageDailyRate = recentBookingsForStats.Any() ? recentBookingsForStats.Average(b => b.PricePerNight) : 0;
            var cancellationRate = recentBookingsForStats.Any() ? (recentBookingsForStats.Count(b => b.Status == BookingStatus.Cancelled) * 100.0 / recentBookingsForStats.Count) : 0;
            var totalNights = recentBookingsForStats.Sum(b => b.TotalNights);
            
            // Dữ liệu cho biểu đồ (7 ngày qua) - trừ hoa hồng
            var chartData = new List<object>();
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.Today.AddDays(-i);
                var dayBookings = bookings.Where(b => b.CreatedAt.Date == date.Date).ToList();
                var dayRevenue = dayBookings.Sum(b => b.TotalPrice * 0.8m); // Trừ 20% hoa hồng
                var dayBookingsCount = dayBookings.Count;
                
                chartData.Add(new
                {
                    Date = date.ToString("dd/MM"),
                    Revenue = dayRevenue,
                    Bookings = dayBookingsCount
                });
            }

            ViewBag.Property = property;
            ViewBag.RoomCount = await _db.Rooms.CountAsync(r => r.PropertyId == propertyId);
            ViewBag.HasPricePackage = await _db.PricePackages.AnyAsync(p => p.PropertyId == propertyId);
            ViewBag.HasRoomPrices = await _db.RoomPrices.AnyAsync(p => p.PropertyId == propertyId);
            
            // Dữ liệu booking
            ViewBag.RecentBookings = recentBookings;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.AverageDailyRate = averageDailyRate;
            ViewBag.CancellationRate = cancellationRate;
            ViewBag.TotalNights = totalNights;
            ViewBag.ChartData = chartData;
            
            return View("PropertyHub");
        }

        [HttpGet]
        public async Task<IActionResult> AllBookings(int propertyId)
        {
            await SetUserHasPropertiesAsync();
            var meId = _users.GetUserId(User);

            // Kiểm tra quyền truy cập
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == meId);
            if (property == null) return NotFound();

            // Lấy tất cả booking cho property này với thông tin phòng
            var bookings = await _db.Bookings
                .Where(b => b.PropertyId == propertyId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            // Lấy thông tin phòng cho mỗi booking
            var bookingWithRooms = new List<dynamic>();
            foreach (var booking in bookings)
            {
                var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == booking.RoomId);
                
                bookingWithRooms.Add(new
                {
                    Booking = booking,
                    RoomName = room?.Name ?? "Phòng không xác định"
                });
            }

            ViewBag.Property = property;
            ViewBag.Bookings = bookingWithRooms;
            
            return View();
        }


        // Shortcut from global nav: tự chọn property gần nhất và chuyển tới Hub
        [HttpGet]
        public async Task<IActionResult> GoToHub()
        {
            await SetUserHasPropertiesAsync();
            var meId = _users.GetUserId(User);
            var property = await _db.Properties
                .Where(p => p.UserId == meId)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .FirstOrDefaultAsync();
            if (property == null) return RedirectToAction("MyProperties");
            return RedirectToAction("PropertyHub", new { propertyId = property.Id });
        }

        // Trang quản lý cơ sở lưu trú tổng quan
        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            await SetUserHasPropertiesAsync();
            var userId = _users.GetUserId(User);
            var properties = await _db.Properties.Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            var stats = new
            {
                Total = properties.Count,
                Draft = properties.Count(p => p.Status == PropertyStatus.Draft),
                Submitted = properties.Count(p => p.Status == PropertyStatus.Submitted),
                Approved = properties.Count(p => p.Status == PropertyStatus.Approved)
            };
            ViewBag.Stats = stats;
            return View("Manage", properties);
        }

        // ========== LỊCH GIÁ & PHÒNG TRỐNG ==========
        [HttpGet]
        public async Task<IActionResult> Calendar(int propertyId, int? year = null, int? month = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            await SetUserHasPropertiesAsync();
            var meId = _users.GetUserId(User);
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == meId);
            if (property == null) return NotFound();

            // Support both year/month and startDate/endDate parameters
            DateTime start, end;
            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value;
                end = endDate.Value;
            }
            else if (year.HasValue && month.HasValue)
            {
                start = new DateTime(year.Value, month.Value, 1);
                end = start.AddMonths(1);
            }
            else
            {
                // Default to next 30 days
                start = DateTime.Today;
                end = start.AddDays(30);
            }

            ViewBag.PropertyId = propertyId;
            ViewBag.StartDate = start;
            ViewBag.EndDate = end;
            ViewBag.PropertyName = property.Name;

            // Load all rooms grouped by type
            var rooms = await _db.Rooms
                .Where(r => r.PropertyId == propertyId)
                .OrderBy(r => r.Name)
                .ToListAsync();

            var roomsByType = rooms
                .GroupBy(r => r.Name.Split(' ').FirstOrDefault() ?? "Other")
                .OrderBy(g => g.Key)
                .ToList();

            // Load all bookings for the date range
            var bookings = await _db.Bookings
                .Where(b => b.PropertyId == propertyId && 
                           b.Status != BookingStatus.Cancelled &&
                           b.CheckIn < end && 
                           b.CheckOut > start)
                .OrderBy(b => b.CheckIn)
                .ToListAsync();

            // Calculate availability map: roomType -> date -> (available, total)
            var availabilityMap = new Dictionary<string, Dictionary<DateTime, (int Available, int Total)>>();
            
            for (var date = start; date < end; date = date.AddDays(1))
            {
                // Overall availability
                if (!availabilityMap.ContainsKey("Overall"))
                    availabilityMap["Overall"] = new Dictionary<DateTime, (int, int)>();

                var totalRooms = rooms.Sum(r => r.Quantity);
                var bookedOnDate = bookings.Count(b => b.CheckIn <= date && b.CheckOut > date);
                var availableOnDate = totalRooms - bookedOnDate;
                availabilityMap["Overall"][date] = (availableOnDate, totalRooms);

                // Per room type availability
                foreach (var typeGroup in roomsByType)
                {
                    var typeName = typeGroup.Key;
                    if (!availabilityMap.ContainsKey(typeName))
                        availabilityMap[typeName] = new Dictionary<DateTime, (int, int)>();

                    var typeRooms = typeGroup.ToList();
                    var typeTotal = typeRooms.Sum(r => r.Quantity);
                    var typeBooked = bookings.Count(b => 
                        typeRooms.Any(r => r.Id == b.RoomId) && 
                        b.CheckIn <= date && 
                        b.CheckOut > date);
                    var typeAvailable = typeTotal - typeBooked;
                    availabilityMap[typeName][date] = (typeAvailable, typeTotal);
                }
            }

            ViewBag.RoomsByType = roomsByType;
            ViewBag.Bookings = bookings;
            ViewBag.AvailabilityMap = availabilityMap;

            return View("Calendar", rooms);
        }

        [HttpGet]
        public async Task<IActionResult> FetchMonth(int propertyId, int year, int month)
        {
            var meId = _users.GetUserId(User);
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.UserId == meId);
            if (property == null) return Unauthorized();

            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1);

            var rooms = await _db.Rooms.Where(r => r.PropertyId == propertyId)
                                        .OrderBy(r => r.Name)
                                        .Select(r => new { r.Id, r.Name, r.Quantity })
                                        .ToListAsync();

            // Calculate remaining rooms for each room type
            var roomQuantities = new Dictionary<int, object>();
            foreach (var room in rooms)
            {
                // Get all bookings for this room
                var allBookings = await _db.Bookings
                    .Where(b => b.RoomId == room.Id)
                    .ToListAsync();
                
                var bookedCount = allBookings
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .Count();
                
                var originalQuantity = room.Quantity;
                var remainingQuantity = originalQuantity - bookedCount;
                
                // Debug log
                Console.WriteLine($"Room {room.Id} ({room.Name}): Original={originalQuantity}, Booked={bookedCount}, Remaining={remainingQuantity}");
                Console.WriteLine($"All bookings for room {room.Id}: {string.Join(", ", allBookings.Select(b => $"ID:{b.Id}, Status:{b.Status}"))}");
                
                roomQuantities[room.Id] = new { original = originalQuantity, remaining = remainingQuantity };
            }

            var rates = await _db.RoomDailyRates
                .Where(x => x.PropertyId == propertyId && x.Date >= start && x.Date < end)
                .Select(x => new {
                    x.RoomId,
                    Date = x.Date,
                    x.Price,
                    x.IsClosed,
                    x.MinStayNights,
                    x.MaxStayNights,
                    x.Allotment
                })
                .ToListAsync();

            // Lấy giá đầu tiên cho mỗi RoomId (có thể có nhiều RoomPrice cho cùng RoomId)
            var basePrices = await _db.RoomPrices
                .Where(p => p.PropertyId == propertyId)
                .GroupBy(p => p.RoomId)
                .ToDictionaryAsync(g => g.Key, g => g.First().Amount);

            return Json(new { rooms, rates, basePrices, roomQuantities });
        }

        public class RateUpdateRequest
        {
            public int PropertyId { get; set; }
            public List<RateUpdateItem> Items { get; set; } = new();
        }
        public class RateUpdateItem
        {
            public int RoomId { get; set; }
            public DateTime Date { get; set; }
            public decimal? Price { get; set; }
            public bool? IsClosed { get; set; }
            public int? MinStayNights { get; set; }
            public int? MaxStayNights { get; set; }
            public int? Allotment { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> SaveRates([FromBody] RateUpdateRequest req)
        {
            var meId = _users.GetUserId(User);
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == req.PropertyId && p.UserId == meId);
            if (property == null) return Unauthorized();

            foreach (var it in req.Items)
            {
                var date = it.Date.Date;
                var existing = await _db.RoomDailyRates
                    .FirstOrDefaultAsync(x => x.PropertyId == req.PropertyId && x.RoomId == it.RoomId && x.Date == date);

                if (existing == null)
                {
                    existing = new RoomDailyRate
                    {
                        PropertyId = req.PropertyId,
                        RoomId = it.RoomId,
                        Date = date
                    };
                    _db.RoomDailyRates.Add(existing);
                }

                existing.Price = it.Price;
                if (it.IsClosed.HasValue) existing.IsClosed = it.IsClosed.Value;
                existing.MinStayNights = it.MinStayNights;
                existing.MaxStayNights = it.MaxStayNights;
                existing.Allotment = it.Allotment;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // Xem chi tiết booking
        [HttpGet]
        public async Task<IActionResult> BookingDetails(string bookingCode)
        {
            await SetUserHasPropertiesAsync();
            var meId = _users.GetUserId(User);
            
            var booking = await _db.Bookings
                .FirstOrDefaultAsync(b => b.BookingCode == bookingCode && b.PropertyId > 0);
            
            if (booking == null) return NotFound();
            
            // Kiểm tra quyền truy cập
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == booking.PropertyId && p.UserId == meId);
            if (property == null) return NotFound();
            
            // Lấy thông tin phòng
            var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == booking.RoomId);
            
            ViewBag.Booking = booking;
            ViewBag.Room = room;
            ViewBag.Property = property;
            
            return View();
        }

        // ========== DISCOUNT CODE MANAGEMENT ==========
        [HttpGet]
        public async Task<IActionResult> DiscountIndex()
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();

            // Lấy tất cả properties của partner
            var userId = me.Id;
            var propertyIds = await _db.Properties
                .Where(p => p.UserId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            // Lấy tất cả mã giảm giá của partner (PropertyId trong danh sách propertyIds)
            var discounts = await _db.Discounts
                .Where(d => d.PropertyId.HasValue && propertyIds.Contains(d.PropertyId.Value))
                .OrderByDescending(d => d.Id)
                .ToListAsync();

            return View(discounts);
        }

        [HttpGet]
        public async Task<IActionResult> DiscountCreate()
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();

            // Lấy danh sách properties của partner để hiển thị trong dropdown
            var userId = me.Id;
            var properties = await _db.Properties
                .Where(p => p.UserId == userId)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            ViewBag.Properties = properties;

            var model = new Discount 
            { 
                StartDate = DateTime.Today, 
                EndDate = DateTime.Today.AddMonths(1) 
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DiscountCreate(Discount model, int? selectedPropertyId)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();

            // Kiểm tra propertyId có thuộc về partner không
            if (selectedPropertyId.HasValue)
            {
                var userId = me.Id;
                var isValidProperty = await _db.Properties
                    .AnyAsync(p => p.Id == selectedPropertyId.Value && p.UserId == userId);

                if (!isValidProperty)
                {
                    ModelState.AddModelError("", "Cơ sở lưu trú không hợp lệ.");
                }
                else
                {
                    model.PropertyId = selectedPropertyId.Value;
                }
            }
            else
            {
                ModelState.AddModelError("", "Vui lòng chọn cơ sở lưu trú.");
            }

            ValidateDiscountModel(model);
            if (!ModelState.IsValid)
            {
                // Load lại danh sách properties
                var userId = me.Id;
                var properties = await _db.Properties
                    .Where(p => p.UserId == userId)
                    .Select(p => new { p.Id, p.Name })
                    .ToListAsync();
                ViewBag.Properties = properties;
                return View(model);
            }

            model.Code = model.Code.Trim().ToUpperInvariant();
            _db.Discounts.Add(model);
            await _db.SaveChangesAsync();
            TempData["success"] = "Đã tạo mã giảm giá";
            return RedirectToAction(nameof(DiscountIndex));
        }

        [HttpGet]
        public async Task<IActionResult> DiscountEdit(int id)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();

            // Kiểm tra mã giảm giá có thuộc về partner không
            var userId = me.Id;
            var propertyIds = await _db.Properties
                .Where(p => p.UserId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            var discount = await _db.Discounts
                .FirstOrDefaultAsync(d => d.Id == id && d.PropertyId.HasValue && propertyIds.Contains(d.PropertyId.Value));

            if (discount == null)
            {
                TempData["error"] = "Không tìm thấy mã giảm giá hoặc bạn không có quyền chỉnh sửa mã này.";
                return RedirectToAction(nameof(DiscountIndex));
            }

            // Load danh sách properties của partner
            var properties = await _db.Properties
                .Where(p => p.UserId == userId)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            ViewBag.Properties = properties;
            ViewBag.CurrentPropertyId = discount.PropertyId;

            return View(discount);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DiscountEdit(int id, Discount model, int? selectedPropertyId)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();

            // Kiểm tra mã giảm giá có thuộc về partner không
            var userId = me.Id;
            var propertyIds = await _db.Properties
                .Where(p => p.UserId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            var existingDiscount = await _db.Discounts
                .FirstOrDefaultAsync(d => d.Id == id && d.PropertyId.HasValue && propertyIds.Contains(d.PropertyId.Value));

            if (existingDiscount == null)
            {
                TempData["error"] = "Không tìm thấy mã giảm giá hoặc bạn không có quyền chỉnh sửa mã này.";
                return RedirectToAction(nameof(DiscountIndex));
            }

            // Kiểm tra propertyId có thuộc về partner không (nếu có thay đổi)
            if (selectedPropertyId.HasValue)
            {
                var isValidProperty = propertyIds.Contains(selectedPropertyId.Value);
                if (!isValidProperty)
                {
                    ModelState.AddModelError("", "Cơ sở lưu trú không hợp lệ.");
                }
                else
                {
                    model.PropertyId = selectedPropertyId.Value;
                }
            }
            else
            {
                // Giữ nguyên PropertyId hiện tại
                model.PropertyId = existingDiscount.PropertyId;
            }

            ValidateDiscountModel(model);
            if (!ModelState.IsValid)
            {
                // Load lại danh sách properties
                var properties = await _db.Properties
                    .Where(p => p.UserId == userId)
                    .Select(p => new { p.Id, p.Name })
                    .ToListAsync();
                ViewBag.Properties = properties;
                ViewBag.CurrentPropertyId = existingDiscount.PropertyId;
                return View(model);
            }

            // Cập nhật thông tin
            existingDiscount.Code = model.Code.Trim().ToUpperInvariant();
            existingDiscount.Title = model.Title;
            existingDiscount.Description = model.Description;
            existingDiscount.DiscountPercent = model.DiscountPercent;
            existingDiscount.DiscountAmount = model.DiscountAmount;
            existingDiscount.StartDate = model.StartDate;
            existingDiscount.EndDate = model.EndDate;
            existingDiscount.IsActive = model.IsActive;
            existingDiscount.ImageUrl = model.ImageUrl;
            existingDiscount.PropertyId = model.PropertyId;

            _db.Discounts.Update(existingDiscount);
            await _db.SaveChangesAsync();
            TempData["success"] = "Đã cập nhật mã giảm giá";
            return RedirectToAction(nameof(DiscountIndex));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DiscountDelete(int id)
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            // Kiểm tra mã giảm giá có thuộc về partner không
            var userId = me.Id;
            var propertyIds = await _db.Properties
                .Where(p => p.UserId == userId)
                .Select(p => p.Id)
                .ToListAsync();

            var discount = await _db.Discounts
                .FirstOrDefaultAsync(d => d.Id == id && d.PropertyId.HasValue && propertyIds.Contains(d.PropertyId.Value));

            if (discount == null)
            {
                TempData["error"] = "Không tìm thấy mã giảm giá hoặc bạn không có quyền xóa mã này.";
                return RedirectToAction(nameof(DiscountIndex));
            }

            _db.Discounts.Remove(discount);
            await _db.SaveChangesAsync();
            TempData["success"] = "Đã xóa mã giảm giá";
            return RedirectToAction(nameof(DiscountIndex));
        }

        private void ValidateDiscountModel(Discount model)
        {
            if (string.IsNullOrWhiteSpace(model.Code))
            {
                ModelState.AddModelError(nameof(model.Code), "Mã không được để trống");
            }
            if (model.DiscountPercent is null && model.DiscountAmount is null)
            {
                ModelState.AddModelError(string.Empty, "Cần nhập phần trăm hoặc số tiền giảm");
            }
        }
    }
}
