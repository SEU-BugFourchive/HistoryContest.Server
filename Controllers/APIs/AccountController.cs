using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using HistoryContest.Server.Data;
using HistoryContest.Server.Services;
using HistoryContest.Server.Models.Entities;
using HistoryContest.Server.Models.ViewModels;
using System.Security.Claims;

namespace HistoryContest.Server.Controllers.APIs
{
    [Authorize]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class AccountController : Controller
    {
        private readonly UnitOfWork unitOfWork;
        private readonly AccountService accountService;

        public AccountController(UnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            accountService = new AccountService(unitOfWork);
        }

        /// <summary>
        /// ��¼
        /// </summary>
        /// <remarks>
        /// ��¼�ɹ��󣬽�������û���Ϊ�û�������ݣ����������Ϊ�û���ʼ�����Ե�Session��
        /// 
        /// ��һ������ֵ��ǵ�¼�Ƿ�ɹ�
        /// </remarks>
        /// <param name="model">�û���������</param>
        /// <returns>ѧ�Ŷ�Ӧ�Ŀ��Խ��</returns>
        /// <response code="200">
        /// ���ص�¼���������JSON��ʽ������
        /// 
        ///     ʧ��ʱ��
        ///     {
        ///         "isSuccessful": false    
        ///     }
        /// 
        ///     �ɹ�ʱ��
        ///     {
        ///         "isSuccessful": true,
        ///         "userViewModel": {
        ///             "userName": "09016319",
        ///             "realName": "Ҷ־��",
        ///             "role": "Student"
        ///         }
        ///     }
        /// </response>
        [AllowAnonymous]
        [HttpPost("[action]")]
        [ProducesResponseType(typeof(UserViewModel), StatusCodes.Status200OK)]
        public async Task<IActionResult> Login([FromBody]LoginViewModel model)
        {
            if(!ModelState.IsValid)
            {
                return BadRequest("Body JSON content invalid"); 
            }
            

            var userContext = await accountService.ValidateUser(model.UserName, model.Password);
            if (userContext.UserViewModel != null)
            {
                InitializeSession(userContext);
                var principal = new ClaimsPrincipal(new ClaimsIdentity(userContext.Claims, accountService.GetType().Name));
#if NETCOREAPP2_0
                await HttpContext.SignInAsync(principal);
#else
                await HttpContext.Authentication.SignInAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, principal);
#endif
                return Json(new { isSuccessful = true, userContext.UserViewModel });
            }
            else
            {
                return Json(new { isSuccessful = false });
            }
        }

        //[AllowAnonymous]
        //[HttpPost("[action]")]
        //public IActionResult Register(RegistrationViewModel model)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest();
        //    }

        //    UserViewModel user = accountService.CreateUser(model.UserName, model.Password, model.role?? "Student");
        //    if (user != null)
        //    {
        //        return Json(new { isSuccessful = true, user });
        //    }
        //    else
        //    {
        //        return Json(new { isSuccessful = false });
        //    }
        //}

        /// <summary>
        /// ע��
        /// </summary>
        /// <remarks>
        /// �ǳ���ǰ�û����������ǰ�û�Session�е��������ݡ�
        /// </remarks>
        /// <response code="200">�ɹ�ע��</response>
        [HttpPost("[action]")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return SignOut();
        }

        [NonAction]
        private void InitializeSession(AccountContext context)
        {
            HttpContext.Session.SetString("id", context.UserViewModel.UserName);
            switch (context.UserViewModel.Role)
            {
                case nameof(Administrator):
                    break;
                case nameof(Counselor):
                    var counselor = (Counselor)context.UserEntity;
                    HttpContext.Session.SetInt32("department", (int)counselor.Department);
                    break;
                case nameof(Student):
                    var student = (Student)context.UserEntity;
                    HttpContext.Session.SetInt32("isTested", student.IsTested ? 1 : 0);
                    break;
                default:
                    throw new TypeLoadException("User role invalid");

            }
        }
    }
}