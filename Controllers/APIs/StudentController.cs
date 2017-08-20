using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HistoryContest.Server.Data;
using HistoryContest.Server.Models.Entities;
using HistoryContest.Server.Extensions;
using HistoryContest.Server.Services;

namespace HistoryContest.Server.Controllers.APIs
{
    [Authorize(Roles = "Student, Administrator")]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class StudentController : Controller
    {
        private readonly UnitOfWork unitOfWork;
        private readonly QuestionSeedService questionSeedService;

        public StudentController(UnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            questionSeedService = new QuestionSeedService(unitOfWork);
        }

        #region State APIs

        /// <summary>
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns></returns>
        /// <response code=""></response>
        /// <response code=""></response>
        [HttpGet("state")]
        public IActionResult State()
        { // TODO: дstudent��state
            return Json(true);
        }

        /// <summary>
        /// ��ʼ��student״̬
        /// </summary>
        /// <remarks>
        ///   �Ѿ���ʼ�����ض���State
        ///   �����Ⱥ� SetSeed  SetStartTime
        /// </remarks>
        /// <returns></returns>
        /// <response code=""></response>
        [HttpGet("state/[action]")]
        public async Task<IActionResult> Initialize()
        {
            if (HttpContext.Session.Get("begintime") != null)
            { // �Ѿ���ʼ�����ض���State����
                return RedirectToAction(nameof(State));
            }

            await SetSeed();
            return SetStartTime();
        }

        /// <summary>
        /// ���³�ʼ��student״̬
        /// </summary>
        /// <remarks>
        ///    δ��ʼ�����ض���State����
        ///    �ѳ�ʼ������ ClearSeed ClearStartTime
        ///             �� SetSeed SetStartTime
        /// </remarks>
        /// <returns></returns>
        /// <response code=""></response>
        [HttpPost("state/[action]")]
        public async Task<IActionResult> Reset()
        {
            if (HttpContext.Session.Get("begintime") == null)
            { // δ��ʼ�����ض���State����
                return RedirectToAction(nameof(State));
            }

            ClearSeed();
            ClearStartTime();
            await SetSeed();
            return SetStartTime();
        }
        #endregion

        #region Seed APIs

        /// <summary>
        /// ����Seesion��seed
        /// </summary>
        /// <remarks>
        ///    Session��δ����seedʱ����seed
        /// </remarks>
        /// <returns>����ID</returns>
        /// <response code="200">��������ID</response>
        [HttpPost("seed")]
        [ProducesResponseType(typeof(int), 200)]
        public async Task<JsonResult> SetSeed()
        {
            if (HttpContext.Session.GetInt32("seed") == null)
            {
                var seed = await questionSeedService.RollSeed();
                HttpContext.Session.SetInt32("seed", seed.ID);
                return Json(seed);
            }
            return Json(await unitOfWork.QuestionSeedRepository.GetByIDAsync(HttpContext.Session.GetInt32("seed")));
        }

        /// <summary>
        /// ���Session��ǰseed
        /// </summary>
        /// <remarks>
        ///    ���Session��ǰseed
        /// </remarks>
        /// <returns></returns>
        /// <response code="204">No Content</response>
        [HttpDelete("seed")]
        public IActionResult ClearSeed()
        {
            HttpContext.Session.Remove("seed");
            return NoContent();
        }
        #endregion

        #region Time APIs

        /// <summary>
        /// ����ʣ��ʱ��
        /// </summary>
        /// <remarks>
        ///     ����ʣ��ʱ��
        /// </remarks>
        /// <returns>ʣ��ʱ��</returns>
        /// <response code="200">����ʣ��ʱ��</response>
        [HttpGet("time")]
        [ProducesResponseType(typeof(TimeSpan), 200)]
        public IActionResult GetLeftTime()
        {
            if(HttpContext.Session.Get<DateTime>("begintime") == default(DateTime))
            {
                return NoContent();
            }    
            TimeSpan timeLeft = TimeSpan.FromMinutes(30) - (DateTime.Now - HttpContext.Session.Get<DateTime>("begintime"));
            return Json(timeLeft);
        }

        /// <summary>
        /// ���ó�ʼʱ��
        /// </summary>
        /// <remarks>
        ///     ���ó�ʼʱ��begintime
        /// </remarks>
        /// <returns></returns>
        /// <response code="200"></response>
        [HttpPost("time")]
        [ProducesResponseType(typeof(DateTime), 200)]
        public IActionResult SetStartTime()
        {
            if(HttpContext.Session.Get<DateTime>("begintime") == default(DateTime))
            {
                DateTime now = DateTime.Now;
                HttpContext.Session.Set<DateTime>("begintime", now);
                return Ok(now);
            }
            return RedirectToAction(nameof(SetStartTime));
        }

        /// <summary>
        /// ��ճ�ʼʱ��
        /// </summary>
        /// <remarks>
        ///     ��ճ�ʼʱ��begintime
        /// </remarks>
        /// <returns></returns>
        /// <response code="204">No Content</response>
        [HttpDelete("time")]
        public IActionResult ClearStartTime()
        {
            HttpContext.Session.Remove("begintime");
            return NoContent();
        }
        #endregion Time APIs
    }
}
