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
        /// ��ȡ����ϸ��
        /// </summary>
        /// <remarks>
        ///    �����ܷ֣���ɵĽ���ʱ�䣬�����ʱ�͵÷ֵ�ϸ��
        ///    ����Ϊѧ��id
        ///    �ṩ��ǰ����ѧ����ֻ��߸ս�����ʱʹ��
        /// </remarks>
        /// <returns>��ǰ��¼id��û��id�����֤Ϊѧ�����Զ���ȡid���Ĵ���ϸ��</returns>
        /// <response code="200">���ص�ǰid(�û�)��Ӧ�Ĵ���ϸ��</response>
        /// <response code="400">��ǰid(�û�)�����ڻ���δ��ɴ���</response>
        [HttpGet("{id?}")]
        [ProducesResponseType(typeof(ResultViewModel), 200)]
        [ProducesResponseType(typeof(string), 400)]
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
                return BadRequest("Student not found");
            }
            if (!student.IsTested)
            {
                return BadRequest("Contest hasn't been completed");
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
        /// ���մ����ɴ���ϸ��
        /// </summary>
        /// <remarks>
        ///     ����𰸵ĸ�ʽ��
        ///     
        ///     [
        ///         {"id": int, "answer": int}
        ///     ]
        ///         
        /// </remarks>
        /// <returns>��ǰ�𰸶�Ӧ�Ĵ���ϸ��</returns>
        /// <response code="200">���ش𰸲�����Ӧ�Ĵ���ϸ��</response>
        /// <response code="400">����JSON��ʽ���Ϸ�</response>
        [HttpPost]
        [ProducesResponseType(typeof(ResultViewModel), 200)]
        [ProducesResponseType(typeof(string), 400)]
        public async Task<IActionResult> CountScore([FromBody]List<SubmittedAnswerViewModel> answers)
        {
            if(!ModelState.IsValid)
            {
                return BadRequest("Body JSON content invalid");
            }

            var student = await unitOfWork.StudentRepository.GetByIDAsync(int.Parse(HttpContext.Session.GetString("id")));
            student.Score = 0;
            student.DateTimeFinished = DateTime.Now;
            student.TimeConsumed = student.DateTimeFinished - HttpContext.Session.Get<DateTime>("begintime");
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
        /// ��ȡ���⼯��������ȷ��
        /// </summary>
        /// <remarks>
        ///    ��ǰ�û���Ӧ������seed�ķ�����ȷ�𰸵�JSON��ʽΪ��
        ///    
        ///     [
        ///         {
        ///             "id": int,
        ///             "answer": int,
        ///             "points": int
        ///         }
        ///     ]
        ///     
        /// </remarks>
        /// <returns>��ǰ�������ӵĴ�����</returns>
        /// <response code="200">����seed��Ӧ�Ĵ�����</response>
        /// <response code="400">seed��δ����</response>
        [HttpGet("answer")]
        [ProducesResponseType(typeof(List<CorrectAnswerViewModel>), 200)]
        [ProducesResponseType(typeof(string), 400)]
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
        /// ��ȡid��Ӧ�������ȷ��
        /// </summary>
        /// <remarks>
        ///    ��ǰid��Ӧ����𰸵�JSON��ʽΪ��
        ///    
        ///     {
        ///         "id": int,
        ///         "answer": int,
        ///         "points": int
        ///     }
        ///    
        /// </remarks>
        /// <returns>��ǰid��Ӧ����Ĵ�</returns>
        /// <response code="200">��id��Ӧ�Ĵ�</response>
        /// <response code="404">id��Ӧ�����ⲻ����</response>
        [HttpGet("answer/{id}")]
        [ProducesResponseType(typeof(CorrectAnswerViewModel), 200)]
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