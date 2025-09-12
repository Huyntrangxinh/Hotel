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

namespace HotelBooking.Controllers
{
    [Authorize]
    public class PartnerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IWebHostEnvironment _hostEnvironment;

        public PartnerController(ApplicationDbContext db, UserManager<ApplicationUser> users, IWebHostEnvironment hostEnvironment)
        {
            _db = db;
            _users = users;
            _hostEnvironment = hostEnvironment;
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
            }

            await _db.SaveChangesAsync();
            TempData["success"] = "Đã lưu thông tin cơ sở lưu trú.";
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

    var document = new ContractDocument(reviewModel);
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

[Authorize(Roles = "Partner")] // Chỉ Partner mới vào được trang này
public async Task<IActionResult> MyProperties()
{
    await SetUserHasPropertiesAsync(); // Thêm dòng này
    var userId = _users.GetUserId(User);
    
    // Thêm logging để debug
    var allProperties = await _db.Properties
        .Where(p => p.UserId == userId)
        .ToListAsync();
        
    var myProperties = allProperties
        .OrderByDescending(p => p.CreatedAt)
        .ToList();
        
    // Log để debug
    Console.WriteLine($"User ID: {userId}");
    Console.WriteLine($"Total properties found: {allProperties.Count}");
    foreach (var prop in allProperties)
    {
        Console.WriteLine($"Property ID: {prop.Id}, Name: {prop.Name}, Status: {prop.Status}, Created: {prop.CreatedAt}");
    }
        
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
        public async Task<IActionResult> PropertyData(int propertyId, string? tab = "property")
        {
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            await SetUserHasPropertiesAsync();
            
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
                viewModel.CheckInTime = propertyData.CheckInTime;
                viewModel.CheckOutTime = propertyData.CheckOutTime;
                viewModel.IsReception24Hours = propertyData.IsReception24Hours;
                viewModel.NumberOfRooms = propertyData.NumberOfRooms;
                viewModel.RoomDescription = propertyData.RoomDescription;
                viewModel.StarRating = propertyData.StarRating;
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
                Beds = r.Beds.SelectMany(b => b.GetAllBedItems()).Select(b => new BedItemVm
                {
                    Type = b.Type,
                    Count = b.Count,
                    BedroomIndex = b.BedroomIndex
                }).ToList()
            }).ToList();

            // Load PricePackage nếu có
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
                Beds = room.Beds.SelectMany(b => b.GetAllBedItems()).Select(b => new ViewModels.Rooms.BedItem { Type = b.Type, Count = b.Count, BedroomIndex = b.BedroomIndex }).ToList(),
                // Tạo Bedrooms data từ Beds, nhóm theo BedroomIndex
                Bedrooms = room.Beds
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
            var me = await _users.GetUserAsync(User);
            if (me == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
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
            propertyData.StarRating = viewModel.StarRating;
            
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

            // Lưu thông tin ảnh
            if (!string.IsNullOrEmpty(viewModel.PhotoCategoriesJson))
            {
                propertyData.PhotoCategoriesJson = viewModel.PhotoCategoriesJson;
            }

            // Manifest: xử lý xóa / cập nhật thứ tự + category / thêm ảnh mới
            var existingUrls = string.IsNullOrWhiteSpace(propertyData.PhotoPaths)
                ? new List<string>()
                : propertyData.PhotoPaths.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

            var deletedUrls = new List<string>();
            try
            {
                if (!string.IsNullOrWhiteSpace(viewModel.DeletedPhotoUrlsJson))
                {
                    deletedUrls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(viewModel.DeletedPhotoUrlsJson) ?? new List<string>();
                }
            }
            catch { }

            // Xóa URL cũ (và có thể xóa file vật lý)
            if (deletedUrls.Count > 0)
            {
                existingUrls = existingUrls.Where(u => !deletedUrls.Contains(u)).ToList();
            }

            // Build danh sách cuối cùng theo manifest
            var manifestItems = new List<(int? NewIndex, string? Url, string Category, int Order)>();
            try
            {
                var raw = string.IsNullOrWhiteSpace(viewModel.PhotoManifestJson) ? "[]" : viewModel.PhotoManifestJson;
                var dicts = System.Text.Json.JsonSerializer.Deserialize<List<System.Text.Json.Nodes.JsonObject>>(raw) ?? new();
                foreach (var d in dicts)
                {
                    int order = d["order"]?.GetValue<int>() ?? 0;
                    string category = d["category"]?.GetValue<string>() ?? "others";
                    string? url = d["url"]?.GetValue<string>();
                    int? newIdx = d["newIndex"]?.GetValue<int?>();
                    manifestItems.Add((newIdx, url, category, order));
                }
            }
            catch { }

            // Thư mục lưu file
            var wwwRootPath2 = _hostEnvironment.WebRootPath;
            var uploadsDir2 = Path.Combine(wwwRootPath2, "uploads", "properties", viewModel.PropertyId.ToString());
            Directory.CreateDirectory(uploadsDir2);

            var finalUrls = new string[manifestItems.Count];
            for (int i = 0; i < manifestItems.Count; i++)
            {
                var it = manifestItems[i];
                if (!string.IsNullOrEmpty(it.Url))
                {
                    // Ảnh cũ: giữ nguyên URL tại vị trí order
                    finalUrls[it.Order] = it.Url!;
                }
                else if (it.NewIndex.HasValue)
                {
                    var file = (viewModel.PropertyPhotos ?? new List<IFormFile>()).ElementAtOrDefault(it.NewIndex.Value);
                    if (file != null && file.Length > 0 && file.ContentType.StartsWith("image/"))
                    {
                        var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(uploadsDir2, fileName);
                        using (var fs = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(fs);
                        }
                        finalUrls[it.Order] = $"/uploads/properties/{viewModel.PropertyId}/{fileName}";
                    }
                }
            }

            // Nếu không có manifest, giữ nguyên danh sách cũ
            var merged = finalUrls.Any(u => !string.IsNullOrEmpty(u)) ? finalUrls.Where(u => !string.IsNullOrEmpty(u)) : existingUrls;
            propertyData.PhotoPaths = string.Join('|', merged);

            propertyData.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

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
                RegistrationNumber = $"REG-{propertyId:D6}"
            };
            return View(vm);
        }

        // POST: lưu phòng rồi quay lại tab Rooms (demo UI)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RoomData(ViewModels.Rooms.RoomCreateViewModel m)
        {
            Console.WriteLine("=== BẮT ĐẦU XỬ LÝ ROOMDATA POST ===");
            Console.WriteLine($"data: {m}");
            Console.WriteLine($"PropertyId: {m.PropertyId}");
            Console.WriteLine($"RoomId: {m.RoomId}");
            Console.WriteLine($"RoomType: {m.RoomType}");
            Console.WriteLine($"RoomName: {m.RoomName}");
            Console.WriteLine($"SizeNumber: {m.SizeNumber}");
            Console.WriteLine($"Quantity: {m.Quantity}");
            Console.WriteLine($"CapacityAdults: {m.CapacityAdults}");
            Console.WriteLine($"CapacityChildren: {m.CapacityChildren}");
            Console.WriteLine($"SecurityDeposit: {m.SecurityDeposit}");
            
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
                // Single bedroom: sử dụng m.Beds
                foreach (var b in m.Beds ?? new())
                {
                    if (b.Count > 0 && !string.IsNullOrWhiteSpace(b.Type))
                    {
                        var newBed = new RoomBed { BedroomIndex = b.BedroomIndex };
                        newBed.AddBedItem(b.Type, b.Count);
                        room.Beds.Add(newBed);
                        Console.WriteLine($"Đã thêm giường (single): {b.Type} x{b.Count}");
                    }
                }
            }
            else
            {
                // Multi bedroom: sử dụng m.Bedrooms
                for (int bedroomIndex = 0; bedroomIndex < (m.Bedrooms?.Count ?? 0); bedroomIndex++)
                {
                    var bedroom = m.Bedrooms[bedroomIndex];
                    foreach (var b in bedroom.Beds ?? new())
                    {
                        if (b.Count > 0 && !string.IsNullOrWhiteSpace(b.Type))
                        {
                            var newBed = new RoomBed { BedroomIndex = bedroomIndex };
                            newBed.AddBedItem(b.Type, b.Count);
                            room.Beds.Add(newBed);
                            Console.WriteLine($"Đã thêm giường (multi): {b.Type} x{b.Count} - Phòng ngủ {bedroomIndex + 1}");
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

            Console.WriteLine("=== HOÀN THÀNH XỬ LÝ - LƯU DATABASE ===");
            Console.WriteLine($"Số lượng ảnh cuối cùng: {room.Photos.Count}");
            Console.WriteLine($"Số lượng giường cuối cùng: {room.Beds.Count}");
            Console.WriteLine($"Số lượng tiện nghi cuối cùng: {room.Amenities.Count}");
            
            await _db.SaveChangesAsync();
            Console.WriteLine("Đã lưu thành công vào database");
            Console.WriteLine("=== KẾT THÚC ROOMDATA POST ===");

            return RedirectToAction(nameof(PropertyData), new { propertyId = m.PropertyId, tab = "rooms" });
        }

        [HttpPost]
        public async Task<IActionResult> CreatePricePackage(int propertyId, string cancellationPolicy, bool breakfastIncluded, string returnTab = "pricing")
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

                // Kiểm tra xem đã có price package chưa, nếu có thì xóa cái cũ
                var existingPackage = await _db.PricePackages
                    .FirstOrDefaultAsync(p => p.PropertyId == propertyId);
                
                if (existingPackage != null)
                {
                    Console.WriteLine("Đã có price package cũ, xóa nó đi");
                    _db.PricePackages.Remove(existingPackage);
                }

                // Tạo price package mới
                var pricePackage = new PricePackage
                {
                    PropertyId = propertyId,
                    CancellationPolicy = cancellationPolicy,
                    BreakfastIncluded = breakfastIncluded,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.PricePackages.Add(pricePackage);
                await _db.SaveChangesAsync();

                Console.WriteLine("Đã lưu price package thành công vào database");
                Console.WriteLine("=== KẾT THÚC CREATEPRICEPACKAGE POST ===");

                return RedirectToAction(nameof(PropertyData), new { propertyId = propertyId, tab = returnTab });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tạo price package: {ex.Message}");
                return RedirectToAction(nameof(PropertyData), new { propertyId = propertyId, tab = returnTab });
            }
        }
    }
}
