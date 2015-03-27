﻿using System;
using System.Diagnostics;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AspNetIdentity.Models;
using AspNetIdentity.Services;
using AspNetIdentity.ViewModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Mvc;

namespace AspNetIdentity.Controllers
{
    public class AccountController : Controller
    {
        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public UserManager<ApplicationUser> UserManager
        {
            get;
            private set;
        }

        public SignInManager<ApplicationUser> SignInManager
        {
            get;
            private set;
        }

        #region /Account/Login
        /**
         * GET /Account/Login
         */
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        /**
         * POST /Account/Login
         */
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            if (ModelState.IsValid)
            {
                var result = await SignInManager.PasswordSignInAsync(model.UserName, model.Password, model.RememberMe, shouldLockout: false);
                if (result.Succeeded)
                {
                    return RedirectToLocal(returnUrl);
                }
                if (result.IsLockedOut)
                {
                    ModelState.AddModelError("", "Locked Out");
                }
                else if (result.IsNotAllowed)
                {
                    ModelState.AddModelError("", "Not Allowed");
                }
                else if (result.RequiresTwoFactor)
                {
                    ModelState.AddModelError("", "Requires Two-Factor Authentication");
                }
                else
                {
                    ModelState.AddModelError("", "Invalid username or password.");
                }
                return View(model);
            }

            // If we got this far, something failed - redisplay the form
            return View(model);
        }
        #endregion

        #region /Account/Register
        /**
         * GET: /Account/Register
         */
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        /**
         * POST: /Account/Register
         */
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                Debug.WriteLine("Register: Validating Email Address");
                if (!IsValidEmail(model.Email))
                {
                    Debug.WriteLine(string.Format("Register: Email Address is not valid"));
                    ModelState.AddModelError("", "Invalid email address");
                    return View(model);
                }

                Debug.WriteLine("Register: Creating new ApplicationUser");
                var user = new ApplicationUser { UserName = model.Email, Email = model.Email };
                Debug.WriteLine(string.Format("Register: New Application User = {0}", user.UserName));
                var result = await UserManager.CreateAsync(user, model.Password);
                Debug.WriteLine(string.Format("Register: Registration = {0}", result.Succeeded));
                if (result.Succeeded)
                {
                    Debug.WriteLine("Register: Sending Email Code");
                    var code = await UserManager.GenerateEmailConfirmationTokenAsync(user);
                    Debug.WriteLine(string.Format("Register: Email for code {0} is {1}", model.Email, code));
                    var callBackUrl = Url.Action("ConfirmEmail", "Account",
                        new { userId = user.Id, code = code },
                        protocol: Context.Request.Scheme);
                    try {
                        await EmailService.Instance.SendEmailAsync(model.Email,
                            "Confirm your account",
                            "Please confirm your account by clicking this link: <a href=\"" + callBackUrl + "\">link</a>");
                        ViewBag.Link = callBackUrl;
                        return View("RegisterEmail");
                    }
                    catch (SmtpException ex)
                    {
                        Debug.WriteLine("Could not send email: " + ex.InnerException.Message);
                        ModelState.AddModelError("", "Could not send email");
                        return View(model);
                    }
                }
                foreach (var error in result.Errors)
                {
                    Debug.WriteLine(string.Format("Register: Adding Error: {0}:{1}", error.Code, error.Description));
                    ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }
            // Somethign went wrong, but we don't know what
            return View(model);
        }

        /**
         * GET: /Account/ConfirmEmail
         */
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(string userId, string code)
        {
            Debug.WriteLine("ConfirmEmail: Checking for userId = " + userId);
            if (userId == null || code == null)
            {
                Debug.WriteLine("ConfirmEmail: Invalid Parameters");
                return View("ConfirmEmailError");
            }
            Debug.WriteLine("ConfirmEmail: Looking for userId");
            var user = await UserManager.FindByIdAsync(userId);
            if (user == null)
            {
                Debug.WriteLine("ConfirmEmail: Could not find user");
                return View("ConfirmEmailError");
            }
            Debug.WriteLine("ConfirmEmail: Found user - checking confirmation code");
            var result = await UserManager.ConfirmEmailAsync(user, code);
            Debug.WriteLine("ConfirmEmail: Code Confirmation = " + result.Succeeded.ToString());
            return View(result.Succeeded ? "ConfirmEmail" : "ConfirmEmailError");
        }
        #endregion

        #region Logoff
        /**
         * POST: /Account/Logout
         */
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            SignInManager.SignOut();
            return RedirectToAction("Index", "Home");
        }
        #endregion

        #region Helpers
        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        public bool IsValidEmail(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;

            // Return true if strIn is in valid e-mail format.
            try
            {
                return Regex.IsMatch(s, @"^(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))$",
                      RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }
        #endregion
    }
}
