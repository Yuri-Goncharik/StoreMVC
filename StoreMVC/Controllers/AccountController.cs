﻿using System;
using System.Collections.Generic;
using System.Linq;
//using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using DotNetOpenAuth.AspNet;
using Microsoft.Web.WebPages.OAuth;
using WebMatrix.WebData;
using System.Drawing.Imaging;

using StoreMVC.Models;
using StoreMVC.Filters;

namespace StoreMVC.Controllers
{
	[Authorize]
	[InitializeSimpleMembershipAttribute]
	public class AccountController : Controller
	{
		private DBStoreMVC db = new DBStoreMVC();

		//
		// GET: /Account/Login
		[AllowAnonymous]
		public ActionResult Login(string returnUrl)
		{
			ViewBag.ReturnUrl = returnUrl;
			return View();
		}

		//
		// POST: /Account/Login

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public ActionResult Login(LoginModel model, string returnUrl)
		{
			// WebSecurity.Login - аутентифицирует пользователя.
			// Если логин и пароль введены правильно - метод возвращает значение true после чего выполняет добавление специальных значений в cookies.
			if (ModelState.IsValid && WebSecurity.Login(model.UserName, model.Password, persistCookie: model.RememberMe))
			{
				return RedirectToLocal(returnUrl);
			}

			// Был введен не правильный логин или пароль
			ModelState.AddModelError("", "The user name or password provided is incorrect.");
			return View(model);
		}

		//
		// POST: /Account/LogOff

		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult LogOff()
		{
			// Удаление специальных аутентификационных cookie значений
			WebSecurity.Logout();

			return RedirectToAction("Index", "Home");
		}

		//
		// GET: /Account/Register

		[AllowAnonymous]
		public ActionResult Register()
		{
			return View();
		}

		//
		// POST: /Account/Register

		[HttpPost]
		[AllowAnonymous]
		[ValidateAntiForgeryToken]
		public ActionResult Register([Bind(Include = "UserName,Password,ConfirmPassword,FirstName,LastName,Patronymic,Email,Captcha")] RegisterModel model)
		{
			if (model.Captcha != (string)Session["code"])
			{
				ModelState.AddModelError("Captcha", "You enter wrong simbols from captcha image");
			}
			if (ModelState.IsValid)
			{
				// Attempt to register the user
				try
				{
					// Создание пользователя
					WebSecurity.CreateUserAndAccount(model.UserName, model.Password, new { model.FirstName, model.LastName, model.Patronymic, model.Email });
					// Аутентификация пользователя
					WebSecurity.Login(model.UserName, model.Password);
					return RedirectToAction("Index", "Home");
				}
				catch (MembershipCreateUserException e)
				{
					ModelState.AddModelError("General", ErrorCodeToString(e.StatusCode));
				}
				catch (Exception e)
				{
					ModelState.AddModelError("General", "Somesing gone wrong. \n Error: " + e);
				}
			}

			// If we got this far, something failed, redisplay form
			return View(model);
		}


		public ActionResult AccountManage()
		{
			int userId = WebSecurity.GetUserId(User.Identity.Name);
			UserProfile currentUser = db.UserProfiles.Find(userId);

			return View(currentUser);
		}

		//
		// GET: /Account/PasswordChange
		public ActionResult PasswordChange(ManageMessageId? message)
		{
			ViewBag.StatusMessage =
				message == ManageMessageId.ChangePasswordSuccess ? "Your password has been changed."
				: message == ManageMessageId.SetPasswordSuccess ? "Your password has been set."
				: message == ManageMessageId.RemoveLoginSuccess ? "The external login was removed."
				: "";
			ViewBag.HasLocalPassword = OAuthWebSecurity.HasLocalAccount(WebSecurity.GetUserId(User.Identity.Name));
			ViewBag.ReturnUrl = Url.Action("PasswordChange");

			return View();
		}

		//
		// POST: /Account/PasswordChange
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult PasswordChange(LocalPasswordModel model)
		{
			if (ModelState.IsValid)
			{
				bool changePasswordSucceeded;
				try
				{
					// ChangePassword выбрасывает исключения в случае не удачной попытки смены пароля.
					changePasswordSucceeded = WebSecurity.ChangePassword(User.Identity.Name, model.OldPassword, model.NewPassword);
				}
				catch (Exception)
				{
					changePasswordSucceeded = false;
				}

				if (changePasswordSucceeded)
				{
					return RedirectToAction("PasswordChange", new { Message = ManageMessageId.ChangePasswordSuccess });
				}
				else
				{
					ModelState.AddModelError("", "The current password is incorrect or the new password is invalid.");
				}
			}

			// If we got this far, something failed, redisplay form
			return View("PasswordChange", model);
		}

		#region Helpers
		private ActionResult RedirectToLocal(string returnUrl)
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

		public enum ManageMessageId
		{
			ChangePasswordSuccess,
			SetPasswordSuccess,
			RemoveLoginSuccess,
		}

		private static string ErrorCodeToString(MembershipCreateStatus createStatus)
		{
			// See http://go.microsoft.com/fwlink/?LinkID=177550 for
			// a full list of status codes.
			switch (createStatus)
			{
				case MembershipCreateStatus.DuplicateUserName:
					return "User name already exists. Please enter a different user name.";

				case MembershipCreateStatus.DuplicateEmail:
					return "A user name for that e-mail address already exists. Please enter a different e-mail address.";

				case MembershipCreateStatus.InvalidPassword:
					return "The password provided is invalid. Please enter a valid password value.";

				case MembershipCreateStatus.InvalidEmail:
					return "The e-mail address provided is invalid. Please check the value and try again.";

				case MembershipCreateStatus.InvalidAnswer:
					return "The password retrieval answer provided is invalid. Please check the value and try again.";

				case MembershipCreateStatus.InvalidQuestion:
					return "The password retrieval question provided is invalid. Please check the value and try again.";

				case MembershipCreateStatus.InvalidUserName:
					return "The user name provided is invalid. Please check the value and try again.";

				case MembershipCreateStatus.ProviderError:
					return "The authentication provider returned an error. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

				case MembershipCreateStatus.UserRejected:
					return "The user creation request has been canceled. Please verify your entry and try again. If the problem persists, please contact your system administrator.";

				default:
					return "An unknown error occurred. Please verify your entry and try again. If the problem persists, please contact your system administrator.";
			}
		}
		#endregion

		[Authorize]
		public ActionResult Home()
		{
			if (User.IsInRole("Moderator"))
			{
				return RedirectToAction("ModeratorPanel");
			}
			else if (User.IsInRole("Admin"))
			{
				return RedirectToAction("AdminPanel");
			}
			else
				return RedirectToAction("Cabinet");
		}

		[Authorize]
		public ActionResult Cabinet()
		{
			return View();
		}

		public ActionResult ModeratorPanel()
		{
			return View();
		}

		[Authorize(Roles = "Admin")]
		public ActionResult AdminPanel()
		{
			return View();
		}

		public ActionResult AccountsEditPanel()
		{
			List<UserProfile> AccountsList = db.UserProfiles.ToList();
			return View(AccountsList);
		}

		//public ActionResult AccountEdit()
		//{
		//	return View();
		//}

		[HttpPost]
		[Authorize(Roles = "Admin")]
		[ValidateAntiForgeryToken]
		public ActionResult AccountEdit([Bind(Include = "UserName,Password,ConfirmPassword,FirstName,LastName,Patronymic,Email")] UserProfile userProfile)
		{
			if (ModelState.IsValid)
			{
			}
			return View();
		}

		[Authorize(Roles = "Moderator")] // К данному методу действия могут получать доступ только пользователи с ролью Admin и Moderator

		[AllowAnonymous]
		public ActionResult Captcha()
		{
			string code = new Random(DateTime.Now.Millisecond).Next(1111, 9999).ToString();
			Session["code"] = code;
			CaptchaImage captcha = new CaptchaImage(code, 110, 50);

			this.Response.Clear();
			this.Response.ContentType = "image/jpeg";

			captcha.Image.Save(this.Response.OutputStream, ImageFormat.Jpeg);

			captcha.Dispose();
			return null;
		}
	}
}

