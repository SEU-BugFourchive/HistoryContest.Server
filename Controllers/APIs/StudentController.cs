using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HistoryContest.Server.Data;
using HistoryContest.Server.Models.Entities;
using HistoryContest.Server.Models.ViewModels;
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
        /// ��ȡѧ��״̬
        /// </summary>
        /// <remarks>
        /// Ŀǰ�������ѧ������״̬���Լ����������Ƿ�����ȷ���á�**ע�����API�ݲ����ѧ��ID�Ƿ������Session�С�**
        /// 
        /// ѧ���Ŀ���״̬�����֣���һ��ö�ٱ�ʾ������
        /// `enum TestState { NotTested = 0, Testing = 1, Tested = 2 }`
        /// 
        /// ���Ը��ݲ�ͬ��״̬����ҳ�ض��򵽲�ͬ��λ�á�
        /// </remarks>
        /// <returns>ѧ��״̬</returns>
        /// <response code="200">����ѧ���Ŀ���״̬���Լ����������Ƿ�����</response>
        /// <response code=""></response>
        [HttpGet("State")]
        [ProducesResponseType(typeof(StudentStateViewModel), StatusCodes.Status200OK)]
        public IActionResult State()
        {
            var state = new StudentStateViewModel();
            if (HttpContext.Session.Get<DateTime>("beginTime") != default(DateTime))
            {
                state.TestState = TestState.Testing;
                state.IsSeedSet = HttpContext.Session.GetInt32("seed") != null;
            }
            else if (HttpContext.Session.GetInt32("isTested") == 1)
            {
                state.TestState = TestState.Tested;
                state.IsSeedSet = true;
            }
            else
            {
                state.TestState = TestState.NotTested;
                state.IsSeedSet = HttpContext.Session.GetInt32("seed") != null;
            }

            return Json(state);
        }

        /// <summary>
        /// ��ʼ������״̬
        /// </summary>
        /// <remarks>
        /// ѧ����ʼ����ǰ��������ؿ���״̬������
        /// * ������������
        /// * ���ÿ��Կ�ʼʱ��
        /// </remarks>
        /// <returns>��ǰѧ�����Կ�ʼʱ��</returns>
        /// <response code="201">�������õĵ�ǰѧ�����Կ�ʼʱ��</response>
        /// <response code="302">�Ѿ���ʼ�����ض���`GET State`����</response>
        [HttpGet("State/[action]")]
        [ProducesResponseType(typeof(DateTime), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status302Found)]
        public async Task<IActionResult> Initialize()
        {
            if (HttpContext.Session.Get("beginTime") != null)
            { // �Ѿ���ʼ�����ض���State����
                return RedirectToAction(nameof(State));
            }

            await SetSeed();
            return SetStartTime();
        }

        /// <summary>
        /// ����ѧ������״̬
        /// </summary>
        /// <remarks>
        /// ��ѧ������������ؽ���ҳ����Ҫ���¿���ʱ����������ѧ���Ŀ���״̬��
        /// </remarks>
        /// <returns></returns>
        /// <response code=""></response>
        [HttpPost("State/[action]")]
        public async Task<IActionResult> Reset()
        {
            if (HttpContext.Session.Get("beginTime") == null)
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
        /// ����һ����������
        /// </summary>
        /// <remarks>
        /// ���������ӳ��������һ������ID�����䱣���ڵ�ǰ�û���Session�С�
        /// </remarks>
        /// <returns>��������ID</returns>
        /// <response code="201">�����������ӵ�ID</response>
        /// <response code="204">���������Ѿ�����</response>
        [HttpPost("Seed")]
        [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> SetSeed()
        {
            if (HttpContext.Session.GetInt32("seed") == null)
            {
                var seed = await questionSeedService.RollSeed();
                HttpContext.Session.SetInt32("seed", seed.ID);
                return CreatedAtAction(nameof(QuestionController.GetQuestionIDSet), nameof(QuestionController), null, seed.ID);
            }
            return NoContent();
        }

        /// <summary>
        /// �����ǰ��������
        /// </summary>
        /// <remarks>
        /// ���Session��ǰseed
        /// </remarks>
        /// <response code="204"></response>
        [HttpDelete("Seed")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult ClearSeed()
        {
            HttpContext.Session.Remove("seed");
            return NoContent();
        }
        #endregion

        #region Time APIs
        /// <summary>
        /// ����ʣ�࿼��ʱ��
        /// </summary>
        /// <remarks>
        /// ��30����Ϊ��׼��ͨ����ǰʱ����Session�п��Կ�ʼʱ��֮��������ǰѧ��ʣ�࿼��ʱ�䡣
        /// 
        /// ���ص��ַ�����ʽ������
        /// 
        ///     "00:29:34.2049107"
        /// 
        /// </remarks>
        /// <returns>ʣ�࿼��ʱ��</returns>
        /// <response code="200">����ʣ��Ŀ���ʱ��</response>
        /// <response code="204">��ǰѧ����δ��ʼ����</response>
        [HttpGet("Time")]
        [ProducesResponseType(typeof(TimeSpan), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult GetLeftTime()
        {
            if (HttpContext.Session.Get<DateTime>("beginTime") == default(DateTime))
            {
                return NoContent();
            }
            TimeSpan timeLeft = TimeSpan.FromMinutes(30) - (DateTime.Now - HttpContext.Session.Get<DateTime>("beginTime"));
            return Json(timeLeft);
        }

        /// <summary>
        /// ���ÿ��Կ�ʼʱ��
        /// </summary>
        /// <remarks>
        /// ����ǰ����ʱ��洢��Session�У���Ϊ���Կ�ʼʱ�䡣
        /// 
        /// ���ص��ַ�����ʽ������
        /// 
        ///     "2017-08-15T23:42:02.2776927+08:00"
        /// 
        /// </remarks>
        /// <returns>��ǰѧ�����Կ�ʼʱ��</returns>
        /// <response code="201">�������õĵ�ǰѧ�����Կ�ʼʱ��</response>
        /// <response code="302">ѧ���ѿ�ʼ���ԣ��ض���ͬһ��ַ��`GET`</response>
        [HttpPost("Time")]
        [ProducesResponseType(typeof(DateTime), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status302Found)]
        public IActionResult SetStartTime()
        {
            if (HttpContext.Session.Get<DateTime>("beginTime") == default(DateTime))
            {
                DateTime now = DateTime.Now;
                HttpContext.Session.Set<DateTime>("beginTime", now);
                return CreatedAtAction(nameof(GetLeftTime), now);
            }
            return RedirectToAction(nameof(GetLeftTime));
        }

        /// <summary>
        /// ������Կ�ʼʱ��
        /// </summary>
        /// <remarks>
        /// ��յ�ǰѧ��Session�п��Կ�ʼʱ��ļ�¼��
        /// </remarks>
        /// <response code="204"></response>
        [HttpDelete("Time")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public IActionResult ClearStartTime()
        {
            HttpContext.Session.Remove("beginTime");
            return NoContent();
        }
        #endregion Time APIs
    }
}
