using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using HotelBooking.Models;
using System.Security.Claims;

namespace HotelBooking.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signInManager;

        // Đổi tên biến _signIn thành _signInManager cho rõ nghĩa
        public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager)
        {
            _users = users;
            _signInManager = signInManager;
        }

        // GET: /account/register
        [HttpGet]
        public IActionResult Register()
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Giữ nguyên logic kiểm tra email đã tồn tại của bạn
            var existed = await _users.FindByEmailAsync(model.Email);
            if (existed != null)
            {
                ModelState.AddModelError(nameof(model.Email),
                    "Địa chỉ email này đã được đăng ký trên hệ thống. Vui lòng dùng email khác hoặc đăng nhập.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName
            };

            var result = await _users.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // === BẮT ĐẦU CODE MỚI: GÁN VAI TRÒ ===
                if (user.Email != null && user.Email.EndsWith("@staff.com", StringComparison.OrdinalIgnoreCase))
                {
                    await _users.AddToRoleAsync(user, "Staff");
                }
                else
                {
                    await _users.AddToRoleAsync(user, "Partner");
                }
                // === KẾT THÚC CODE MỚI ===

                // Giữ nguyên logic chuyển hướng về trang Login của bạn
                TempData["success"] = "Tạo tài khoản thành công. Vui lòng đăng nhập để tiếp tục.";
                return RedirectToAction("Login");
            }

            // Giữ nguyên logic xử lý lỗi của bạn
            foreach (var e in result.Errors)
            {
                if (e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase) ||
                    e.Description.Contains("email", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(model.Email),
                        "Địa chỉ email này đã được đăng ký trên hệ thống. Vui lòng dùng email khác hoặc đăng nhập.");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, e.Description);
                }
            }

            return View(model);
        }

        // GET: /account/login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Index", "Home");
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /account/login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                // Staff có thể sử dụng các tính năng booking bình thường
                // Chỉ chuyển hướng đến Admin Dashboard nếu có returnUrl cụ thể
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);
                
                // Mặc định chuyển về trang chủ cho tất cả user
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        // POST: /account/logout
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();

        // === External Login with Google ===
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string? returnUrl = null)
        {
            var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
            var props = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(props, provider);
        }

        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
        {
            if (!string.IsNullOrEmpty(remoteError))
            {
                TempData["error"] = $"Đăng nhập thất bại: {remoteError}";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["error"] = "Không lấy được thông tin đăng nhập ngoài.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var signIn = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
            if (signIn.Succeeded)
            {
                return (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
            }

            var email = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email);
            var name = info.Principal.FindFirstValue(System.Security.Claims.ClaimTypes.Name) ?? email;

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["error"] = "Tài khoản Google không cung cấp email.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var user = await _users.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser { UserName = email, Email = email, FullName = name };
                var createRes = await _users.CreateAsync(user);
                if (createRes.Succeeded)
                {
                    await _users.AddToRoleAsync(user, "Partner");
                }
                else
                {
                    TempData["error"] = string.Join("; ", createRes.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(Login), new { returnUrl });
                }
            }

            var addLoginRes = await _users.AddLoginAsync(user, info);
            if (!addLoginRes.Succeeded)
            {
                TempData["error"] = string.Join("; ", addLoginRes.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            await _signInManager.SignInAsync(user, isPersistent: true);
            return (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
        }
    }
}