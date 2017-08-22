using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using HistoryContest.Server.Data;
using HistoryContest.Server.Services;
using HistoryContest.Server.Extensions;
using HistoryContest.Server.Models.ViewModels;

namespace HistoryContest.Server.Controllers.APIs
{
    [Authorize]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ResultController : Controller
    {
        private readonly UnitOfWork unitOfWork;
        private readonly QuestionSeedService questionSeedService;

        public ResultController(UnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            questionSeedService = new QuestionSeedService(unitOfWork);
        }

        /// <summary>
        /// ��ȡһλѧ���Ŀ��Խ��
        /// </summary>
        /// <remarks>
        /// ����ѧ����ѧ��(ID)���ظ�ѧ���Ŀ��Խ����
        ///    
        /// ID�����ǿ�ѡ�ġ����������ID���ҵ�ǰ�û���֤Ϊѧ������ȡSession�е�ѧ����ΪID��
        ///    
        /// ʹ���龰��
        /// 1. ѧ��������Ϻ����µ�¼ʱ����ҳ���ض��򵽵������api��
        /// 2. ����Ա�ڿ���Ժ�÷����ʱ����Ҫ�鿴ĳλѧ���Ŀ�����ϸϸ�ڡ�
        /// </remarks>
        /// <param name="id">ѧ����ѧ��</param>
        /// <returns>ѧ�Ŷ�Ӧ�Ŀ��Խ��</returns>
        /// <response code="200">
        /// ��������ѯ��ѧ���Ŀ��Խ���������¼�������ɣ�
        /// * ����
        /// * ���ʱ�䡢������ʱ
        /// * ����ϸ��
        ///     - ����ϸ��Ϊ����ѯ��ѧ��������30�����������ɵ����飬
        ///       ÿ��Ԫ��������ID����ȷ�𰸡�ѧ���ύ�Ĵ𰸹��ɡ�
        /// </response>
        /// <response code="400">��ǰ�û�����ѧ�����ӦSession��û��ID</response>
        /// <response code="403">����ѯ��ѧ��û����ɿ���</response>
        /// <response code="404">����IDû�ж�Ӧ��ѧ��</response>
        [HttpGet("{id?}")]
        [ProducesResponseType(typeof(ResultViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetResult(int? id)
        {
            if (id == null && HttpContext.User.IsInRole("Student") && HttpContext.Session.Get("id") != null)
            { // ���������id���ҵ�ǰ�û���֤Ϊѧ������ȡSession�е�ѧ����Ϊid
                id = int.Parse(HttpContext.Session.GetString("id"));
            }
            else
            {
                return BadRequest("Empty argument request invalid");
            }

            var student = await unitOfWork.StudentRepository.GetByIDAsync(id);
            if(student == null)
            {
                return NotFound("Student not found");
            }
            if (!student.IsTested)
            {
                return Forbid("Contest has not been completed");
            }

            var model = new ResultViewModel();
            // TODO: ��model�ӻ�����ȡ������Ctrl + Alt + K��TODO�б�
            
            model.Score = (int)student.Score;
            model.Details = student.QuestionSeed.QuestionIDs.Zip(student.Choices, (ID, choice) => new ResultDetailViewModel
            {
                ID = ID,
                RightAnswer = (unitOfWork.QuestionRepository.GetByID(ID)).Answer,
                SubmittedAnswer = choice
            }).ToList();

            return Json(model);
        }



        /// <summary>
        /// ����ѧ�����Է���
        /// </summary>
        /// <remarks>
        /// **ע��**��Ŀǰ��˵�ʵ����ʱ���ڲ���*����ǰ�˴�������answers����*�����������Ҳ����˵ѧ��ûѡ�𰸵���Ŀ���û�д�����ˣ��������©��
        /// </remarks>
        /// <param name="answers">����ID�뿼��ѡ����������</param>
        /// <returns>�����Ŀ��Խ��</returns>
        /// <response code="200">���ؿ����Ŀ��Խ�����ý��JSON��ģ����`GET api/Result/{id}`��ͬ�������ڽ������²�ѯ��</response>
        /// <response code="400">
        /// * �����������ʽ���Ϸ�
        /// * ����������һ��IDû�ж�Ӧ������
        /// </response>
        [HttpPost]
        [Authorize(Roles = "Student, Administrator")]
        [ProducesResponseType(typeof(ResultViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CountScore([FromBody]List<SubmittedAnswerViewModel> answers)
        {
            if(!ModelState.IsValid)
            { // TODO:<yzh> ������Ҫ��������size�Ƿ���ȷ
                return BadRequest("Body JSON content invalid");
            }

            var student = await unitOfWork.StudentRepository.GetByIDAsync(int.Parse(HttpContext.Session.GetString("id")));
            student.Score = 0;
            student.DateTimeFinished = DateTime.Now;
            student.TimeConsumed = student.DateTimeFinished - HttpContext.Session.Get<DateTime>("beginTime");
            student.Choices = answers.Select(a => (byte)a.Answer).ToArray();

            var model = new ResultViewModel
            {
                Details = new List<ResultDetailViewModel>(capacity: 30),
                TimeFinished = (DateTime)student.DateTimeFinished,
                TimeConsumed = (TimeSpan)student.TimeConsumed
            };
            foreach(var a in answers)
            {
                var item = await unitOfWork.QuestionRepository.GetByIDAsync(a.ID);
                if (item == null)
                {
                    return BadRequest("Encounter invalid ID in answer set: " + a.ID);
                }
                student.Score += a.Answer == item.Answer ? item.Points : 0;
                model.Details.Add(new ResultDetailViewModel { ID = item.ID, RightAnswer = item.Answer, SubmittedAnswer = a.Answer });
            }
            model.Score = (int)student.Score;

            // TODO: ��model�浽����
            unitOfWork.StudentRepository.Update(student);
            await unitOfWork.SaveAsync();
            return Json(model);
        }

        /// <summary>
        /// ��ȡһ���Ծ�����д�
        /// </summary>
        /// <remarks>
        /// ���API���������Ӷ�Ӧ����������Ĵ𰸼���ֵ���أ���ǰ���ڱ��ؼ�������������ڷֵ����������㸺��������������  
        /// </remarks>
        /// <returns>��ǰ�������Ӷ�Ӧ����������Ĵ�</returns>
        /// <response code="200">���ص�ǰ�û�Session�д洢�������е���������Ĵ𰸡���ֵ</response>
        /// <response code="400">��ǰ�û�û�ж�Ӧ����������</response>
        [HttpGet("Answer")]
        [Authorize(Roles = "Student, Administrator")]
        [ProducesResponseType(typeof(List<CorrectAnswerViewModel>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAllAnswers()
        {
            var seed = HttpContext.Session.GetInt32("seed");
            if (seed == null)
            {
                return BadRequest("Question seed not created");
            }

            var source = await questionSeedService.GetQuestionsBySeedID((int)seed);
            if (source == null)
            {
                // TODO: ��ϸ�����쳣
                throw new Exception("Improper seed created, ID: " + seed);
            }

            return Json(source.Select(q => new CorrectAnswerViewModel { ID = q.ID, Answer = q.Answer, Points = q.Points }));
        }

        /// <summary>
        /// ��ȡһ����Ĵ�
        /// </summary>
        /// <remarks>
        /// ���API��Ҫ����� `POST api/question` ʹ�ã�ʹǰ���ܹ�ͨ����ŷ����ִεؼ����𰸣��ڱ��ؼ��������
        /// </remarks>
        /// /// <param name="id">�����Ӧ��ΨһID</param>
        /// <returns>ID��Ӧ����Ĵ�</returns>
        /// <response code="200">����ID��Ӧ����Ĵ𰸡���ֵ</response>
        /// <response code="404">IDû�ж�Ӧ������</response>
        [HttpGet("Answer/{id}")]
        [Authorize(Roles = "Student, Administrator")]
        [ProducesResponseType(typeof(CorrectAnswerViewModel), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAnswerByID(int id)
        {
            var item = await unitOfWork.QuestionRepository.GetByIDAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Json(new CorrectAnswerViewModel { ID = item.ID, Answer = item.Answer, Points = item.Points });
        }
    }
}